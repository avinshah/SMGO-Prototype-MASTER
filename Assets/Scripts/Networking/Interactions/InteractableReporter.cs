using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Local VR-hand trigger detector that (a) logs to GameRecorder and (b) raises an OnThresholdReached
/// UnityEvent you can wire to Ready / Start buttons. Requires a trigger collider.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class InteractableReporter : NetworkBehaviour
{

    [Header("GameRecorder Choice Reporting (optional)")]
    public bool ReportChoice = false;
    public string ChoiceKey = "A";
    public int ChoiceDelta = 1;

    [Header("MAIN category (for GameRecorder)")]
    public Category MainCategory = Category.Box;
    public int MainDelta = 1;

    [Header("SUB category (optional)")]
    public string SubCategoryName = "";
    public int SubDelta = 1;

    [Header("Optional: override object label in recorder log")]
    public string ObjectLabel;

    [Header("Detection Settings")]
    [Tooltip("Tag that identifies VR hands. If empty, will also match typical hand collider names.")]
    public string handTag = "Hand";
    public LayerMask handLayers = ~0;

    [Header("Threshold / Events")]
    [Tooltip("How many touches are needed to fire the event once.")]
    public int TriggerThreshold = 1;
    [Tooltip("If true, fire only the first time; otherwise can fire repeatedly as threshold is re-met.")]
    public bool FireOnce = false;
    [Tooltip("Minimum seconds between touches from the same local player.")]
    public float PerPlayerCooldown = 0.4f;
    public UnityEvent OnThresholdReached = new UnityEvent();

    [Header("Visual Feedback")]
    public bool changeColorOnTouch = true;
    public Color touchColor = Color.green;
    public float colorResetTime = 0.3f;

    // InteractableReporter.cs
    [SerializeField] private bool ignoreOverlapsOnEnable = true;
    [SerializeField] private float armDelay = 0.1f;
    private bool _armed;

    // Fields
    [SerializeField] private bool autoRearmOnExit = true;

    // Tracking fields (local)
    private float _lastTouchTime;
    private int _localTouchCount;
    private bool _localFired;
    private PlayerRef _lastInteractor = PlayerRef.None;
    private Renderer _renderer;
    private Color _originalColor;

    // Networked (for analytics / HUDs if you want)
    [Networked] public int TriggerCount { get; private set; }
    [Networked] public bool ThresholdFired { get; private set; }

    private void Awake()
    {
        // Ensure trigger collider
        var col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;

        _renderer = GetComponent<Renderer>();
        if (_renderer) _originalColor = _renderer.material.color;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_armed) return; // <-- ignore initial overlaps
        // Optional visibility gate
        var vm = other.GetComponentInParent<PlayerVisibilityManager>();
        if (vm && !vm.CanInteractWith(gameObject)) return;

        TryReport(other);
    }

    private void OnEnable()
    {
        if (ignoreOverlapsOnEnable)
        {
            _armed = false;
            StartCoroutine(ArmAfterDelay(armDelay));
        }
        else
        {
            _armed = true;
        }
    }
    private System.Collections.IEnumerator ArmAfterDelay(float d)
    {
        yield return new WaitForSeconds(d);
        _armed = true;
    }



    /// <summary>Programmatic activation (alternative to trigger).</summary>
    public void ActivateByPlayer(NetworkObject playerNetObj)
    {
        if (playerNetObj == null) return;
        if (!playerNetObj.HasInputAuthority) return;

        _lastTouchTime = Time.time;
        if (changeColorOnTouch) ShowTouchFeedback();
        ReportTouch();
        EvaluateThreshold();
    }

    private void TryReport(Collider other)
    {
        if (!IsHand(other)) return;

        if (Time.time - _lastTouchTime < PerPlayerCooldown) return;
        _lastTouchTime = Time.time;

        if (changeColorOnTouch) ShowTouchFeedback();
        ReportTouch();
        EvaluateThreshold();
    }

    private bool IsHand(Collider other)
    {
        if (!string.IsNullOrEmpty(handTag) && other.CompareTag(handTag)) return true;
        if ((handLayers.value & (1 << other.gameObject.layer)) != 0) return true;

        // fallback by name
        string n = other.name.ToLower();
        return n.Contains("hand") || n.Contains("capsule") || n.Contains("finger") || n.Contains("bone");
    }

    private void ShowTouchFeedback()
    {
        if (!_renderer) return;
        StopAllCoroutines();
        _renderer.material.color = touchColor;
        StartCoroutine(ResetColor(colorResetTime));
    }

    private System.Collections.IEnumerator ResetColor(float t)
    {
        yield return new WaitForSeconds(t);
        if (_renderer) _renderer.material.color = _originalColor;
    }

    private void ReportTouch()
    {
        // Remember who touched it (for listeners that need the player)
        _lastInteractor = Runner != null ? Runner.LocalPlayer : PlayerRef.None;

        // ----- Choice reporting (optional) -----
        var me = Runner ? Runner.LocalPlayer : PlayerRef.None;
        if (ReportChoice && GameRecorder.Instance != null && me != PlayerRef.None)
        {
            var key = string.IsNullOrWhiteSpace(ChoiceKey) ? "" : ChoiceKey.Trim();
            Debug.Log($"[Reporter] CHOICE key='{key}' Δ{ChoiceDelta} from {me} on '{name}'");
            GameRecorder.Instance.RPC_RecordChoice(me, key, ChoiceDelta);
        }

        // ----- Main/Sub interaction event (to recorder) -----
        // Only the authority sends the interaction RPC to avoid duplicates
        if (GameRecorder.Instance != null && Object && Object.HasStateAuthority)
        {
            string label = string.IsNullOrEmpty(ObjectLabel) ? gameObject.name : ObjectLabel;
            string subKey = string.IsNullOrWhiteSpace(SubCategoryName) ? "" : SubCategoryName.Trim();
            GameRecorder.Instance.RPC_ReportInteractionEx(Runner.LocalPlayer, label, MainCategory, MainDelta, subKey, SubDelta);
        }

        // ----- Local networked counters (authority only, if you use them) -----
        if (Object && Object.HasStateAuthority)
        {
            TriggerCount += 1;
        }
    }

    // InteractableReporter.cs
    private void SetThresholdFiredSafe(bool v)
    {
        // Runner is non-null only after Spawned(); Object.IsValid is also safe
        if (Runner != null && Object != null && Object.IsValid)
        {
            ThresholdFired = v;
        }
    }
    private void EvaluateThreshold()
    {
        _localTouchCount += 1;
        if (_localTouchCount >= Mathf.Max(1, TriggerThreshold))
        {
            if (!_localFired || !FireOnce)
            {
                _localFired = true;
                SetThresholdFiredSafe(true);   // <-- was: ThresholdFired = true;
                OnThresholdReached?.Invoke();
            }
        }
    }

    /// <summary>Gets the last player who interacted with this object.</summary>
    public bool TryGetLastInteractor(out PlayerRef interactor)
    {
        interactor = _lastInteractor;
        return _lastInteractor != PlayerRef.None;
    }

    /// <summary>Tries to find the last interactor's NetworkObject in the scene.</summary>
    public bool TryGetLastInteractor(out PlayerRef interactor, out NetworkObject interactorNO)
    {
        interactor = _lastInteractor;
        interactorNO = GetLastInteractorNetworkObject();
        return interactor != PlayerRef.None && interactorNO != null;
    }

    public NetworkObject GetLastInteractorNetworkObject()
    {
        if (Runner == null || _lastInteractor == PlayerRef.None) return null;

        foreach (var no in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            if (no && no.InputAuthority == _lastInteractor) return no;

        return null;
    }


    private void OnTriggerExit(Collider other)
    {
        if (!IsHand(other)) return;

        if (autoRearmOnExit)
        {
            _localTouchCount = 0;
            _localFired = false;
            SetThresholdFiredSafe(false);      // <-- was: ThresholdFired = false;
        }
    }
}
