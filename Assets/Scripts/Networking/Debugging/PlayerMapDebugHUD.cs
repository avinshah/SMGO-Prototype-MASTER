using Fusion;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;


public class PlayerMapDebugHUD : MonoBehaviour
{
    [Tooltip("If true, also draw OnGUI (handy in Editor).")]
    public bool drawOnGUIInEditor = true;

    [Tooltip("Hook this to your VRDebugOverlay method that accepts a string (e.g., SetText).")]
    public UnityEvent<string> OnHudText;

    NetworkRunner _runner;
    GUIStyle _style;

    void Awake()
    {
        _style = new GUIStyle { fontSize = 14, normal = { textColor = Color.cyan } };
    }

    void Update()
    {
        var text = BuildText();
        OnHudText?.Invoke(text); // ← feeds your VR overlay
    }

    void OnGUI()
    {
        if (!drawOnGUIInEditor) return;
        GUI.Label(new Rect(10, 10, 1200, 400), BuildText(), _style);
    }

    string BuildText()
    {
        if (!_runner) _runner = FindObjectOfType<NetworkRunner>();
        if (!_runner) return "Runner=<none>";

        var sb = new StringBuilder(256);
        sb.AppendLine($"Mode={_runner.GameMode}  Running={_runner.IsRunning}  Players={_runner.ActivePlayers.Count()}");

        // (Optional) show who holds StateAuthority on LobbyManager if present
        var lm = FindObjectOfType<LobbyManager>();
        if (lm && lm.Object) sb.AppendLine($"LobbyManager SA={lm.Object.HasStateAuthority}");

        foreach (var p in _runner.ActivePlayers)
        {
            var ok = _runner.TryGetPlayerObject(p, out var po);
            if (ok && po)
            {
                sb.AppendLine($"P{p.PlayerId:D2} → {po.name}  SA={po.HasStateAuthority}  IA={po.HasInputAuthority}");
            }
            else
            {
                sb.AppendLine($"P{p.PlayerId:D2} → <no map>");
            }
        }
        return sb.ToString();
    }
}
