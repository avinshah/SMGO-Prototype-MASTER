using Fusion;
using UnityEngine;
using System.Text;

public class RunnerHUDDriver : MonoBehaviour
{
    public float updateHz = 4f; // refresh rate
    float _next;

    VRDebugOverlay _hud;

    void Awake()
    {
        _hud = FindObjectOfType<VRDebugOverlay>();
        if (!_hud) _hud = gameObject.AddComponent<VRDebugOverlay>(); // ensures a panel exists
    }

    void Update()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + (1f / Mathf.Max(0.01f, updateHz));
        if (!_hud) return;

#if UNITY_2023_1_OR_NEWER
    var all = Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<NetworkRunner>();
#endif
        var active = RunnerLocator.GetActiveRunner();

        var sb = new StringBuilder(1200);
        sb.AppendLine($"Runners found: {all.Length}");
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (!r) continue;

            bool enabled = r.enabled && r.isActiveAndEnabled;
            int players = 0; foreach (var _ in r.ActivePlayers) players++;

            bool isActivePick = (r == active);
            sb.Append(isActivePick ? "▶ " : "  ");
            sb.Append($"[{i}] {r.name}  [{(enabled ? "ENABLED" : "DISABLED")}]  IsRunning={r.IsRunning}  IsServer={r.IsServer}  ProvideInput={r.ProvideInput}  Local={r.LocalPlayer}  Players={players}\n");

            foreach (var p in r.ActivePlayers)
            {
                bool hasPO = r.TryGetPlayerObject(p, out var po);
                sb.Append($"      • {p}  PO={(hasPO ? po.name : "<none>")}");
                if (hasPO) sb.Append($" @ {po.transform.position:F2}");
                sb.Append('\n');
            }
        }

        sb.AppendLine();
        sb.AppendLine("Events:");
        foreach (var line in DebugFeed.Lines) sb.AppendLine(line);

        _hud.SetText(sb.ToString());
    }
}
