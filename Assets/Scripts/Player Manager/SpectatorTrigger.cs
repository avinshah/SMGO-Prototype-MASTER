using Fusion;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Trigger component to enable spectator (Character 3) interactions at specific game moments.
/// Can be triggered by colliders, timers, or UnityEvents.
/// </summary>
public class SpectatorInteractionTrigger : NetworkBehaviour
{
    public enum TriggerMode
    {
        OnPlayerTouch,      // When any player touches this
        OnSpecificTouch,    // When specific character touches
        OnTimer,            // After X seconds
        OnManualCall,       // Via UnityEvent or code
        OnThreshold         // When InteractableReporter threshold reached
    }
    
    [Header("Trigger Configuration")]
    [SerializeField] private TriggerMode triggerMode = TriggerMode.OnPlayerTouch;
    [SerializeField] private CharacterRole requiredRole = CharacterRole.Character1;
    [SerializeField] private float timerDelay = 30f;
    [SerializeField] private bool triggerOnce = true;
    
    [Header("Action Configuration")]
    [SerializeField] private bool enableSpectatorInteractions = true;
    [SerializeField] private bool makeSpectatorsVisible = false;
    [SerializeField] private bool teleportSpectators = false;
    [SerializeField] private Transform spectatorTeleportDestination;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private string notificationMessage = "Spectators can now interact!";
    [SerializeField] private float notificationDuration = 3f;
    
    [Header("Events")]
    public UnityEvent OnTriggered;
    public UnityEvent<PlayerRef> OnSpectatorEnabled;
    
    // State
    [Networked] private NetworkBool HasTriggered { get; set; } = false;
    private float _timerStartTime;
    private AudioSource _audioSource;
    private InteractableReporter _reporter;
    
    private void Awake()
    {
        // Setup audio
        _audioSource = GetComponent<AudioSource>();
        if (!_audioSource && activationSound)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
            _audioSource.playOnAwake = false;
        }
        
        // Setup InteractableReporter if using threshold mode
        if (triggerMode == TriggerMode.OnThreshold)
        {
            _reporter = GetComponent<InteractableReporter>();
            if (_reporter)
            {
                _reporter.OnThresholdReached.AddListener(OnThresholdReached);
            }
            else
            {
                Debug.LogWarning("[SpectatorTrigger] Threshold mode requires InteractableReporter component!");
            }
        }
        
        // Setup collider for touch modes
        if (triggerMode == TriggerMode.OnPlayerTouch || triggerMode == TriggerMode.OnSpecificTouch)
        {
            var collider = GetComponent<Collider>();
            if (collider)
            {
                collider.isTrigger = true;
            }
        }
    }
    
    public override void Spawned()
    {
        base.Spawned();
        
        if (triggerMode == TriggerMode.OnTimer)
        {
            _timerStartTime = Time.time;
        }
    }
    
    private void Update()
    {
        if (triggerMode == TriggerMode.OnTimer && !HasTriggered)
        {
            if (Time.time - _timerStartTime >= timerDelay)
            {
                TryTrigger();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (triggerMode != TriggerMode.OnPlayerTouch && triggerMode != TriggerMode.OnSpecificTouch)
            return;
        
        if (HasTriggered && triggerOnce)
            return;
        
        // Check if it's a player
        var playerLink = PlayerLink.FindOn(other.transform);
        var playerNO = playerLink?.PlayerNO ?? other.GetComponentInParent<NetworkObject>();
        
        if (playerNO == null || !playerNO.HasInputAuthority)
            return;
        
        // Check role requirement for specific touch
        if (triggerMode == TriggerMode.OnSpecificTouch)
        {
            var role = LobbyManager.Instance?.GetPlayerRole(playerNO.InputAuthority) ?? CharacterRole.None;
            if (role != requiredRole)
                return;
        }
        
        TryTrigger();
    }
    
    private void OnThresholdReached()
    {
        if (triggerMode == TriggerMode.OnThreshold)
        {
            TryTrigger();
        }
    }
    
    /// <summary>
    /// Manual trigger method - can be called from UnityEvents or other scripts
    /// </summary>
    public void ManualTrigger()
    {
        if (triggerMode == TriggerMode.OnManualCall || !triggerOnce || !HasTriggered)
        {
            TryTrigger();
        }
    }
    
    private void TryTrigger()
    {
        if (HasTriggered && triggerOnce)
            return;
        
        // Only StateAuthority can execute the trigger
        if (Object.HasStateAuthority)
        {
            ExecuteTrigger();
        }
        else
        {
            RPC_RequestTrigger();
        }
    }
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestTrigger()
    {
        if (!HasTriggered || !triggerOnce)
        {
            ExecuteTrigger();
        }
    }
    
    private void ExecuteTrigger()
    {
        if (!Object.HasStateAuthority)
            return;
        
        HasTriggered = true;
        
        // Notify all clients
        RPC_OnTriggered();
        
        // Enable spectator interactions
        if (enableSpectatorInteractions)
        {
            EnableAllSpectators();
        }
        
        // Teleport spectators if configured
        if (teleportSpectators && spectatorTeleportDestination)
        {
            TeleportAllSpectators();
        }
        
        Debug.Log($"[SpectatorTrigger] Activated - Spectators enabled: {enableSpectatorInteractions}");
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnTriggered()
    {
        // Play effects
        if (effectPrefab)
        {
            var effect = Instantiate(effectPrefab, transform.position, transform.rotation);
            Destroy(effect, 5f);
        }
        
        if (_audioSource && activationSound)
        {
            _audioSource.PlayOneShot(activationSound);
        }
        
        // Show notification
        if (!string.IsNullOrEmpty(notificationMessage))
        {
            ShowNotification(notificationMessage);
        }
        
        // Fire events
        OnTriggered?.Invoke();
    }
    
    private void EnableAllSpectators()
    {
        if (!LobbyManager.Instance)
            return;
        
        // Find all Character 3 players
        foreach (var player in Runner.ActivePlayers)
        {
            var role = LobbyManager.Instance.GetPlayerRole(player);
            if (role == CharacterRole.Character3)
            {
                if (Runner.TryGetPlayerObject(player, out var playerObj))
                {
                    var visManager = playerObj.GetComponent<PlayerVisibilityManager>();
                    if (visManager)
                    {
                        if (makeSpectatorsVisible)
                        {
                            // Make them visible AND interactive (like regular players)
                            visManager.RPC_SetVisibilityState(
                                PlayerVisibilityManager.VisibilityState.InGame,
                                CharacterRole.Character3
                            );
                        }
                        else
                        {
                            // Keep them invisible but allow interactions
                            visManager.EnableSpectatorInteractions();
                        }
                        
                        OnSpectatorEnabled?.Invoke(player);
                    }
                }
            }
        }
    }
    
    private void TeleportAllSpectators()
    {
        if (!spectatorTeleportDestination)
            return;
        
        var teleporter = FindObjectOfType<DelayedTeleporter>();
        if (!teleporter)
        {
            Debug.LogWarning("[SpectatorTrigger] No DelayedTeleporter found for spectator teleportation");
            return;
        }
        
        foreach (var player in Runner.ActivePlayers)
        {
            var role = LobbyManager.Instance.GetPlayerRole(player);
            if (role == CharacterRole.Character3)
            {
                if (Runner.TryGetPlayerObject(player, out var playerObj))
                {
                    teleporter.RequestTeleport(playerObj, 0.5f, spectatorTeleportDestination);
                }
            }
        }
    }
    
    private void ShowNotification(string message)
    {
        // Create a temporary UI notification
        StartCoroutine(ShowNotificationCoroutine(message));
    }
    
    private System.Collections.IEnumerator ShowNotificationCoroutine(string message)
    {
        // Create canvas if doesn't exist
        GameObject canvasGO = new GameObject("SpectatorNotification");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.position = transform.position + Vector3.up * 2f;
        
        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(4f, 1f);
        rect.localScale = Vector3.one * 0.01f;
        
        // Add background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvas.transform, false);
        var bgImage = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0, 0, 0, 0.9f);
        
        // Add text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(canvas.transform, false);
        var text = textGO.AddComponent<UnityEngine.UI.Text>();
        text.text = message;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 50;
        text.color = Color.cyan;
        text.alignment = TextAnchor.MiddleCenter;
        
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        
        // Animate
        float elapsed = 0;
        while (elapsed < notificationDuration)
        {
            elapsed += Time.deltaTime;
            
            // Billboard to camera
            if (Camera.main)
            {
                Vector3 lookDir = canvas.transform.position - Camera.main.transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                    canvas.transform.rotation = Quaternion.LookRotation(lookDir);
            }
            
            // Fade out in last second
            if (elapsed > notificationDuration - 1f)
            {
                float alpha = (notificationDuration - elapsed);
                bgImage.color = new Color(0, 0, 0, 0.9f * alpha);
                text.color = new Color(0, 1, 1, alpha);
            }
            
            yield return null;
        }
        
        Destroy(canvasGO);
    }
    
    /// <summary>
    /// Reset the trigger (useful for testing or round-based games)
    /// </summary>
    public void ResetTrigger()
    {
        if (Object.HasStateAuthority)
        {
            HasTriggered = false;
            
            if (triggerMode == TriggerMode.OnTimer)
            {
                _timerStartTime = Time.time;
            }
        }
    }
    
    /// <summary>
    /// Check if this trigger has been activated
    /// </summary>
    public bool IsTriggered() => HasTriggered;
    
    private void OnDrawGizmosSelected()
    {
        // Show trigger area
        if (triggerMode == TriggerMode.OnPlayerTouch || triggerMode == TriggerMode.OnSpecificTouch)
        {
            Gizmos.color = HasTriggered ? Color.green : Color.yellow;
            var collider = GetComponent<Collider>();
            if (collider)
            {
                if (collider is BoxCollider box)
                {
                    Gizmos.DrawWireCube(transform.position + box.center, box.size);
                }
                else if (collider is SphereCollider sphere)
                {
                    Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
                }
            }
        }
        
        // Show teleport destination
        if (teleportSpectators && spectatorTeleportDestination)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, spectatorTeleportDestination.position);
            Gizmos.DrawWireSphere(spectatorTeleportDestination.position, 0.5f);
        }
    }
}