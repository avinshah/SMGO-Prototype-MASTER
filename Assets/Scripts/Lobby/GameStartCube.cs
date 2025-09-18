using Fusion;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Controls the Game Start Cube that appears when all players are ready.
/// Any player can interact with it to start the game.
/// </summary>
public class GameStartCubeController : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color countdownColor = Color.yellow;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseIntensity = 0.2f;
    [SerializeField] private float rotationSpeed = 30f;
    
    [Header("Countdown Settings")]
    [SerializeField] private bool useCountdown = true;
    [SerializeField] private float countdownDuration = 3f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem startParticles;
    [SerializeField] private AudioClip countdownSound;
    [SerializeField] private AudioClip startSound;
    
    [Header("UI Elements")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private Text statusText;
    
    private InteractableReporter _reporter;
    private Renderer _renderer;
    private AudioSource _audioSource;
    private Material _material;
    private Vector3 _originalScale;
    private bool _isCountingDown = false;
    private float _countdownTimer;
    
    private void Awake()
    {
        _originalScale = transform.localScale;
        
        // Setup renderer
        _renderer = GetComponent<Renderer>();
        if (_renderer)
        {
            _material = _renderer.material;
            _material.color = activeColor;
        }
        
        // Setup audio
        _audioSource = GetComponent<AudioSource>();
        if (!_audioSource)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
            _audioSource.playOnAwake = false;
        }
        
        // Setup InteractableReporter
        SetupInteractableReporter();
        
        // Create UI
        if (!worldCanvas)
        {
            CreateWorldCanvas();
        }
        
        // Setup collider
        var collider = GetComponent<Collider>();
        if (collider)
        {
            collider.isTrigger = true;
        }
        
        // Setup particles if assigned
        if (startParticles)
        {
            startParticles.Stop();
        }
        
        UpdateDisplay("START GAME", activeColor);
    }
    
    private void SetupInteractableReporter()
    {
        _reporter = GetComponent<InteractableReporter>();
        if (!_reporter)
        {
            _reporter = gameObject.AddComponent<InteractableReporter>();
        }
        
        _reporter.TriggerThreshold = 1;
        _reporter.FireOnce = true; // Only allow one activation
        _reporter.PerPlayerCooldown = 1f;
        _reporter.MainCategory = Category.Box;
        _reporter.ObjectLabel = "GameStartCube";
        
        _reporter.OnThresholdReached.RemoveAllListeners();
        _reporter.OnThresholdReached.AddListener(OnStartActivated);
    }
    
    private void OnStartActivated()
    {
        if (_isCountingDown || !LobbyManager.Instance) return;
        
        // Check one more time if we can start
        if (!CanStartGame())
        {
            ShowError("Not all players ready!");
            return;
        }
        
        if (useCountdown)
        {
            StartCoroutine(CountdownSequence());
        }
        else
        {
            StartGameImmediately();
        }
    }
    
    private IEnumerator CountdownSequence()
    {
        _isCountingDown = true;
        _countdownTimer = countdownDuration;
        
        // Disable further interactions
        if (_reporter) _reporter.enabled = false;
        
        // Visual feedback
        if (_material) _material.color = countdownColor;
        
        while (_countdownTimer > 0)
        {
            // Update display
            int secondsLeft = Mathf.CeilToInt(_countdownTimer);
            UpdateDisplay($"STARTING IN\n{secondsLeft}", countdownColor);
            
            // Play countdown sound
            if (_audioSource && countdownSound && _countdownTimer % 1f < Time.deltaTime)
            {
                _audioSource.PlayOneShot(countdownSound, 0.5f);
            }
            
            // Increase pulse intensity during countdown
            float currentPulse = pulseIntensity * (1f + (countdownDuration - _countdownTimer) / countdownDuration);
            float scale = 1f + Mathf.Sin(Time.time * pulseSpeed * 2f) * currentPulse;
            transform.localScale = _originalScale * scale;
            
            _countdownTimer -= Time.deltaTime;
            yield return null;
        }
        
        // Final effects
        UpdateDisplay("GO!", Color.white);
        
        if (startParticles) startParticles.Play();
        if (_audioSource && startSound) _audioSource.PlayOneShot(startSound, 1f);
        
        // Start the game
        StartGameImmediately();
        
        // Hide cube after a moment
        yield return new WaitForSeconds(0.5f);
        gameObject.SetActive(false);
    }
    
    private void StartGameImmediately()
    {
        if (_reporter.TryGetLastInteractor(out var interactor))
        {
            LobbyManager.Instance.RPC_RequestGameStart();
        }
    }
    
    private bool CanStartGame()
    {
        if (!LobbyManager.Instance) return false;
        
        // This is a simplified check - the actual logic is in LobbyManager
        // You might want to expose a public method in LobbyManager for this
        return true;
    }
    
    private void ShowError(string message)
    {
        StartCoroutine(ShowErrorMessage(message));
    }
    
    private IEnumerator ShowErrorMessage(string message)
    {
        Color originalColor = _material ? _material.color : activeColor;
        
        UpdateDisplay(message, Color.red);
        if (_material) _material.color = Color.red;
        
        yield return new WaitForSeconds(2f);
        
        UpdateDisplay("START GAME", originalColor);
        if (_material) _material.color = originalColor;
    }
    
    private void CreateWorldCanvas()
    {
        // Create canvas above cube
        GameObject canvasGO = new GameObject("StartCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.up * 1f;
        
        worldCanvas = canvasGO.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        
        var rectTransform = worldCanvas.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(2f, 0.8f);
        rectTransform.localScale = Vector3.one * 0.01f;
        
        // Background panel
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.9f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // Main text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);
        statusText = textGO.AddComponent<Text>();
        statusText.text = "START GAME";
        statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusText.fontSize = 40;
        statusText.fontStyle = FontStyle.Bold;
        statusText.color = Color.green;
        statusText.alignment = TextAnchor.MiddleCenter;
        
        var textRect = statusText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
        
        // Add glow effect
        var outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0, 1, 0, 0.5f);
        outline.effectDistance = new Vector2(2, 2);
    }
    
    private void UpdateDisplay(string text, Color color)
    {
        if (statusText)
        {
            statusText.text = text;
            statusText.color = color;
        }
    }
    
    private void Update()
    {
        if (!_isCountingDown)
        {
            // Gentle rotation
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            
            // Pulse effect
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
            transform.localScale = _originalScale * pulse;
        }
        
        // Billboard canvas
        if (worldCanvas && Camera.main)
        {
            Vector3 lookDir = worldCanvas.transform.position - Camera.main.transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                worldCanvas.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }
    
    public void Show()
    {
        gameObject.SetActive(true);
        _isCountingDown = false;
        if (_reporter) _reporter.enabled = true;
        UpdateDisplay("START GAME", activeColor);
    }
    
    public void Hide()
    {
        gameObject.SetActive(false);
    }
    
    private void OnEnable()
    {
        // Reset state when shown
        _isCountingDown = false;
        transform.localScale = _originalScale;
        if (_material) _material.color = activeColor;
    }
}