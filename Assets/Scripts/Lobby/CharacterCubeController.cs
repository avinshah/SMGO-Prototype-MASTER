using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles interaction with Character Selection Cubes in the lobby.
/// Attach this to each character cube (1, 2, and 3).
/// </summary>
public class CharacterCubeController : MonoBehaviour
{
    [Header("Configuration")]
    private CharacterRole _role;
    private LobbyManager _lobbyManager;
    
    [Header("Visual Elements")]
    [SerializeField] private TextMesh displayText;
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private Text canvasText;
    
    [Header("Colors")]
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color occupiedColor = Color.yellow;
    [SerializeField] private Color flashColor = Color.cyan;
    
    private InteractableReporter _reporter;
    private Renderer _renderer;
    private Color _originalColor;
    private bool _isOccupied;
    
    public void Initialize(CharacterRole role, LobbyManager manager)
    {
        _role = role;
        _lobbyManager = manager;
        
        SetupComponents();
        UpdateVisuals(false, string.Empty);
    }
    
    private void SetupComponents()
    {
        // Get or add renderer
        _renderer = GetComponent<Renderer>();
        if (_renderer)
        {
            _originalColor = _renderer.material.color;
        }
        
        // Setup InteractableReporter for touch detection
        _reporter = GetComponent<InteractableReporter>();
        if (!_reporter)
        {
            _reporter = gameObject.AddComponent<InteractableReporter>();
        }
        
        // Configure reporter
        _reporter.TriggerThreshold = 1;
        _reporter.FireOnce = false; // Allow multiple interactions
        _reporter.PerPlayerCooldown = 0.5f;
        _reporter.MainCategory = Category.Box; // Use existing category
        _reporter.ObjectLabel = $"Character{(int)_role}Cube";
        
        // Clear any existing listeners and add our handler
        _reporter.OnThresholdReached.RemoveAllListeners();
        _reporter.OnThresholdReached.AddListener(OnCubeTouched);
        
        // Create or setup world canvas for status text
        if (!worldCanvas)
        {
            CreateWorldCanvas();
        }
        
        // Setup collider as trigger
        var collider = GetComponent<Collider>();
        if (collider)
        {
            collider.isTrigger = true;
        }
        
        // Add NetworkObject if missing (for Fusion)
        if (!GetComponent<NetworkObject>())
        {
            gameObject.AddComponent<NetworkObject>();
        }
    }
    
    private void CreateWorldCanvas()
    {
        // Create canvas above the cube
        GameObject canvasGO = new GameObject($"CharacterCanvas_{_role}");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.up * 0.6f;
        
        worldCanvas = canvasGO.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        
        var rectTransform = worldCanvas.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(2f, 0.5f);
        rectTransform.localScale = Vector3.one * 0.01f;
        
        // Add background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // Add text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);
        canvasText = textGO.AddComponent<Text>();
        canvasText.text = GetDefaultText();
        canvasText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        canvasText.fontSize = 40;
        canvasText.color = Color.white;
        canvasText.alignment = TextAnchor.MiddleCenter;
        
        var textRect = canvasText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);
    }
    
    private void OnCubeTouched()
    {
        if (!_lobbyManager) return;
        
        // Get the last interactor from the reporter
        if (_reporter.TryGetLastInteractor(out var interactor))
        {
            // Request character selection through lobby manager
            _lobbyManager.RPC_RequestCharacterSelection(interactor, _role);
            
            // Visual feedback
            if (_role == CharacterRole.Character3)
            {
                // Character 3 shows brief flash
                StartCoroutine(FlashEffect());
            }
        }
    }
    
    public void UpdateVisuals(bool occupied, string occupantName)
    {
        _isOccupied = occupied;
        
        // Update color
        if (_renderer)
        {
            if (_role == CharacterRole.Character3)
            {
                // Character 3 always shows as available
                _renderer.material.color = availableColor;
            }
            else
            {
                _renderer.material.color = occupied ? occupiedColor : availableColor;
            }
        }
        
        // Update text
        string statusText = GetStatusText(occupied, occupantName);
        
        if (canvasText)
        {
            canvasText.text = statusText;
        }
        
        if (displayText)
        {
            displayText.text = statusText;
        }
    }
    
    private string GetDefaultText()
    {
        return _role switch
        {
            CharacterRole.Character1 => "Character 1\n(Player)",
            CharacterRole.Character2 => "Character 2\n(Player)",
            CharacterRole.Character3 => "Character 3\n(Spectator)",
            _ => "Unknown"
        };
    }
    
    private string GetStatusText(bool occupied, string occupantName)
    {
        if (_role == CharacterRole.Character3)
        {
            return "Character 3\n(Spectator)\nUnlimited";
        }
        
        if (occupied)
        {
            return $"Character {(int)_role}\nOCCUPIED\n{occupantName}";
        }
        
        return GetDefaultText() + "\nAvailable";
    }
    
    private System.Collections.IEnumerator FlashEffect()
    {
        if (!_renderer) yield break;
        
        Color original = _renderer.material.color;
        _renderer.material.color = flashColor;
        yield return new WaitForSeconds(1f);
        _renderer.material.color = original;
    }
    
    private void Update()
    {
        // Billboard the canvas to camera
        if (worldCanvas && Camera.main)
        {
            Vector3 lookDir = worldCanvas.transform.position - Camera.main.transform.position;
            lookDir.y = 0; // Keep upright
            if (lookDir != Vector3.zero)
            {
                worldCanvas.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }
    
    public void SetOccupied(bool occupied, PlayerRef occupant = default)
    {
        string occupantName = occupied && occupant != PlayerRef.None ? $"Player {occupant.PlayerId}" : "";
        UpdateVisuals(occupied, occupantName);
    }
    
    public void ShowFlash()
    {
        StartCoroutine(FlashEffect());
    }
    
    public CharacterRole GetRole() => _role;
    public bool IsOccupied() => _isOccupied;
}