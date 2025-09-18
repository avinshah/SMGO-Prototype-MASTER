using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Fusion;
using UnityEngine;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif
using UnityEngine.UI;

/// <summary>
/// Shows player mappings AND mirrors all Unity console logs (Log/Warning/Error/Exception)
/// into a VR HUD text overlay. Designed for local client HUD.
/// </summary>
public class PlayerMapDebugOverlayDriver : MonoBehaviour
{
    [Header("Target (assign your HUD text)")]
#if TMP_PRESENT || UNITY_TEXTMESHPRO
    [SerializeField] private TMP_Text tmpText;
#endif
    [SerializeField] private Text uiText;

    [Tooltip("If not assigned, try to auto-find a text component under the local VR rig whose name contains this token.")]
    [SerializeField] private string autoFindNameContains = "DebugOverlay";

    [Tooltip("Search under the local VRRigMarker only (recommended).")]
    [SerializeField] private bool autoFindUnderLocalRig = true;

    [Header("Console capture")]
    [SerializeField] private bool captureUnityLogs = true;
    [Tooltip("Include stack trace for errors/exceptions in the HUD.")]
    [SerializeField] private bool includeStackTraceForErrors = true;

    [Header("Filtering")]
    [SerializeField] private bool showLogs = true;
    [SerializeField] private bool showWarnings = true;
    [SerializeField] private bool showErrors = true;

    [Header("Output shaping")]
    [Tooltip("Max lines to keep in the live HUD buffer (older lines are dropped).")]
    [SerializeField] private int maxLines = 200;
    [Tooltip("How often (seconds) to redraw the HUD text.")]
    [SerializeField] private float updateInterval = 0.1f;
    [Tooltip("Prefix each line with a timestamp like 20:17:45.123")]
    [SerializeField] private bool prefixTimestamps = true;

    [Header("Sections")]
    [Tooltip("Show the player mapping header at the top.")]
    [SerializeField] private bool showPlayerMappings = true;

    // --- internals ---
    private readonly Queue<string> _lines = new Queue<string>(256);
    private readonly object _lock = new object();
    private float _nextFlushTime;
    private int _mainThreadId;

    private void Awake()
    {
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        TryAutoFindOverlayTarget();
    }

    private void OnEnable()
    {
        if (captureUnityLogs)
        {
            Application.logMessageReceivedThreaded += OnLog;
        }
    }

    private void OnDisable()
    {
        if (captureUnityLogs)
        {
            Application.logMessageReceivedThreaded -= OnLog;
        }
    }

    private void TryAutoFindOverlayTarget()
    {
        bool HasTextAssigned() =>
#if TMP_PRESENT || UNITY_TEXTMESHPRO
            (tmpText != null) ||
#endif
            (uiText != null);

        if (HasTextAssigned()) return;

        Transform root = null;
        if (autoFindUnderLocalRig && VRRigMarker.Local)
            root = VRRigMarker.Local.transform;
        else
            root = null; // search whole scene

#if TMP_PRESENT || UNITY_TEXTMESHPRO
        var tmps = (root ? root.GetComponentsInChildren<TMP_Text>(true) : FindObjectsOfType<TMP_Text>(true));
        foreach (var t in tmps)
            if (t && t.name.IndexOf(autoFindNameContains, StringComparison.OrdinalIgnoreCase) >= 0) { tmpText = t; break; }
        if (tmpText) return;
#endif
        var texts = (root ? root.GetComponentsInChildren<Text>(true) : FindObjectsOfType<Text>(true));
        foreach (var t in texts)
            if (t && t.name.IndexOf(autoFindNameContains, StringComparison.OrdinalIgnoreCase) >= 0) { uiText = t; break; }
    }

    // >>> IMPORTANT: Explicitly use UnityEngine.LogType so it matches Application.LogCallback
    private void OnLog(string condition, string stackTrace, UnityEngine.LogType type)
    {
        // Filter
        if (type == UnityEngine.LogType.Log && !showLogs) return;
        if (type == UnityEngine.LogType.Warning && !showWarnings) return;
        if ((type == UnityEngine.LogType.Error || type == UnityEngine.LogType.Exception || type == UnityEngine.LogType.Assert) && !showErrors) return;

        var sb = new StringBuilder(256);
        if (prefixTimestamps)
        {
            var now = DateTime.Now;
            sb.Append(now.ToString("HH:mm:ss.fff")).Append(' ');
        }

        // Type prefix
        switch (type)
        {
            case UnityEngine.LogType.Warning: sb.Append("[W] "); break;
            case UnityEngine.LogType.Error:
            case UnityEngine.LogType.Exception:
            case UnityEngine.LogType.Assert: sb.Append("[E] "); break;
            default: sb.Append("[L] "); break;
        }

        sb.Append(condition);

        // Stack for errors/exceptions (optional)
        if (includeStackTraceForErrors && (type == UnityEngine.LogType.Error || type == UnityEngine.LogType.Exception || type == UnityEngine.LogType.Assert))
        {
            if (!string.IsNullOrWhiteSpace(stackTrace))
            {
                var collapsed = stackTrace.Replace('\n', ' ').Replace("\r", "");
                sb.Append("  ").Append(collapsed);
            }
        }

        lock (_lock)
        {
            _lines.Enqueue(sb.ToString());
            while (_lines.Count > maxLines) _lines.Dequeue();
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextFlushTime) return;
        _nextFlushTime = Time.unscaledTime + Mathf.Max(0.02f, updateInterval);

        var sb = new StringBuilder(8192);

        if (showPlayerMappings)
        {
            AppendPlayerMappings(sb);
            sb.AppendLine("────────────────────────────────");
        }

        lock (_lock)
        {
            foreach (var line in _lines)
                sb.AppendLine(line);
        }

        SetOverlayText(sb.ToString());
    }

    private void AppendPlayerMappings(StringBuilder sb)
    {
        var lm = LobbyManager.Instance;
        var runner = lm ? lm.Runner : null;

        sb.AppendLine("[Player Map]");
        if (runner == null)
        {
            sb.AppendLine("  (Runner not ready)");
            return;
        }

        foreach (var p in runner.ActivePlayers)
        {
            var role = lm.GetPlayerRole(p);
            var ready = lm.IsPlayerReady(p);
            int spectatorSlot = lm.GetSpectatorOrdinal(p); // -1 if not spectator

            sb.Append("  P").Append(p.PlayerId)
              .Append(p == runner.LocalPlayer ? " (Local)" : "")
              .Append("  Role: ").Append(role)
              .Append("  Ready: ").Append(ready ? "Y" : "N");

            if (spectatorSlot >= 0) sb.Append("  SpectatorSlot: ").Append(spectatorSlot);

            sb.AppendLine();
        }
    }

    private void SetOverlayText(string text)
    {
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (tmpText) { tmpText.text = text; return; }
#endif
        if (uiText) { uiText.text = text; return; }
    }

    // -------- Optional helpers you can call from UnityEvents or code --------

    /// <summary>Enable/disable capturing at runtime (handy to wire from SetManager).</summary>
    public void SetCaptureEnabled(bool on)
    {
        if (on && !captureUnityLogs)
        {
            captureUnityLogs = true;
            Application.logMessageReceivedThreaded += OnLog;
        }
        else if (!on && captureUnityLogs)
        {
            captureUnityLogs = false;
            Application.logMessageReceivedThreaded -= OnLog;
        }
    }

    /// <summary>Clear the scrolling log buffer.</summary>
    public void ClearLogs()
    {
        lock (_lock) _lines.Clear();
    }

    /// <summary>Push an app-specific line (bypasses Unity console).</summary>
    public static void Push(string message, UnityEngine.LogType type = UnityEngine.LogType.Log)
    {
        var inst = FindObjectOfType<PlayerMapDebugOverlayDriver>();
        if (inst == null) return;
        inst.OnLog(message, "", type);
    }
}
