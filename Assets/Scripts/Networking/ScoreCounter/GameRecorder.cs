using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public enum Category
{
    Nature,
    Tech,
    Art,
    Box,
    // add yours…
}



[Serializable]
public struct CategoryThreshold
{
    public Category Category;
    public int Threshold;
    public SceneRef SceneToLoad;
    public string Label; // optional, for logs
}




[Serializable]
public struct RecordedAction
{
    public int PlayerNumber;     // 1 or 2
    public string ObjectName;
    public Category MainCategory;
    public int MainDelta;
    public string SubCategoryKey; // e.g. "Blocks" or "ID_3" or ""
    public int SubDelta;
    public double ServerTime;

    public override string ToString()
    {
        string sub = string.IsNullOrEmpty(SubCategoryKey) ? "" : $" | Sub:{SubCategoryKey} ({SubDelta:+#;-#;0})";
        return $"P{PlayerNumber} [{ObjectName}] Main:{MainCategory} ({MainDelta:+#;-#;0}){sub} @ {ServerTime:0.00}s";
    }
}

public class GameRecorder : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static GameRecorder Instance { get; private set; }

    [Header("Config")]
    public List<CategoryThreshold> CategoryScenes = new List<CategoryThreshold>();
    public bool DebugLogEvents = true;

    // === CHOICES (named flags/counters like A..H) ===
    [Header("Choices")]
    [Tooltip("Optional catalogue of choice keys for inspector reference (not required).")]
    public List<string> ChoiceKeys = new List<string> { "A", "B", "C", "D", "E", "F", "G", "H" };

    // Local mirrors (authoritative on StateAuthority, synced via RPCs like main/sub)
    private readonly Dictionary<string, int> _choiceCounts = new Dictionary<string, int>();

    public IReadOnlyDictionary<string, int> ChoiceCounts => _choiceCounts;

    // Event log (local list mirrored via RPC)
    public readonly List<RecordedAction> EventLog = new List<RecordedAction>();

    // Authoritative totals
    private readonly Dictionary<Category, int> _mainScores = new Dictionary<Category, int>();
    private readonly Dictionary<string, int> _subScores = new Dictionary<string, int>(); // dynamic; no predef needed

    public IReadOnlyDictionary<Category, int> MainScores { get { return _mainScores; } }
    public IReadOnlyDictionary<string, int> SubScores { get { return _subScores; } }

    public override void Spawned()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GameRecorder instances found. Keeping the first.");
            return;
        }
        Instance = this;

        foreach (Category c in Enum.GetValues(typeof(Category)))
            if (!_mainScores.ContainsKey(c)) _mainScores[c] = 0;

        if (Object.HasStateAuthority && DebugLogEvents)
            Debug.Log("[Recorder] StateAuthority ready.");
    }

    // === REPORTING ===
    // Simple (main only) – kept for compatibility
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ReportInteraction(PlayerRef reporter, string objectName, Category mainCategory, int mainDelta)
    {
        RPC_ReportInteractionEx(reporter, objectName, mainCategory, mainDelta, "", 0);
    }





    // Report a choice selection (count-based; delta can be +1/-1)
    // If you only care "picked or not", treat >0 as picked.
    // GameRecorder.cs
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RecordChoice(PlayerRef reporter, string choiceKey, int delta)
    {
        if (!Object.HasStateAuthority) return;
        if (string.IsNullOrWhiteSpace(choiceKey)) return;
        choiceKey = choiceKey.Trim();
        int cur = _choiceCounts.TryGetValue(choiceKey, out var v) ? v : 0;
        int next = cur + delta;
        _choiceCounts[choiceKey] = next;
        RPC_SyncChoice(choiceKey, next);
        if (DebugLogEvents)
            Debug.Log($"[Recorder] Choice '{choiceKey}' {(delta >= 0 ? "+" : "")}{delta} -> {next} (P{PlayerNumberFromRef(reporter)}) @ {Runner.SimulationTime:0.00}s");
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncChoice(string choiceKey, int value)
    {
        if (string.IsNullOrWhiteSpace(choiceKey)) return;
        _choiceCounts[choiceKey.Trim()] = value;
    }

    // --------- Query helpers used by SetManager ---------
    public bool Has(string key) => _choiceCounts.TryGetValue(key, out var v) && v > 0;

    public bool HasAny(IEnumerable<string> keys)
    {
        if (keys == null) return false;
        foreach (var k in keys) if (Has(k)) return true;
        return false;
    }
    public bool HasAny(params string[] keys) => HasAny((IEnumerable<string>)keys);

    public bool HasAll(IEnumerable<string> keys)
    {
        if (keys == null) return true;
        foreach (var k in keys) if (!Has(k)) return false;
        return true;
    }

    public void DumpChoices()
    {
        foreach (var kv in _choiceCounts)
            Debug.Log($"[Recorder] CHOICE '{kv.Key}' = {kv.Value}");
    }
    public bool HasAll(params string[] keys) => HasAll((IEnumerable<string>)keys);

    // Extended (main + sub)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ReportInteractionEx(PlayerRef reporter, string objectName, Category mainCategory, int mainDelta, string subCategoryKey, int subDelta)
    {
        if (!Object.HasStateAuthority) return;

        int playerNum = PlayerNumberFromRef(reporter);
        if (playerNum == 0) playerNum = GuessPlayerNumber(reporter);

        // Update main
        _mainScores[mainCategory] = _mainScores.ContainsKey(mainCategory) ? _mainScores[mainCategory] + mainDelta : mainDelta;

        // Update sub if provided (non-empty)
        if (!string.IsNullOrEmpty(subCategoryKey))
            _subScores[subCategoryKey] = _subScores.ContainsKey(subCategoryKey) ? _subScores[subCategoryKey] + subDelta : subDelta;

        double t = Runner.SimulationTime;

        // Broadcast event + new values
        RPC_BroadcastEvent(playerNum, objectName, mainCategory, mainDelta, subCategoryKey, subDelta, t);
        RPC_SyncSingleMain(mainCategory, _mainScores[mainCategory]);
        if (!string.IsNullOrEmpty(subCategoryKey))
            RPC_SyncSingleSub(subCategoryKey, _subScores[subCategoryKey]);

        if (DebugLogEvents)
        {
            string subTxt = string.IsNullOrEmpty(subCategoryKey) ? "" : $" | Sub '{subCategoryKey}' -> total {_subScores[subCategoryKey]}";
            Debug.Log($"[Recorder] Main [{mainCategory}] += {mainDelta} -> total {_mainScores[mainCategory]} (P{playerNum}, {objectName}){subTxt}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastEvent(int playerNumber, string objectName, Category mainCategory, int mainDelta, string subCategoryKey, int subDelta, double serverTime)
    {
        var ra = new RecordedAction
        {
            PlayerNumber = playerNumber,
            ObjectName = objectName,
            MainCategory = mainCategory,
            MainDelta = mainDelta,
            SubCategoryKey = subCategoryKey,
            SubDelta = subDelta,
            ServerTime = serverTime
        };
        EventLog.Add(ra);
        if (DebugLogEvents) Debug.Log($"[Event] {ra}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncSingleMain(Category category, int value) => _mainScores[category] = value;

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncSingleSub(string subKey, int value) { if (!string.IsNullOrEmpty(subKey)) _subScores[subKey] = value; }

    // === PLAYER LIFECYCLE ===
    public void PlayerJoined(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        // Create copies to avoid modification during iteration
        var mainScoresCopy = new Dictionary<Category, int>(_mainScores);
        foreach (var kvp in mainScoresCopy)
            RPC_SyncSingleMain(kvp.Key, kvp.Value);

        var subScoresCopy = new Dictionary<string, int>(_subScores);
        foreach (var kvp in subScoresCopy)
            RPC_SyncSingleSub(kvp.Key, kvp.Value);

        if (DebugLogEvents) Debug.Log($"[Recorder] Player joined: {player}");
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (DebugLogEvents) Debug.Log($"[Recorder] Player left: {player}");
    }

    // === SCENE ROUTING (unchanged) ===
    public void EvaluateAndRouteScenes()
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[Recorder] Only StateAuthority should route scenes.");
            return;
        }

        var scenesToLoad = new List<SceneRef>();
        foreach (var map in CategoryScenes)
        {
            int score = _mainScores.ContainsKey(map.Category) ? _mainScores[map.Category] : 0;
            if (score >= map.Threshold) scenesToLoad.Add(map.SceneToLoad);
        }

        if (scenesToLoad.Count == 0)
        {
            if (DebugLogEvents) Debug.Log("[Recorder] No thresholds met. Staying.");
            return;
        }

        var chosen = scenesToLoad[0];
        string label = "[unlabeled]";
        foreach (var map in CategoryScenes)
            if (map.SceneToLoad.Equals(chosen)) { label = string.IsNullOrEmpty(map.Label) ? chosen.ToString() : map.Label; break; }

        if (DebugLogEvents) Debug.Log($"[Recorder] Loading scene: {label}");
        Runner.LoadScene(chosen);
    }

    public Dictionary<Category, int> GetMainScoresSnapshot() => new Dictionary<Category, int>(_mainScores);
    public Dictionary<string, int> GetSubScoresSnapshot() => new Dictionary<string, int>(_subScores);

    private int PlayerNumberFromRef(PlayerRef r)
    {
        int idx = 0;
        foreach (var p in Runner.ActivePlayers) { idx++; if (p == r) return idx; }
        return 0;
    }
    private int GuessPlayerNumber(PlayerRef r) => (r.RawEncoded % 2 == 0) ? 1 : 2;
}
