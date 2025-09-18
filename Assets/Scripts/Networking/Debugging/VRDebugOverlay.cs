using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space debug HUD sized for VR readability with 12pt text.
/// Places a panel ~1m in front of the user's head and scales it to ~0.352m x ~0.210m.
/// Call SetText()/AppendLine() to update it.
/// </summary>
[DefaultExecutionOrder(10000)]
public class VRDebugOverlay : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Meters in front of head/camera")]
    public float distanceMeters = 1.0f;
    [Tooltip("Meters above/below eye level (positive = up)")]
    public float verticalOffsetMeters = -0.05f;
    [Tooltip("Smoothing for pose following")]
    public float followLerp = 15f;

    [Header("Target World Size (meters)")]
    public float targetWorldWidth = 0.352f;  // ~20° at 1m
    public float targetWorldHeight = 0.210f; // ~12° at 1m

    [Header("Text")]
    public int fontSize = 12; // you asked for 12
    [Tooltip("Left padding, pixels")]
    public int paddingX = 20;
    [Tooltip("Top/bottom padding, pixels")]
    public int paddingY = 14;
    [Range(0f, 1f)] public float bgAlpha = 0.85f;

    [Header("Layer")]
    [Tooltip("Optional layer to put the overlay on (e.g., 'UI'). -1 keeps current.")]
    public int overlayLayer = -1;

    Canvas _canvas;
    RectTransform _rect;
    Text _text;
    Image _bg;

    // internal buffer so you can AppendLine without string allocations
    System.Text.StringBuilder _sb = new System.Text.StringBuilder(1024);

    Transform Head
    {
        get
        {
            var rig = VRRigMarker.Local;
            if (rig && rig.Head) return rig.Head;
            if (Camera.main) return Camera.main.transform;
            return null;
        }
    }

    void Awake()
    {
        // Build a small world-space canvas with a background and Text
        var canvasGO = new GameObject("VRDebugOverlay_Canvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = Camera.main;

        _rect = _canvas.GetComponent<RectTransform>();
        // Choose a comfortable pixel resolution for text wrapping; we’ll auto-scale to hit target meters.
        _rect.sizeDelta = new Vector2(700, 420); // pixels (arbitrary); scale will convert to meters

        // Background
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        _bg = bgGO.AddComponent<Image>();
        _bg.color = new Color(0f, 0f, 0f, bgAlpha);
        var bgRect = _bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        // Text
        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(canvasGO.transform, false);
        _text = txtGO.AddComponent<Text>();
        _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _text.fontSize = fontSize;         // 12pt requested
        _text.alignment = TextAnchor.UpperLeft;
        _text.color = Color.white;
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Truncate;

        var tRect = _text.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
        tRect.offsetMin = new Vector2(paddingX, paddingY);
        tRect.offsetMax = new Vector2(-paddingX, -paddingY);

        // Outline for legibility
        var outline = txtGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        // Optional layer routing
        if (overlayLayer >= 0)
        {
            SetLayerRecursive(canvasGO, overlayLayer);
        }

        // Compute scale so the world width/height match targets.
        // World width = rect.sizeDelta.x * localScale.x
        // => localScale.x = targetWorldWidth / rectWidthPixels
        Vector3 s = Vector3.one;
        s.x = targetWorldWidth / _rect.sizeDelta.x;
        s.y = targetWorldHeight / _rect.sizeDelta.y;
        s.z = (s.x + s.y) * 0.5f; // uniform-ish
        _rect.localScale = s;

        // Start with something visible
        _text.text = "VR Debug Overlay\nWaiting for data…";
    }

    void Update()
    {
        // Keep following the head/camera
        var head = Head;
        if (!head) return;

        // Desired pose: in front of head by distanceMeters, with a small vertical offset.
        var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.0001f) fwd = head.forward; // fallback
        var targetPos = head.position + fwd * distanceMeters + Vector3.up * verticalOffsetMeters;

        // Face the user (yaw-only so it stays upright)
        Vector3 look = targetPos - head.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.0001f) look = fwd;
        var targetRot = Quaternion.LookRotation(look.normalized, Vector3.up);

        // Smooth follow
        transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
    }

    public void SetText(string msg)
    {
        _text.text = msg ?? "";
    }

    public void Clear()
    {
        _sb.Length = 0;
        _text.text = "";
    }

    public void AppendLine(string line)
    {
        _sb.AppendLine(line);
        _text.text = _sb.ToString();
    }

    public void SetBackgroundAlpha(float a)
    {
        bgAlpha = Mathf.Clamp01(a);
        if (_bg) _bg.color = new Color(0f, 0f, 0f, bgAlpha);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        if (!go) return;
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }
}
