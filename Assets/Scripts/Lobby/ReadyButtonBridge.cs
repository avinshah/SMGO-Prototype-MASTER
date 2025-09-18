using UnityEngine;
using Fusion;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class ReadyButtonBridge : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("If set, only colliders with this tag count as hands. Leave empty to accept any collider (debug).")]
    public string handTag = "Hand";

    [Tooltip("Also listen to InteractableReporter.OnThresholdReached if present.")]
    public bool useReporterEvent = true;

    [Tooltip("Fallback: call toggle on raw OnTriggerEnter even if reporter event didn’t fire.")]
    public bool useRawTriggerFallback = true;

    [Header("Debug")]
    public bool logTouches = true;

    private InteractableReporter _reporter;
    private PlayerRef _me;

    private void Awake()
    {
        // Ensure trigger + kinematic RB so Unity will send trigger events reliably
        var col = GetComponent<Collider>() ?? gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        var rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;

        _reporter = GetComponent<InteractableReporter>();

        // Cache local player once (no NetworkObject needed on this prefab)
        _me = (LobbyManager.Instance && LobbyManager.Instance.Runner != null)
            ? LobbyManager.Instance.Runner.LocalPlayer
            : PlayerRef.None;

        if (logTouches)
            Debug.Log($"[ReadyButtonBridge] Awake on '{name}'. localPlayer={_me}");
    }

    private void OnEnable()
    {
        if (useReporterEvent && _reporter)
        {
            _reporter.TriggerThreshold = 1;
            _reporter.PerPlayerCooldown = 0.4f;
            _reporter.FireOnce = false;
            _reporter.OnThresholdReached.RemoveListener(OnPressedViaReporter);
            _reporter.OnThresholdReached.AddListener(OnPressedViaReporter);
        }
    }

    private void OnDisable()
    {
        if (_reporter) _reporter.OnThresholdReached.RemoveListener(OnPressedViaReporter);
    }

    // -------- Reporter event path --------
    private void OnPressedViaReporter()
    {
        if (logTouches) Debug.Log("[ReadyButtonBridge] OnThresholdReached fired.");
        SendToggleReady();
    }

    // -------- Raw trigger fallback --------
    private void OnTriggerEnter(Collider other)
    {
        if (!useRawTriggerFallback) return;

        bool isHand = string.IsNullOrEmpty(handTag) || other.CompareTag(handTag);
        if (!isHand) return;

        if (logTouches) Debug.Log($"[ReadyButtonBridge] Raw OnTriggerEnter from '{other.name}' (tag={other.tag}).");
        SendToggleReady();
    }

    private void SendToggleReady()
    {
        if (!LobbyManager.Instance) { if (logTouches) Debug.LogWarning("[ReadyButtonBridge] No LobbyManager.Instance"); return; }

        // Prefer the local player we cached (works even if reporter has no Runner)
        var who = _me;
        if (who == PlayerRef.None && LobbyManager.Instance.Runner != null)
            who = LobbyManager.Instance.Runner.LocalPlayer;

        if (who == PlayerRef.None) { if (logTouches) Debug.LogWarning("[ReadyButtonBridge] No valid PlayerRef"); return; }

        if (logTouches) Debug.Log($"[ReadyButtonBridge] Toggling ready for {who}.");
        LobbyManager.Instance.RPC_RequestReadyToggle(who);
    }
}
