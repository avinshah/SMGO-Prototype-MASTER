using Fusion;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hands-only touch highlighter for Photon Fusion (compat version).
/// - Put this on the cube root (with NetworkObject).
/// - Cube is a kinematic trigger; hands only need colliders.
/// - When a local hand overlaps, we RPC the server with Runner.LocalPlayer.
/// - Server flips IsLit for a short time; all clients tint the cube.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkTouchHighlighterHandsOnly : NetworkBehaviour
{
    [Header("Visuals")]
    public Color LitColor = Color.yellow;
    public float LitSeconds = 0.25f;

    [Header("Filter (optional)")]
    public LayerMask AcceptedLayers = ~0;     // accept all by default
    public string RequiredTag = "";           // empty = no tag filter

    [Header("Events (invoked on StateAuthority)")]
    public UnityEvent OnTouched;

    // Replicated state (compat: no OnChanged)
    [Networked] private NetworkBool IsLit { get; set; }
    [Networked] private double LitUntil { get; set; } // network-time (Runner.SimulationTime) when light should turn off

    // Local cache to detect changes without OnChanged
    private bool _lastIsLit;

    // visuals
    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private Color _baseColor = Color.white;

    private void Awake()
    {
        // Find any renderer
        _renderer = GetComponent<Renderer>();
        if (!_renderer) _renderer = GetComponentInChildren<Renderer>(true);

        if (_renderer)
        {
            _mpb = new MaterialPropertyBlock();
            var mat = _renderer.sharedMaterial;
            if (mat && mat.HasProperty("_Color")) _baseColor = mat.color;
        }

        // Physics: static trigger + kinematic RB
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // apply initial visual
        ApplyVisual(IsLit);
        _lastIsLit = IsLit;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Simple filters
        if (!LayerAccepted(other.gameObject)) return;
        if (!TagAccepted(other.gameObject)) return;

        if (Runner == null) return;

        // Hands are local-only; the overlap only happens on the local client wearing the headset.
        // Report using the local PlayerRef. Server will validate/act.
        RPC_ReportTouch(Runner.LocalPlayer);
    }

    // Client -> Server
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportTouch(PlayerRef who)
    {
        // Start/extend the lit window
        IsLit = true;
        var now = Runner != null ? Runner.SimulationTime : 0.0;
        LitUntil = System.Math.Max(LitUntil, now + LitSeconds);

        // Fire server-side event once per report
        OnTouched?.Invoke();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        var now = Runner.SimulationTime;
        if (IsLit && now >= LitUntil)
            IsLit = false;
    }

    private void Update()
    {
        // Compat "OnChanged": if replicated flag changed, update visuals
        if (IsLit != _lastIsLit)
        {
            ApplyVisual(IsLit);
            _lastIsLit = IsLit;
        }
    }

    private void ApplyVisual(bool lit)
    {
        if (_renderer == null || _mpb == null) return;
        var c = lit ? LitColor : _baseColor;
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", c);
        _renderer.SetPropertyBlock(_mpb);
    }

    private bool LayerAccepted(GameObject go) => (AcceptedLayers.value & (1 << go.layer)) != 0;

    private bool TagAccepted(GameObject go)
    {
        if (string.IsNullOrEmpty(RequiredTag)) return true;
        if (go.CompareTag(RequiredTag)) return true;
        var p = go.transform.parent;
        return p && p.CompareTag(RequiredTag);
    }
}
