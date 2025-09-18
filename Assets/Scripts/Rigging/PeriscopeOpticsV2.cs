using UnityEngine;

[DisallowMultipleComponent]
public class PeriscopeOpticsV2 : MonoBehaviour
{
    [Header("Mounts (assign or leave blank to auto-find by name)")]
    public Transform topMount;                 // child under the HANDLE side (Z = look direction)
    public Transform baseScreen;               // child under the BASE side   (Z = screen normal)
    public string autoTopName = "TopMount";
    public string autoBaseName = "BaseScreen";

    [Header("Camera/RT")]
    public Camera periscopeCam;                // auto-created if null (child of TopMount)
    public int rtSize = 1024;
    public RenderTextureFormat rtFormat = RenderTextureFormat.ARGB32;
    public bool useMipMaps = false;

    [Header("Screen")]
    public MeshRenderer screenRenderer;        // assign your Plane/Quad renderer, or leave null to auto-create a Quad
    public Vector2 screenSizeMeters = new(0.15f, 0.15f);  // used only if we auto-create
    public bool flipU = false;                 // flip if mirrored
    public bool flipV = false;

    [Header("Offsets (optional)")]
    public Vector3 camLocalOffset = Vector3.zero;       // cam relative to TopMount
    public Vector3 screenLocalOffset = Vector3.zero;       // screen relative to BaseScreen

    public enum ScreenNormalMode { ZForward, ZBack, YUp, YDown, XRight, XLeft }

    [Header("Screen Orientation")]
    public ScreenNormalMode screenNormal = ScreenNormalMode.ZForward; // set to YDown for -Y planes



    RenderTexture _rt;
    Material _mat;
    Transform _camT;

    void OnEnable()
    {
        EnsureMounts();
        EnsureRT();
        EnsureCamera();
        EnsureScreen();
        LateUpdate(); // place once immediately
    }

    void OnDisable()
    {
        if (periscopeCam) periscopeCam.targetTexture = null;
        if (_rt) { _rt.Release(); Destroy(_rt); _rt = null; }
        if (_mat) { Destroy(_mat); _mat = null; }
    }

    void EnsureMounts()
    {
        if (!topMount || !baseScreen)
        {
            var all = GetComponentsInChildren<Transform>(true);
            if (!topMount)
                topMount = System.Array.Find(all, t => t.name == autoTopName);
            if (!baseScreen)
                baseScreen = System.Array.Find(all, t => t.name == autoBaseName);
        }

        if (!topMount || !baseScreen)
        {
            Debug.LogError($"{name}: PeriscopeOpticsV2 needs TopMount and BaseScreen. " +
                           $"Create empties named '{autoTopName}' and '{autoBaseName}' under your rig/bones, " +
                           $"or assign them explicitly.");
            enabled = false;
        }
    }

    void EnsureRT()
    {
        if (_rt && (_rt.width != rtSize || _rt.height != rtSize))
        {
            _rt.Release(); Destroy(_rt); _rt = null;
        }
        if (!_rt)
        {
            _rt = new RenderTexture(rtSize, rtSize, 16, rtFormat)
            {
                useMipMap = useMipMaps,
                autoGenerateMips = useMipMaps,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1 // keep MSAA off for Quest perf
            };
            _rt.Create();
        }
    }

    void EnsureCamera()
    {
        if (!periscopeCam)
        {
            var go = new GameObject("PeriscopeCam");
            go.transform.SetParent(topMount, false);
            periscopeCam = go.AddComponent<Camera>();
        }

        _camT = periscopeCam.transform;
        periscopeCam.targetTexture = _rt;
        periscopeCam.clearFlags = CameraClearFlags.Skybox;    // SolidColor also fine
        periscopeCam.backgroundColor = Color.black;
        periscopeCam.allowHDR = false;
        periscopeCam.allowMSAA = false;
        periscopeCam.useOcclusionCulling = false;
        periscopeCam.stereoTargetEye = StereoTargetEyeMask.None; // monoscopic RT
        // Optional: exclude your periscope layer so it doesn't see itself
        // periscopeCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Periscope"));
    }

    void EnsureScreen()
    {
        if (!screenRenderer)
        {
            // Auto-create a Quad (1x1 unit) and scale it to meters
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "PeriscopeScreen";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(baseScreen, false);
            quad.transform.localScale = new Vector3(screenSizeMeters.x, screenSizeMeters.y, 1f);
            screenRenderer = quad.GetComponent<MeshRenderer>();
        }

        if (_mat == null)
        {
            // Use Unlit/Texture so lighting doesn’t dim the feed
            _mat = new Material(Shader.Find("Unlit/Texture"));
            _mat.mainTexture = _rt;
        }
        screenRenderer.sharedMaterial = _mat;
    }

    void LateUpdate()
    {
        if (!topMount || !baseScreen) return;

        // 1) Camera follows TopMount (Z forward, Y up), with local offset
        _camT.position = topMount.TransformPoint(camLocalOffset);
        _camT.rotation = topMount.rotation;

        // 2) Screen sits on BaseScreen with local offset
        var t = screenRenderer.transform;
        // NEW:
        t.position = baseScreen.TransformPoint(screenLocalOffset);

        // rotate the mesh so ITS normal aligns to baseScreen's +Z
        Quaternion meshFix = Quaternion.identity;
        switch (screenNormal)
        {
            case ScreenNormalMode.ZForward: meshFix = Quaternion.identity; break;  // +Z (Unity Quad)
            case ScreenNormalMode.ZBack: meshFix = Quaternion.Euler(0f, 180f, 0f); break;  // -Z → +Z
            case ScreenNormalMode.YUp: meshFix = Quaternion.Euler(90f, 0f, 0f); break;  // +Y → +Z (Unity Plane)
            case ScreenNormalMode.YDown: meshFix = Quaternion.Euler(-90f, 0f, 0f); break;  // -Y → +Z (your case)
            case ScreenNormalMode.XRight: meshFix = Quaternion.Euler(0f, -90f, 0f); break;  // +X → +Z
            case ScreenNormalMode.XLeft: meshFix = Quaternion.Euler(0f, 90f, 0f); break;  // -X → +Z
        }
        t.rotation = baseScreen.rotation * meshFix;

        // If using our auto-created Quad, keep its size locked to meters
        // (If you assigned your own Plane/Quad, we don't touch its scale)
        if (t.name == "PeriscopeScreen")
        {
            var sx = Mathf.Abs(screenSizeMeters.x) * (flipU ? -1f : 1f);
            var sy = Mathf.Abs(screenSizeMeters.y) * (flipV ? -1f : 1f);
            t.localScale = new Vector3(sx, sy, 1f);
        }
        else
        {
            // You supplied your own plane/quad; just flip UV by material if needed
            // Simple flip by tiling is possible:
            var mat = screenRenderer.material;
            var tiling = mat.mainTextureScale;
            tiling.x = Mathf.Abs(tiling.x) * (flipU ? -1f : 1f);
            tiling.y = Mathf.Abs(tiling.y) * (flipV ? -1f : 1f);
            mat.mainTextureScale = tiling;
        }
    }
}
