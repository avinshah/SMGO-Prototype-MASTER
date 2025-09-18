using System.Collections.Generic;
using UnityEngine;
using Fusion;

/// <summary>
/// Finds the "active" NetworkRunner in scenes that may have multiple runners
/// (e.g., FusionBootstrap client, temp runners, disabled BuildingBlocks runner).
/// Prefers: enabled + IsRunning, then enabled, then anything. Among candidates,
/// prefers IsServer, then more ActivePlayers, then ProvideInput=true.
/// </summary>
public static class RunnerLocator
{
    public static NetworkRunner GetActiveRunner()
    {
#if UNITY_2023_1_OR_NEWER
    var all = Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<NetworkRunner>();
#endif
        if (all == null || all.Length == 0) return null;

        var enabledRunning = new List<NetworkRunner>();
        var enabledOnly = new List<NetworkRunner>();

        foreach (var r in all)
        {
            if (r == null) continue;
            if (r.enabled && r.isActiveAndEnabled)
            {
                if (r.IsRunning) enabledRunning.Add(r);
                else enabledOnly.Add(r);
            }
        }

        // Prefer enabled+running; else enabled; else first available (may be starting up)
        if (enabledRunning.Count > 0) return Rank(enabledRunning);
        if (enabledOnly.Count > 0) return Rank(enabledOnly);
        return Rank(all);
    }

    static NetworkRunner Rank(IList<NetworkRunner> list)
    {
        NetworkRunner best = null;
        int bestScore = int.MinValue;

        foreach (var r in list)
        {
            if (r == null) continue;
            int players = 0; foreach (var _ in r.ActivePlayers) players++;

            // Score: prefer IsServer, then more players, then ProvideInput=true.
            int score = 0;
            if (r.IsRunning) score += 1000;
            if (r.IsServer) score += 500;
            score += Mathf.Clamp(players, 0, 100);
            if (r.ProvideInput) score += 10;

            // Mild penalty for obvious helper/disabled objects by name
            var n = r.name ?? "";
            if (n.IndexOf("BuildingBlock", System.StringComparison.OrdinalIgnoreCase) >= 0) score -= 50;
            if (n.IndexOf("Temporary", System.StringComparison.OrdinalIgnoreCase) >= 0) score -= 10;

            if (score > bestScore) { bestScore = score; best = r; }
        }

        return best;
    }
}
