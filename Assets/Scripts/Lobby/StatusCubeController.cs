using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player status indicator (traffic light + extras):
/// - RED: present, no character, not ready
/// - AMBER (flashing): Character1/2 selected, not ready
/// - GREEN (glow): Character1/2 selected + ready
/// - PURPLE: Character3 selected, not ready
/// - JADE (glow): Character3 selected + ready
/// - BLUE (glow): Ready with no role
///
/// Includes optional world-space label and a custom label for simulated cubes.
/// </summary>
public class StatusCubeController : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color redColor = Color.red;
    [SerializeField] private Color amberColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color greenColor = new Color(0.25f, 1f, 0.25f);
    [SerializeField] private Color purpleColor = new Color(0.6f, 0.4f, 1f);
    [SerializeField] private Color jadeColor = new Color(0.2f, 0.9f, 0.6f);
    [SerializeField] private Color blueColor = new Color(0.3f, 0.6f, 1f);

    [Header("Effects")]
    [SerializeField] private float amberFlashSpeed = 4f;   // Hz-ish
    [SerializeField] private float amberFlashAmount = 0.5f; // 0..1
    [SerializeField] private float readyGlowSpeed = 2.5f;
    [SerializeField] private float readyGlowAmount = 0.35f;

    [Header("UI (optional)")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private Text playerText;
    [SerializeField] private Text statusText;

    // --- State ---
    private PlayerRef _owner;
    private int _playerNumber;
    private string _customLabel; // for simulated cubes
    private CharacterRole _role = CharacterRole.None;
    private bool _ready = false;

    private bool _flashAmber; // defined ONCE
    private bool _glowReady;  // defined ONCE

    private Renderer _renderer;
    private Material _mat;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer) _mat = _renderer.material;

        if (!worldCanvas) CreateWorldCanvas();
        UpdateVisuals();
    }

    public void Initialize(PlayerRef owner, int playerNumber)
    {
        _owner = owner;
        _playerNumber = playerNumber;
        UpdateTexts();
    }

    /// <summary>For simulated cubes: set a custom top label (e.g., "Sim P2").</summary>
    public void SetCustomPlayerLabel(string label)
    {
        _customLabel = label;
        UpdateTexts();
    }

    /// <summary>Authoritative update from LobbyManager.</summary>
    public void ApplyVisual(CharacterRole role, bool ready, bool flashingAmber)
    {
        _role = role;
        _ready = ready;

        // flash when Char1/2 and not ready (or explicit flag)
        _flashAmber = (!_ready && (role == CharacterRole.Character1 || role == CharacterRole.Character2)) || flashingAmber;
        // glow any time we're ready, regardless of role color
        _glowReady = _ready;

        UpdateVisuals();
    }

    public void SetPosition(Vector3 worldPos) => transform.position = worldPos;

    private void Update()
    {
        // AMBER flashing for Char1/2 when NOT ready
        if (_flashAmber && !_ready && _mat)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * amberFlashSpeed);
            Color baseCol = GetBaseColor();
            _mat.color = Color.Lerp(baseCol, Color.white, amberFlashAmount * t);
        }
        // READY glow (green/jade/blue depending on role)
        else if (_glowReady && _mat)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * readyGlowSpeed);
            Color baseCol = GetBaseColor();
            _mat.color = Color.Lerp(baseCol, Color.white, readyGlowAmount * t);
        }

        // Billboard UI
        if (worldCanvas && Camera.main)
        {
            Vector3 look = worldCanvas.transform.position - Camera.main.transform.position;
            look.y = 0f;
            if (look != Vector3.zero)
                worldCanvas.transform.rotation = Quaternion.LookRotation(look);
        }
    }

    private void UpdateVisuals()
    {
        if (_mat) _mat.color = GetBaseColor();
        UpdateTexts();
    }

    private Color GetBaseColor()
    {
        if (_ready)
        {
            return _role switch
            {
                CharacterRole.Character1 => greenColor,  // ready Char1
                CharacterRole.Character2 => greenColor,  // ready Char2
                CharacterRole.Character3 => jadeColor,   // ready spectator
                _ => blueColor,                           // ready with no role
            };
        }

        // Not ready
        return _role switch
        {
            CharacterRole.Character1 => amberColor,
            CharacterRole.Character2 => amberColor,
            CharacterRole.Character3 => purpleColor,
            _ => redColor,
        };
    }

    private void UpdateTexts()
    {
        if (playerText)
        {
            playerText.text = !string.IsNullOrEmpty(_customLabel)
                ? _customLabel
                : (_playerNumber > 0 ? $"P{_playerNumber}" : "");
        }

        if (statusText)
        {
            string roleTxt = _role switch
            {
                CharacterRole.Character1 => "Char 1",
                CharacterRole.Character2 => "Char 2",
                CharacterRole.Character3 => "Spectator",
                _ => "No Role"
            };
            string stateTxt = _ready ? "READY" : (_role == CharacterRole.None ? "JOINED" : "SELECTED");
            statusText.text = $"{roleTxt}\n{stateTxt}";
            statusText.color = _ready ? Color.white : Color.yellow;
        }
    }

    private void CreateWorldCanvas()
    {
        var canvasGO = new GameObject("StatusCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.up * 0.4f;

        worldCanvas = canvasGO.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;

        var rect = worldCanvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0.8f, 0.6f);
        rect.localScale = Vector3.one * 0.01f;

        // Background
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.75f);
        var bgRect = bgImg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        // Player text (top)
        var pGO = new GameObject("PlayerText");
        pGO.transform.SetParent(canvasGO.transform, false);
        playerText = pGO.AddComponent<Text>();
        playerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        playerText.fontSize = 32;
        playerText.color = Color.white;
        playerText.alignment = TextAnchor.UpperCenter;
        var pRect = playerText.GetComponent<RectTransform>();
        pRect.anchorMin = new Vector2(0, 0.5f);
        pRect.anchorMax = new Vector2(1, 1);
        pRect.offsetMin = new Vector2(10, 0);
        pRect.offsetMax = new Vector2(-10, -5);

        // Status text (bottom)
        var sGO = new GameObject("StatusText");
        sGO.transform.SetParent(canvasGO.transform, false);
        statusText = sGO.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusText.fontSize = 28;
        statusText.color = Color.yellow;
        statusText.alignment = TextAnchor.LowerCenter;
        var sRect = statusText.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0, 0);
        sRect.anchorMax = new Vector2(1, 0.5f);
        sRect.offsetMin = new Vector2(10, 5);
        sRect.offsetMax = new Vector2(-10, 0);
    }
}
