using Fusion;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

public class VRNetworkHUD : MonoBehaviour
{
    public Vector3 localPos = new Vector3(0, -0.3f, 0.8f);
    public Vector3 localEuler = Vector3.zero;
    public Vector3 localScale = Vector3.one * 0.0025f;
    public int fontSize = 36;
    public Color color = Color.white;

    Text _text;

    void Awake()
    {
        // World-space canvas pinned to camera
        var cam = Camera.main ? Camera.main : FindObjectOfType<Camera>();
        var canvasGO = new GameObject("VRNetworkHUD_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1400, 1000);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);
        _text = textGO.AddComponent<Text>();
        _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _text.fontSize = fontSize;
        _text.color = color;
        _text.alignment = TextAnchor.UpperLeft;
        var tr = _text.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(0, 1); tr.pivot = new Vector2(0, 1);
        tr.sizeDelta = new Vector2(1400, 1000);

        if (cam)
        {
            canvasGO.transform.SetParent(cam.transform, false);
            canvasGO.transform.localPosition = localPos;
            canvasGO.transform.localEulerAngles = localEuler;
            canvasGO.transform.localScale = localScale;
        }
    }

    void Update()
    {
#if UNITY_2023_1_OR_NEWER
    var runners = Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
#else
        var runners = Object.FindObjectsOfType<NetworkRunner>();
#endif
        var sb = new StringBuilder(1024);
        sb.AppendLine($"Runners found: {runners.Length}");

        // Prefer the enabled, running runner for clarity
        foreach (var r in runners)
        {
            bool enabled = r.enabled && r.isActiveAndEnabled;
            int playerCount = 0; foreach (var _ in r.ActivePlayers) playerCount++;
            sb.AppendLine($"- {r.name}  [{(enabled ? "ENABLED" : "DISABLED")}]  IsRunning={r.IsRunning}  IsServer={r.IsServer}  Local={r.LocalPlayer}  Players={playerCount}");
            foreach (var p in r.ActivePlayers)
            {
                bool hasPO = r.TryGetPlayerObject(p, out var po);
                sb.AppendLine($"    • {p}  PO={(hasPO ? po.name : "<none>")}{(hasPO ? $" @{po.transform.position:F2}" : "")}");
            }
        }

        // Debug feed lines (touches / RPCs / teleports)
        sb.AppendLine("\nEvents:");
        if (DebugFeed.Lines != null)
            foreach (var line in DebugFeed.Lines) sb.AppendLine(line);

        if (_text) _text.text = sb.ToString();
    }

}
