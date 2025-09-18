using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Transform))]
public class MetaGrabRelayFeedback : MonoBehaviour
{
    [Header("Visual Feedback")]
    public Color FlashColor = Color.green;
    public float FlashDuration = 0.2f;

    [Header("UI Feedback")]
    public Canvas WorldCanvas;
    public Text FeedbackText;

    [Header("Audio Feedback (optional)")]
    public AudioClip Beep;

    private AudioSource _audio;
    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private Color _baseColor = Color.white;
    private float _flashT;
    private int _count;

    private void Awake()
    {
        // Find a renderer on this object or any child
        _renderer = GetComponent<Renderer>();
        if (!_renderer) _renderer = GetComponentInChildren<Renderer>();

        if (_renderer)
        {
            _mpb = new MaterialPropertyBlock();
            // Try to read starting color
            _renderer.GetPropertyBlock(_mpb);
            if (_renderer.sharedMaterial && _renderer.sharedMaterial.HasProperty("_Color"))
                _baseColor = _renderer.sharedMaterial.color;
        }
        else
        {
            Debug.LogWarning("[MetaGrabRelayFeedback] No Renderer found on self or children.");
        }

        // audio
        _audio = gameObject.GetComponent<AudioSource>();
        if (!_audio) _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 1f;
        _audio.playOnAwake = false;

        if (!WorldCanvas || !FeedbackText)
            CreateWorldCanvas();

        UpdateText();
    }

    private void Update()
    {
        if (_flashT > 0f && _renderer)
        {
            _flashT -= Time.deltaTime / Mathf.Max(0.01f, FlashDuration);
            var c = Color.Lerp(_baseColor, FlashColor, Mathf.Clamp01(_flashT));
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", c);
            _renderer.SetPropertyBlock(_mpb);
        }

        if (WorldCanvas)
        {
            WorldCanvas.transform.position = transform.position + Vector3.up * 0.25f;
            Billboard(WorldCanvas.transform);
        }
    }

    public void OnRelayTriggered(GameObject interactor)
    {
        _count++;
        UpdateText();
        Flash();
        if (_audio && Beep) _audio.PlayOneShot(Beep, 0.5f);
    }

    private void Flash()
    {
        _flashT = 1f;
        if (_renderer)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", FlashColor);
            _renderer.SetPropertyBlock(_mpb);
        }
    }

    private void UpdateText()
    {
        if (FeedbackText) FeedbackText.text = $"Grabbed {_count}x";
    }

    private void Billboard(Transform t)
    {
        var cam = Camera.main;
        if (!cam) return;
        var dir = t.position - cam.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void CreateWorldCanvas()
    {
        var cg = new GameObject("RelayCanvas");
        cg.transform.SetParent(null, false);
        WorldCanvas = cg.AddComponent<Canvas>();
        WorldCanvas.renderMode = RenderMode.WorldSpace;

        var rt = WorldCanvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.22f, 0.07f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(cg.transform, false);
        FeedbackText = textGO.AddComponent<Text>();
        FeedbackText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        FeedbackText.alignment = TextAnchor.MiddleCenter;
        FeedbackText.color = Color.white;
        FeedbackText.text = "Grabbed 0x";
        FeedbackText.rectTransform.sizeDelta = rt.sizeDelta;
    }
}
