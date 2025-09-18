using UnityEngine;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class StartGameButtonBridge : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("If set, only colliders with this tag count as hands. Leave empty to accept any collider (debug).")]
    public string handTag = "Hand";

    [Tooltip("Also listen to InteractableReporter.OnThresholdReached if present.")]
    public bool useReporterEvent = true;

    [Tooltip("Fallback: call start on raw OnTriggerEnter even if reporter event didn’t fire.")]
    public bool useRawTriggerFallback = true;

    [Header("Debug")]
    public bool logTouches = true;

    private InteractableReporter _reporter;

    private void Awake()
    {
        var col = GetComponent<Collider>() ?? gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        var rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;

        _reporter = GetComponent<InteractableReporter>();
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

    private void OnPressedViaReporter()
    {
        if (logTouches) Debug.Log("[StartGameButtonBridge] OnThresholdReached fired.");
        SendStart();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useRawTriggerFallback) return;

        bool isHand = string.IsNullOrEmpty(handTag) || other.CompareTag(handTag);
        if (!isHand) return;

        if (logTouches) Debug.Log($"[StartGameButtonBridge] Raw OnTriggerEnter from '{other.name}' (tag={other.tag}).");
        SendStart();
    }

    private void SendStart()
    {
        if (!LobbyManager.Instance) { if (logTouches) Debug.LogWarning("[StartGameButtonBridge] No LobbyManager.Instance"); return; }

        if (logTouches) Debug.Log("[StartGameButtonBridge] Requesting game start.");
        LobbyManager.Instance.RPC_RequestGameStart();

        DebugFeed.Log("[Start] RequestGameStart invoked");
    }
}
