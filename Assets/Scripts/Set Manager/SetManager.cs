using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A timeline-free, one-shot mini-scene orchestrator:
/// - (Optional) Teleport players into this set when it begins
/// - Fire OnSetBegin (enable/disable, run functions)
/// - (Optional) Evaluate choice rules/branches once and fire per-choice events
/// - Wait for either: EndSet() called by anything (timeline signal, trigger, code), or a real-time fallback
/// - On end: fire OnSetEnd, enable next SetManager, (optionally) disable self
/// </summary>
public class SetManager : NetworkBehaviour
{
    // ---------------- Lifecycle ----------------
    [Header("Lifecycle")]
    [SerializeField] private bool runOnStateAuthorityOnly = true;
    [SerializeField] private bool autoBeginOnEnable = true;
    [SerializeField] private bool disableSelfOnEnd = true;
    [SerializeField] private SetManager nextSet;
    [SerializeField] private SetManager[] setSequence;

    // ---------------- Teleport ----------------
    [Header("Teleport (on set begin)")]
    [SerializeField] private DelayedTeleporter teleporter;
    [SerializeField] private bool teleportEnabled = true;
    [SerializeField] private float teleportDelaySeconds = 0f;

    [SerializeField] private bool teleportChar1 = true;
    [SerializeField] private bool teleportChar2 = true;
    [SerializeField] private bool teleportSpectators = true;
    [Tooltip("If non-empty, only these spectator ordinals (0=first,1=second,...) are teleported.")]
    [SerializeField] private List<int> spectatorSlotsToTeleport = new List<int>();

    [Tooltip("Entry destinations for THIS set.")]
    [SerializeField] private Transform destChar1;
    [SerializeField] private Transform destChar2;
    [Tooltip("Spectator entry destinations in order: [0]=1st spectator, [1]=2nd, ...")]
    [SerializeField] private Transform[] destChar3Ordered;

    // ---------------- Choice evaluation ----------------
    [Header("Choice evaluation (one-shot at begin)")]
    [SerializeField] private bool evaluateChoicesOnBegin = false;
    [Tooltip("If true, stop after the first matching rule/branch.")]
    [SerializeField] private bool firstMatchOnly = false;

    [Serializable]

    // When to teleport relative to choice evaluation
    public enum TeleportWhen { Disabled, OnBeginBeforeChoice, OnBeginAfterChoice }

    [Header("Teleport timing")]
    [SerializeField] private TeleportWhen teleportWhen = TeleportWhen.OnBeginAfterChoice;

    // Optional per-branch/per-rule teleport override
    [Serializable]
    public class TeleportOverride
    {
        [Tooltip("Tick to use this override when the rule/branch matches.")]
        public bool useOverride = false;

        [Tooltip("Override for Character 1 (leave null to fall back to default destChar1).")]
        public Transform destChar1;

        [Tooltip("Override for Character 2 (leave null to fall back to default destChar2).")]
        public Transform destChar2;

        [Tooltip("Override for spectators. If null or shorter, missing slots fall back to defaults.")]
        public Transform[] destChar3Ordered;
    }

    [Serializable]
    public class BeginEndOverride
    {
        [Tooltip("Tick to use this override when the rule/branch matches.")]
        public bool useOverride = false;

        [Header("Begin (replaces defaults if used)")]
        public List<GameObject> enableAtBegin = new List<GameObject>();
        public UnityEvent OnSetBegin;

        [Header("End (replaces defaults if used)")]
        public List<GameObject> disableAtEnd = new List<GameObject>();
        public UnityEvent OnSetEnd;

        [Header("Flow (replaces defaults if used)")]
        public SetManager nextSetOverride;

        [Tooltip("If ticked, this value replaces 'disableSelfOnEnd' for this run.")]
        public bool overrideDisableSelfOnEnd = false;
        public bool disableSelfOnEnd = true;
    }

    [Tooltip("General rules (AND/OR). Use these for combos like [A,C,E] etc.")]
    [SerializeField] private List<ChoiceRule> comboRules = new List<ChoiceRule>();

    [Serializable]
    public class ChoiceRule
    {
        public List<string> requireAll = new List<string>();
        public List<string> requireAny = new List<string>();
        public UnityEvent OnMatched;

        [Header("Teleport override (optional)")]
        public TeleportOverride teleportOverride;

        [Header("Begin/End override (optional)")]
        public BeginEndOverride beginEndOverride;
    }

    [Tooltip("Switch-style branches: A→eventA, B→eventB, etc.")]
    [SerializeField] private List<ChoiceBranch> singleChoiceBranches = new List<ChoiceBranch>();

    [Serializable]
    public class ChoiceBranch
    {
        [Tooltip("A single key to branch on (e.g., \"A\").")]
        public string key;
        public UnityEvent OnKeyChosen;

        [Header("Teleport override (optional)")]
        public TeleportOverride teleportOverride;

        [Header("Begin/End override (optional)")]
        public BeginEndOverride beginEndOverride;
    }



    [Header("Choice evaluation order")]
    [SerializeField] private bool evaluateBeforeBegin = false;


    // ---------------- Begin / End hooks ----------------
    [Header("Begin/End hooks")]
    [Tooltip("Enable these at begin (before events).")]
    [SerializeField] private List<GameObject> enableAtBegin = new List<GameObject>();
    [Tooltip("Disable these at end (before advancing).")]
    [SerializeField] private List<GameObject> disableAtEnd = new List<GameObject>();

    public UnityEvent OnSetBegin;   // run your functions here
    public UnityEvent OnSetEnd;     // run your functions here
    public UnityEvent OnAnyChoiceMatched; // fires if any rule/branch matched
    public UnityEvent OnTimeout;    // fires if fallback triggers end

    // ---------------- Fallback ----------------
    [Header("Fallback end (real-time)")]
    [SerializeField] private bool enableFallback = true;
    [SerializeField] private float fallbackSeconds = 0f; // 0 = disabled

    // ---------------- Internals ----------------
    private bool _begun;
    private bool _ended;
    private double _deadlineRT;
    private NetworkRunner RunnerOrNull => LobbyManager.Instance ? LobbyManager.Instance.Runner : null;

    private void OnEnable()
    {
        if (autoBeginOnEnable) TryBegin();
    }

    public void TryBegin()
    {
        if (_begun) return;

        if (runOnStateAuthorityOnly)
        {
            var ok = Object && Object.HasStateAuthority;
            if (!ok) return;
        }

        _begun = true;
        BeginSet();
    }

    // Effective begin/end/flow picked from a matching rule/branch (null = use defaults)
    private BeginEndOverride _effBE;
    private void BeginSet()
    {

        Debug.Log($"[SetManager] BeginSet on {(RunnerOrNull ? RunnerOrNull.name : "no-runner")} SA={Object && Object.HasStateAuthority}");

        // Decide overrides first if requested
        TeleportOverride matchedTele = null;
        _effBE = null;

        if (evaluateChoicesOnBegin && evaluateBeforeBegin)
        {
            (matchedTele, _effBE) = EvaluateChoicesOnce_CollectEffects();
        }

        // Teleport BEFORE choices (defaults) – unchanged behaviour
        if (teleportWhen == TeleportWhen.OnBeginBeforeChoice)
            TeleportPlayersByRole(null);

        // BEGIN enables (override replaces defaults if present)
        var beginList = (_effBE != null && _effBE.useOverride && _effBE.enableAtBegin != null && _effBE.enableAtBegin.Count > 0)
            ? _effBE.enableAtBegin
            : enableAtBegin;
        foreach (var go in beginList) if (go) go.SetActive(true);

        // BEGIN functions (override replaces default if present)
        if (_effBE != null && _effBE.useOverride && _effBE.OnSetBegin != null)
            _effBE.OnSetBegin.Invoke();
        else
            OnSetBegin?.Invoke();

        // If we didn't evaluate up front, do it now (original default timing)
        if (evaluateChoicesOnBegin && !evaluateBeforeBegin)
        {
            (matchedTele, _effBE) = EvaluateChoicesOnce_CollectEffects();
        }

        // Teleport AFTER choices – now we can apply per-choice teleport override
        if (teleportWhen == TeleportWhen.OnBeginAfterChoice)
            TeleportPlayersByRole(matchedTele);

        // Arm fallback
        if (enableFallback && fallbackSeconds > 0f)
            StartCoroutine(FallbackCoroutine());
    }

    public void OnGameStartTriggered()
    {
        if (Object && Object.HasStateAuthority)
            RPC_BeginSetIndex(0);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_BeginSetIndex(int index)
    {
        if (setSequence == null || index < 0 || index >= setSequence.Length) return;
        var set = setSequence[index];
        if (!set) return;

        if (!set.gameObject.activeSelf) set.gameObject.SetActive(true);
        set.TryBegin(); // public wrapper around the private BeginSet()
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TeleportOwned(PlayerRef who, Vector3 pos, Quaternion rot, float delay)
    {
        var r = RunnerOrNull;
        if (r != null && r.TryGetPlayerObject(who, out var po) && po && po.HasStateAuthority)
        {
            if (teleporter) teleporter.RequestTeleport(po, delay, pos, rot);
            else po.transform.SetPositionAndRotation(pos, rot);
        }
    }

    // SetManager.cs (where you previously teleported locally)
    /*private void TeleportAllByRoleViaLobby()
    {
        var lm = LobbyManager.Instance;
        if (!(lm && lm.Object && lm.Object.HasStateAuthority)) return; // only LM owner sends

        var r = lm.Runner;
        foreach (var p in r.ActivePlayers)
        {
            var role = lm.GetPlayerRole(p);      // your existing role resolver
            var dst = RoleToDest(role);         // map role → destination Transform (Set 1’s points)
            if (!dst) continue;
            lm.RPC_TeleportToPose(p, dst.position, dst.rotation);
        }
    }*/

    private (TeleportOverride tele, BeginEndOverride be) EvaluateChoicesOnce_CollectEffects()
    {
        var rec = GameRecorder.Instance;
        if (rec == null) return (null, null);

        TeleportOverride selectedTele = null;
        BeginEndOverride selectedBE = null;
        bool any = false;

        // --- helper local funcs (trim-aware) ---
        bool Has(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return rec.Has(raw.Trim());
        }
        bool HasAll(IEnumerable<string> keys)
        {
            if (keys == null) return true;
            foreach (var s in keys) if (!Has(s)) return false;
            return true;
        }
        bool HasAny(IEnumerable<string> keys)
        {
            if (keys == null) return true;
            foreach (var s in keys) if (Has(s)) return true;
            return false;
        }

        // ----- Single-key branches -----
        foreach (var b in singleChoiceBranches)
        {
            if (string.IsNullOrWhiteSpace(b.key)) continue;
            var k = b.key.Trim();
            bool has = Has(k);

            Debug.Log($"[Set:{name}] Single-branch key='{k}' -> Has={has}");

            if (!has) continue;

            any = true;
            b.OnKeyChosen?.Invoke();

            if (b.teleportOverride != null && b.teleportOverride.useOverride)
                selectedTele = b.teleportOverride;

            if (b.beginEndOverride != null && b.beginEndOverride.useOverride)
                selectedBE = b.beginEndOverride;

            if (firstMatchOnly) { if (any) OnAnyChoiceMatched?.Invoke(); return (selectedTele, selectedBE); }
        }

        // ----- Combo rules (AND/OR via requireAll / requireAny) -----
        foreach (var r in comboRules)
        {
            // Both conditions must be satisfied:
            // - requireAll: either empty/null OR all present
            // - requireAny: either empty/null OR at least one present
            bool okAll = (r.requireAll == null || r.requireAll.Count == 0) || HasAll(r.requireAll);
            bool okAny = (r.requireAny == null || r.requireAny.Count == 0) || HasAny(r.requireAny);

            Debug.Log($"[Set:{name}] Combo rule AND({string.Join(", ", r.requireAll ?? new List<string>())}) " +
                      $"OR({string.Join(", ", r.requireAny ?? new List<string>())}) -> All={okAll} Any={okAny}");

            if (!(okAll && okAny)) continue;

            any = true;
            r.OnMatched?.Invoke();

            if (r.teleportOverride != null && r.teleportOverride.useOverride)
                selectedTele = r.teleportOverride;

            if (r.beginEndOverride != null && r.beginEndOverride.useOverride)
                selectedBE = r.beginEndOverride;

            if (firstMatchOnly) { if (any) OnAnyChoiceMatched?.Invoke(); return (selectedTele, selectedBE); }
        }

        if (any) OnAnyChoiceMatched?.Invoke();
        return (selectedTele, selectedBE); // may be (null, null)
    }


    private IEnumerator FallbackCoroutine()
    {
        // Use real-time so it isn’t affected by FPS or timeScale
        yield return new WaitForSecondsRealtime(fallbackSeconds);
        if (!_ended)
        {
            OnTimeout?.Invoke();
            EndSet(); // same as Advance
        }
    }
    private void TeleportPlayersByRole(TeleportOverride ov)
    {
        if (!teleporter) teleporter = FindFirstObjectByType<DelayedTeleporter>();

        if (teleportWhen == TeleportWhen.Disabled) return;           // hard gate
        if (!teleporter || RunnerOrNull == null || LobbyManager.Instance == null) return;

        // Resolve effective destinations = override ?? defaults (per slot)
        Transform effC1 = ov != null && ov.useOverride && ov.destChar1 ? ov.destChar1 : destChar1;
        Transform effC2 = ov != null && ov.useOverride && ov.destChar2 ? ov.destChar2 : destChar2;
        Transform[] effC3 = (ov != null && ov.useOverride && ov.destChar3Ordered != null && ov.destChar3Ordered.Length > 0)
            ? ov.destChar3Ordered
            : destChar3Ordered;

        // Char1 / Char2
        foreach (var p in RunnerOrNull.ActivePlayers)
        {
            var role = LobbyManager.Instance.GetPlayerRole(p);
            if (role == CharacterRole.Character1 && teleportChar1 && effC1)
                Tele(p, effC1);
            else if (role == CharacterRole.Character2 && teleportChar2 && effC2)
                Tele(p, effC2);
        }

        // Spectators
        if (teleportSpectators && effC3 != null)
        {
            var spectators = LobbyManager.Instance.GetSpectatorsOrdered();
            for (int i = 0; i < spectators.Count; i++)
            {
                if (spectatorSlotsToTeleport != null && spectatorSlotsToTeleport.Count > 0 &&
                    !spectatorSlotsToTeleport.Contains(i))
                    continue;

                if (i >= effC3.Length) continue;
                var dst = effC3[i];
                if (dst) Tele(spectators[i], dst);
            }
        }
        // SetManager.cs  -> inside TeleportPlayersByRole(...)
        void Tele(PlayerRef who, Transform dst)
        {
            if (!dst) return;
            if (RunnerOrNull != null &&
                RunnerOrNull.TryGetPlayerObject(who, out var po) &&
                po && po.HasStateAuthority)
            {
                if (!teleporter) teleporter = FindFirstObjectByType<DelayedTeleporter>();
                if (teleporter) teleporter.RequestTeleport(po, teleportDelaySeconds, dst);
                else po.transform.SetPositionAndRotation(dst.position, dst.rotation);
            }
        }


    }
    private void EvaluateChoicesOnce()
    {
        var rec = GameRecorder.Instance;
        if (!rec) return;

        bool any = false;

        // Switch-like single key branches
        foreach (var b in singleChoiceBranches)
        {
            if (!string.IsNullOrWhiteSpace(b.key) && rec.Has(b.key))
            {
                any = true;
                b.OnKeyChosen?.Invoke();
                if (firstMatchOnly) { OnAnyChoiceMatched?.Invoke(); return; }
            }
        }

        // General AND/OR combo rules
        foreach (var r in comboRules)
        {
            bool okAll = r.requireAll == null || r.requireAll.Count == 0 || rec.HasAll(r.requireAll);
            bool okAny = r.requireAny == null || r.requireAny.Count == 0 || rec.HasAny(r.requireAny);
            if (okAll && okAny)
            {
                any = true;
                r.OnMatched?.Invoke();
                if (firstMatchOnly) { OnAnyChoiceMatched?.Invoke(); return; }
            }
        }

        if (any) OnAnyChoiceMatched?.Invoke();
    }

    // Trim-aware test against GameRecorder
    private static bool RecHas(GameRecorder rec, string keyRaw)
    {
        if (rec == null) return false;
        if (string.IsNullOrWhiteSpace(keyRaw)) return false;
        var k = keyRaw.Trim();
        return rec.Has(k);
    }

    // ========== Public API ==========
    /// <summary>Call this from Timeline signals, triggers, or code to finish this set now.</summary>
    public void EndSet() => Advance();

    /// <summary>Manual re-check of choices later if you need it.</summary>
    public void EvaluateChoicesNow() => EvaluateChoicesOnce();

    private void Advance()
    {
        if (_ended) return;
        _ended = true;

        StopAllCoroutines();


        // END disables (override replaces defaults if present)
        var endList = (_effBE != null && _effBE.useOverride && _effBE.disableAtEnd != null && _effBE.disableAtEnd.Count > 0)
            ? _effBE.disableAtEnd
            : disableAtEnd;
        foreach (var go in endList) if (go) go.SetActive(false);

        // END functions (override replaces default if present)
        if (_effBE != null && _effBE.useOverride && _effBE.OnSetEnd != null)
            _effBE.OnSetEnd.Invoke();
        else
            OnSetEnd?.Invoke();

        // Re-check once at end so late choices are honored
        var effects = EvaluateChoicesOnce_CollectEffects();
        _effBE = effects.be;   // we only need the BE override to choose the next set

        // NEXT SET (override replaces default if present)
        var targetNext = (_effBE != null && _effBE.useOverride && _effBE.nextSetOverride)
            ? _effBE.nextSetOverride
            : nextSet;

        // (Optional) helpful debug
        Debug.Log($"[Set:{name}] Advance → next = {(targetNext ? targetNext.name : "<none>")} " +
              $"(override={(_effBE != null && _effBE.useOverride ? (_effBE.nextSetOverride ? _effBE.nextSetOverride.name : "<null>") : "<no override>")})");



        if (targetNext)
        {
            if (!targetNext.gameObject.activeSelf) targetNext.gameObject.SetActive(true);
            // If you rely on autoBeginOnEnable, you can stop here.
            // If you want to be explicit, also force begin locally on every peer:
            // targetNext.TryBegin(true);
        }

        // Disable self (override replaces default if flagged)
        bool disableSelf =
            (_effBE != null && _effBE.useOverride && _effBE.overrideDisableSelfOnEnd)
                ? _effBE.disableSelfOnEnd
                : disableSelfOnEnd;

        if (disableSelf) gameObject.SetActive(false);

    }
}
