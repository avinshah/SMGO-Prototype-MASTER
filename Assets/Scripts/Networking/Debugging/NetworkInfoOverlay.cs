using Fusion;
using UnityEngine;

public class NetworkInfoOverlay : MonoBehaviour
{
    [Header("HUD")]
    public bool show = true;
    public KeyCode toggleKey = KeyCode.F1;
    [Range(10, 28)] public int fontSize = 14;
    public Vector2 topLeft = new Vector2(10, 10);

    NetworkRunner _r;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) show = !show;

        if (_r == null)
        {
#if UNITY_2023_1_OR_NEWER
            _r = Object.FindFirstObjectByType<NetworkRunner>();
#else
            _r = Object.FindObjectOfType<NetworkRunner>();
#endif
        }
    }

    void OnGUI()
    {
        if (!show) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
        float y = topLeft.y;

        if (_r == null)
        {
            GUI.Label(new Rect(topLeft.x, y, 900, 24), "Runner: <none>", style);
            return;
        }

        // Count players in a version-proof way (no LINQ, no .ActivePlayersCount)
        int playerCount = 0;
        foreach (var _ in _r.ActivePlayers) playerCount++;

        GUI.Label(new Rect(topLeft.x, y, 1200, 24),
            $"Runner OK | IsRunning={_r.IsRunning} | IsServer={_r.IsServer} | Local={_r.LocalPlayer} | Players={playerCount}",
            style);
        y += 20;

        // Per-player line, showing whether host can resolve a PlayerObject
        foreach (var p in _r.ActivePlayers)
        {
            bool hasPO = _r.TryGetPlayerObject(p, out var po);
            string poTxt = hasPO ? $"PO:{po.name}" : "PO:<none>";
            string posTxt = hasPO ? $" @ {po.transform.position:F2}" : "";
            GUI.Label(new Rect(topLeft.x, y, 1400, 24), $" - {p}  {poTxt}{posTxt}", style);
            y += 20;
        }
    }
}
