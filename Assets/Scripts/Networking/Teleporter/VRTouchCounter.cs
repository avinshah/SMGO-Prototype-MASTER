using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class VRTouchCounter : MonoBehaviour
{
    [Header("UI (world-space)")]
    [Tooltip("If left empty, a small world-space Canvas+Text is auto-created above the object.")]
    public Canvas WorldCanvas;
    public Text CounterText;

    [Header("Visual feedback")]
    public Renderer TargetRenderer;               // auto if left empty
    public Color FlashColor = Color.green;
    public float FlashDuration = 0.15f;

    [Header("Audio (optional)")]
    public AudioSource AudioSrc;                  // auto if left empty
    public AudioClip Beep;

    [Header("Filters")]
    public LayerMask AcceptedLayers = ~0;         // accept any by default
    public string RequiredTag = "";               // leave empty to accept any

    [Header("Debug")]
    public int TouchCount;

    Color _baseColor;
    Material _runtimeMat;
    float _flashT;

    void Reset()
    {
        // Make sure we are a static trigger with kinematic RB
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Awake()
    {
        // Renderer/material setup
        if (!TargetRenderer) TargetRenderer = GetComponentInChildren<Renderer>();
        if (TargetRenderer)
        {
            _runtimeMat = TargetRenderer.material;   // instance
            _baseColor = _runtimeMat.HasProperty("_Color") ? _runtimeMat.color : Color.white;
        }

        // UI setup (create if not assigned)
        if (!WorldCanvas || !CounterText)
            CreateWorldCanvas();

        // Audio
        if (!AudioSrc)
        {
            AudioSrc = gameObject.AddComponent<AudioSource>();
            AudioSrc.spatialBlend = 1f;
            AudioSrc.playOnAwake = false;
        }

        UpdateLabel();
    }

    void Update()
    {
        // Simple flash lerp back to base color
        if (_flashT > 0f && _runtimeMat != null)
        {
            _flashT -= Time.deltaTime / Mathf.Max(FlashDuration, 0.01f);
            var c = Color.Lerp(_baseColor, FlashColor, Mathf.Clamp01(_flashT));
            if (_runtimeMat.HasProperty("_Color")) _runtimeMat.color = c;
        }

        // Keep the label above the object and billboard to camera
        if (WorldCanvas)
        {
            var up = Vector3.up * 0.25f;
            WorldCanvas.transform.position = transform.position + up;
            BillboardToMainCamera(WorldCanvas.transform);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!LayerAccepted(other.gameObject)) return;
        if (!TagAccepted(other.gameObject)) return;

        TouchCount++;
        UpdateLabel();
        Flash();
        BeepOnce();
    }

    bool LayerAccepted(GameObject go) =>
        (AcceptedLayers.value & (1 << go.layer)) != 0;

    bool TagAccepted(GameObject go)
    {
        if (string.IsNullOrEmpty(RequiredTag)) return true;
        if (go.CompareTag(RequiredTag)) return true;
        var p = go.transform.parent;
        return p && p.CompareTag(RequiredTag);
    }

    void UpdateLabel()
    {
        if (CounterText) CounterText.text = $"Touches: {TouchCount}";
    }

    void Flash()
    {
        _flashT = 1f;
        if (_runtimeMat != null && _runtimeMat.HasProperty("_Color"))
            _runtimeMat.color = FlashColor;
    }

    void BeepOnce()
    {
        if (AudioSrc && Beep) AudioSrc.PlayOneShot(Beep, 0.5f);
    }

    void BillboardToMainCamera(Transform t)
    {
        var cam = Camera.main;
        if (!cam) return;
        Vector3 dir = t.position - cam.transform.position;
        dir.y = 0f; // yaw-only billboard for comfort
        if (dir.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    void CreateWorldCanvas()
    {
        // Canvas
        GameObject cg = new GameObject("TouchLabel_Canvas");
        cg.layer = gameObject.layer;
        cg.transform.SetParent(null, worldPositionStays: false);
        WorldCanvas = cg.AddComponent<Canvas>();
        WorldCanvas.renderMode = RenderMode.WorldSpace;
        var scaler = cg.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var rt = WorldCanvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.2f, 0.06f);   // ~20cm x 6cm
        rt.localScale = Vector3.one * 1f;

        // Background
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(WorldCanvas.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.35f);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(WorldCanvas.transform, false);
        CounterText = textGO.AddComponent<Text>();
        CounterText.text = "Touches: 0";
        CounterText.alignment = TextAnchor.MiddleCenter;
        CounterText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        CounterText.color = Color.white;
        var txRT = CounterText.GetComponent<RectTransform>();
        txRT.anchorMin = Vector2.zero;
        txRT.anchorMax = Vector2.one;
        txRT.offsetMin = new Vector2(6, 4);
        txRT.offsetMax = new Vector2(-6, -4);
    }
}
