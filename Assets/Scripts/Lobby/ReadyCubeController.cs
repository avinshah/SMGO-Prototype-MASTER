using Fusion;
using UnityEngine;

public class ReadyCubeController : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color redColor = Color.red;                 // no character
    [SerializeField] private Color yellowColor = new Color(1f, 0.85f, 0f);  // has character, not ready
    [SerializeField] private Color greenColor = Color.green;               // ready

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.1f;
    [SerializeField] private float colorPulseSpeed = 2.0f;
    [SerializeField] private float colorPulseAmount = 0.25f;

    private Renderer _renderer;
    private Material _mat;
    private Vector3 _baseScale;

    // Cached state (for color re-eval)
    private CharacterRole _role = CharacterRole.None;
    private bool _isReady = false;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer) _mat = _renderer.material;
        _baseScale = transform.localScale;

        // Ensure we have a trigger + kinematic rigidbody (also done in manager, but safe to keep)
        var col = GetComponent<Collider>() ?? gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        var rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // If an InteractableReporter is present, leave wiring to LobbyManager.SpawnLocalReadyCube()
        // (it registers OnThresholdReached and calls RPC_RequestReadyToggle)
    }

    private void Update()
    {
        // Pulse only when ready
        if (_isReady)
        {
            float s = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = _baseScale * s;

            if (_mat)
            {
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * colorPulseSpeed);
                _mat.color = Color.Lerp(greenColor, Color.white, colorPulseAmount * t);
            }
        }
        else
        {
            transform.localScale = _baseScale;
        }
    }

    /// <summary>Called by LobbyManager after spawn and after every state change via RPC_UpdateReadyCubeVisual.</summary>
    public void SetReadyState(bool ready)
    {
        _isReady = ready;
        ApplyColor();
    }

    /// <summary>Helper so LobbyManager can set initial debug color (Red/Yellow/Green) on spawn.</summary>
    public void SetDebugColor(CharacterRole role, bool ready)
    {
        _role = role;
        _isReady = ready;
        ApplyColor();
    }

    private void ApplyColor()
    {
        if (!_mat) return;

        if (_isReady)
        {
            _mat.color = greenColor;                      // green if ready (even with no role)
        }
        else
        {
            // not ready
            if (_role == CharacterRole.None) _mat.color = redColor;   // no character
            else _mat.color = yellowColor; // character chosen (1/2/3), not ready
        }
    }
}
