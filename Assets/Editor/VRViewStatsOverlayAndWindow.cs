// Assets/Editor/VRViewStatsOverlayAndWindow.cs
// Unity 2022+: Scene View overlay + dockable/resizable window.
// Live, edit-mode camera-view stats: visible tris/verts, approx draw calls (submeshes),
// instancing candidates, unique materials/textures, rough texture memory.
//
// Notes:
// - Approx draw calls == submesh count (real draws depend on SRP Batcher, batching, shadows, etc.).
// - Texture memory is a rough guess (width*height*4 / MB). Use Profiler/OVR metrics for accuracy.
// - For SkinnedMeshRenderer we check sk.enabled && activeInHierarchy (avoid isActiveAndEnabled API issues).

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
#endif

// -------- Shared util --------
static class VRViewStatsCore
{
    public struct ViewStats
    {
        public int visibleRenderers;
        public long visibleVerts;
        public long visibleTris;

        public int uniqueMaterials;
        public int uniqueTextures;

        public int approxDrawCalls; // naive: submesh count
        public int instancingEligibleInstances;
        public int instancingGroups;
        public int estInstancedDraws;
        public int estDrawSavings;

        public long approxTexturePixels;
        public double approxTextureMemMB;
    }

    static readonly Dictionary<Mesh, (int subMeshes, long[] trisPerSub, int vertCount)> _meshCache =
        new Dictionary<Mesh, (int, long[], int)>(1024);

    static int GuessBpp(Texture tex) => 4; // rough 32bpp default

    static (int subMeshes, long[] trisPer, int verts) GetMeshInfo(Mesh mesh)
    {
        if (!mesh) return (0, Array.Empty<long>(), 0);
        if (_meshCache.TryGetValue(mesh, out var info)) return info;

        int sub = mesh.subMeshCount;
        var arr = new long[sub];
        for (int i = 0; i < sub; i++)
            arr[i] = (long)mesh.GetIndexCount(i) / 3L;

        info = (sub, arr, mesh.vertexCount);
        _meshCache[mesh] = info;
        return info;
    }

    public static Camera FindSceneViewCamera()
    {
        var sv = SceneView.lastActiveSceneView;
        return sv ? sv.camera : null;
    }

    public static ViewStats CalculateFromCamera(Camera cam, bool includeInactive)
    {
        var s = new ViewStats();
        if (!cam) return s;

        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        int mask = cam.cullingMask;

        var uniqueMats = new HashSet<Material>();
        var uniqueTexs = new HashSet<Texture>();
        var instancingGroups = new Dictionary<(Mesh, Material), int>();

        // MeshFilters
        var mfs = UnityEngine.Object.FindObjectsOfType<MeshFilter>(includeInactive);
        foreach (var mf in mfs)
        {
            if (!mf || !mf.sharedMesh) continue;
            var go = mf.gameObject;
            if (!includeInactive && !go.activeInHierarchy) continue;

            var mr = go.GetComponent<MeshRenderer>();
            if (!mr || !mr.enabled) continue;

            int layer = go.layer;
            if ((mask & (1 << layer)) == 0) continue;

            if (!GeometryUtility.TestPlanesAABB(planes, mr.bounds)) continue;

            s.visibleRenderers++;

            var (sub, trisPer, verts) = GetMeshInfo(mf.sharedMesh);
            s.visibleVerts += verts;
            long tris = 0; for (int i = 0; i < sub; i++) tris += trisPer[i];
            s.visibleTris += tris;

            s.approxDrawCalls += sub;

            foreach (var mat in mr.sharedMaterials)
            {
                if (!mat) continue;
                uniqueMats.Add(mat);
                var t = mat.mainTexture;
                if (t)
                {
                    uniqueTexs.Add(t);
                    if (t is Texture2D t2)
                    {
                        long px = (long)t2.width * (long)t2.height;
                        s.approxTexturePixels += px;
                        s.approxTextureMemMB += (px * GuessBpp(t2)) / (1024.0 * 1024.0);
                    }
                    else s.approxTextureMemMB += 1.0;
                }
                if (mat.enableInstancing)
                {
                    var key = (mf.sharedMesh, mat);
                    instancingGroups[key] = instancingGroups.TryGetValue(key, out var c) ? c + 1 : 1;
                    s.instancingEligibleInstances++;
                }
            }
        }

        // SkinnedMeshRenderers
        var sks = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(includeInactive);
        foreach (var sk in sks)
        {
            if (!sk || !sk.sharedMesh) continue;
            var go = sk.gameObject;
            if (!includeInactive && !go.activeInHierarchy) continue;
            if (!sk.enabled) continue;

            int layer = go.layer;
            if ((mask & (1 << layer)) == 0) continue;

            if (!GeometryUtility.TestPlanesAABB(planes, sk.bounds)) continue;

            s.visibleRenderers++;

            var (sub, trisPer, verts) = GetMeshInfo(sk.sharedMesh);
            s.visibleVerts += verts;
            long tris = 0; for (int i = 0; i < sub; i++) tris += trisPer[i];
            s.visibleTris += tris;

            s.approxDrawCalls += sub;

            foreach (var mat in sk.sharedMaterials)
            {
                if (!mat) continue;
                uniqueMats.Add(mat);
                var t = mat.mainTexture;
                if (t)
                {
                    uniqueTexs.Add(t);
                    if (t is Texture2D t2)
                    {
                        long px = (long)t2.width * (long)t2.height;
                        s.approxTexturePixels += px;
                        s.approxTextureMemMB += (px * GuessBpp(t2)) / (1024.0 * 1024.0);
                    }
                    else s.approxTextureMemMB += 1.0;
                }
                if (mat.enableInstancing)
                {
                    var key = (sk.sharedMesh, mat);
                    instancingGroups[key] = instancingGroups.TryGetValue(key, out var c) ? c + 1 : 1;
                    s.instancingEligibleInstances++;
                }
            }
        }

        s.uniqueMaterials = uniqueMats.Count;
        s.uniqueTextures = uniqueTexs.Count;

        int groups = 0, saved = 0;
        foreach (var kv in instancingGroups)
        {
            groups++;
            saved += (kv.Value - 1);
        }
        s.instancingGroups = groups;
        s.estInstancedDraws = groups;
        s.estDrawSavings = saved;

        return s;
    }
}

#if UNITY_2021_2_OR_NEWER
// -------- Overlay --------
[Overlay(typeof(SceneView), "VR View Stats", true)]
public class VRViewStatsOverlay : Overlay
{
    Toggle _useSceneViewCam;
    ObjectField _cameraPick;
    Toggle _includeInactive;
    Button _openWindowBtn;
    Label _camName;

    Label _visRend, _visVerts, _visTris;
    Label _uniqueMats, _uniqueTex, _texPixels, _texMB;
    Label _approxDraws, _instEligible, _instGroups, _instEstDraws, _instSaved;

    Camera SourceCamera()
    {
        if (_useSceneViewCam.value) return VRViewStatsCore.FindSceneViewCamera();
        var picked = _cameraPick.value as Camera;
        if (picked && picked.isActiveAndEnabled) return picked;
        var main = Camera.main;
        if (main && main.isActiveAndEnabled) return main;
        return VRViewStatsCore.FindSceneViewCamera();
    }

    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement { style = { minWidth = 300, paddingLeft = 6, paddingRight = 6, paddingTop = 6, paddingBottom = 6 } };

        var header = RowSplit(out var title, out _openWindowBtn, leftText: "VR View Stats (Edit Mode)", rightButtonText: "Open as Window");
        _openWindowBtn.clicked += () => VRViewStatsWindow.OpenWindow();
        root.Add(header);

        var camRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
        _useSceneViewCam = new Toggle("Use Scene View Camera") { value = true };
        _useSceneViewCam.RegisterValueChangedCallback(_ => UpdateNow());
        camRow.Add(_useSceneViewCam);

        _cameraPick = new ObjectField("Or pick Camera") { objectType = typeof(Camera), allowSceneObjects = true };
        _cameraPick.RegisterValueChangedCallback(_ => UpdateNow());
        camRow.Add(_cameraPick);
        root.Add(camRow);

        _includeInactive = new Toggle("Include Inactive Objects (slow)") { value = false };
        _includeInactive.RegisterValueChangedCallback(_ => UpdateNow());
        root.Add(_includeInactive);

        _camName = new Label("Camera: -") { style = { marginBottom = 6 } };
        root.Add(_camName);

        root.Add(Box(b => {
            b.Add(MiniHeader("Visible (from camera)"));
            b.Add(Row("Renderers", out _visRend));
            b.Add(Row("Vertices", out _visVerts));
            b.Add(Row("Triangles", out _visTris));
        }));

        root.Add(Box(b => {
            b.Add(MiniHeader("Materials & Textures"));
            b.Add(Row("Unique Materials", out _uniqueMats));
            b.Add(Row("Unique Textures", out _uniqueTex));
            b.Add(Row("Approx Texture Pixels", out _texPixels));
            b.Add(Row("Approx Texture Memory (MB)", out _texMB));
        }));

        root.Add(Box(b => {
            b.Add(MiniHeader("Batches / Draw Calls (approx)"));
            b.Add(Row("Approx Draw Calls (submeshes)", out _approxDraws));
            b.Add(Row("Instancing Eligible (instances)", out _instEligible));
            b.Add(Row("Instancing Groups", out _instGroups));
            b.Add(Row("Est. Instanced Draws", out _instEstDraws));
            b.Add(Row("Est. Draw Call Savings", out _instSaved));
        }));

        var help = new HelpBox(
            "Edit-mode estimates from chosen camera's frustum.\n" +
            "Real draws depend on SRP Batcher, batching, shadows, keywords, lightmaps, etc.\n" +
            "For Quest profiling: Play on device + Unity Profiler + OVR Metrics.",
            HelpBoxMessageType.Info);
        help.style.marginTop = 6;
        root.Add(help);

        EditorApplication.update += UpdateNow;
        Selection.selectionChanged += UpdateNow;
        EditorApplication.hierarchyChanged += UpdateNow;

        UpdateNow();
        return root;
    }

    public override void OnWillBeDestroyed()
    {
        base.OnWillBeDestroyed();
        EditorApplication.update -= UpdateNow;
        Selection.selectionChanged -= UpdateNow;
        EditorApplication.hierarchyChanged -= UpdateNow;
    }

    void UpdateNow()
    {
        var cam = SourceCamera();
        _camName.text = $"Camera: {(cam ? cam.name : "None")}";

        var s = VRViewStatsCore.CalculateFromCamera(cam, _includeInactive.value);

        _visRend.text = s.visibleRenderers.ToString("N0");
        _visVerts.text = s.visibleVerts.ToString("N0");
        _visTris.text = s.visibleTris.ToString("N0");

        _uniqueMats.text = s.uniqueMaterials.ToString("N0");
        _uniqueTex.text = s.uniqueTextures.ToString("N0");
        _texPixels.text = s.approxTexturePixels.ToString("N0");
        _texMB.text = s.approxTextureMemMB.ToString("F2");

        _approxDraws.text = s.approxDrawCalls.ToString("N0");
        _instEligible.text = s.instancingEligibleInstances.ToString("N0");
        _instGroups.text = s.instancingGroups.ToString("N0");
        _instEstDraws.text = s.estInstancedDraws.ToString("N0");
        _instSaved.text = s.estDrawSavings.ToString("N0");
    }

    // ----- UI helpers -----
    static VisualElement Box(Action<VisualElement> fill)
    {
        var ve = new VisualElement();
        ve.style.borderTopWidth = 1; ve.style.borderBottomWidth = 1;
        ve.style.borderLeftWidth = 1; ve.style.borderRightWidth = 1;
        ve.style.paddingLeft = 6; ve.style.paddingRight = 6;
        ve.style.paddingTop = 4; ve.style.paddingBottom = 4;
        ve.style.marginBottom = 6;
        fill(ve);
        return ve;
    }

    static Label MiniHeader(string text)
    {
        var l = new Label(text);
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.marginBottom = 2;
        return l;
    }

    static VisualElement Row(string key, out Label valueLabel)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
        row.Add(new Label(key));
        valueLabel = new Label("0") { style = { unityTextAlign = TextAnchor.MiddleRight } };
        row.Add(valueLabel);
        return row;
    }

    static VisualElement RowSplit(out Label leftTitle, out Button rightButton, string leftText, string rightButtonText)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
        leftTitle = new Label(leftText) { style = { unityFontStyleAndWeight = FontStyle.Bold } };
        rightButton = new Button() { text = rightButtonText };
        row.Add(leftTitle);
        row.Add(rightButton);
        return row;
    }
}

// -------- Dockable/resizable window --------
public class VRViewStatsWindow : EditorWindow
{
    [MenuItem("Window/Analysis/VR View Stats (Window)")]
    public static void OpenWindow()
    {
        var w = GetWindow<VRViewStatsWindow>("VR View Stats");
        w.minSize = new Vector2(340, 220);
        w.Show();
    }

    Toggle _useSceneViewCam;
    ObjectField _cameraPick;
    Toggle _includeInactive;
    Label _camName;

    Label _visRend, _visVerts, _visTris;
    Label _uniqueMats, _uniqueTex, _texPixels, _texMB;
    Label _approxDraws, _instEligible, _instGroups, _instEstDraws, _instSaved;

    Camera SourceCamera()
    {
        if (_useSceneViewCam.value) return VRViewStatsCore.FindSceneViewCamera();
        var picked = _cameraPick.value as Camera;
        if (picked && picked.isActiveAndEnabled) return picked;
        var main = Camera.main;
        if (main && main.isActiveAndEnabled) return main;
        return VRViewStatsCore.FindSceneViewCamera();
    }

    void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingLeft = 8; root.style.paddingRight = 8;
        root.style.paddingTop = 8; root.style.paddingBottom = 8;

        root.Add(new Label("VR View Stats (Edit Mode)") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
        _useSceneViewCam = new Toggle("Use Scene View Camera") { value = true };
        _useSceneViewCam.RegisterValueChangedCallback(_ => UpdateNow());
        row.Add(_useSceneViewCam);

        _cameraPick = new ObjectField("Or pick Camera") { objectType = typeof(Camera), allowSceneObjects = true };
        _cameraPick.RegisterValueChangedCallback(_ => UpdateNow());
        row.Add(_cameraPick);
        root.Add(row);

        _includeInactive = new Toggle("Include Inactive Objects (slow)") { value = false };
        _includeInactive.RegisterValueChangedCallback(_ => UpdateNow());
        root.Add(_includeInactive);

        _camName = new Label("Camera: -") { style = { marginBottom = 6 } };
        root.Add(_camName);

        root.Add(Box(b => {
            b.Add(MiniHeader("Visible (from camera)"));
            b.Add(Row("Renderers", out _visRend));
            b.Add(Row("Vertices", out _visVerts));
            b.Add(Row("Triangles", out _visTris));
        }));

        root.Add(Box(b => {
            b.Add(MiniHeader("Materials & Textures"));
            b.Add(Row("Unique Materials", out _uniqueMats));
            b.Add(Row("Unique Textures", out _uniqueTex));
            b.Add(Row("Approx Texture Pixels", out _texPixels));
            b.Add(Row("Approx Texture Memory (MB)", out _texMB));
        }));

        root.Add(Box(b => {
            b.Add(MiniHeader("Batches / Draw Calls (approx)"));
            b.Add(Row("Approx Draw Calls (submeshes)", out _approxDraws));
            b.Add(Row("Instancing Eligible (instances)", out _instEligible));
            b.Add(Row("Instancing Groups", out _instGroups));
            b.Add(Row("Est. Instanced Draws", out _instEstDraws));
            b.Add(Row("Est. Draw Call Savings", out _instSaved));
        }));

        var help = new HelpBox(
            "Dock and resize this window anywhere. Live, edit-mode camera view.\n" +
            "For accurate runtime/Quest stats, profile on device (Profiler + OVR Metrics).",
            HelpBoxMessageType.Info);
        help.style.marginTop = 6;
        root.Add(help);

        EditorApplication.update += UpdateNow;
        Selection.selectionChanged += UpdateNow;
        EditorApplication.hierarchyChanged += UpdateNow;

        UpdateNow();
    }

    void OnDisable()
    {
        EditorApplication.update -= UpdateNow;
        Selection.selectionChanged -= UpdateNow;
        EditorApplication.hierarchyChanged -= UpdateNow;
    }

    void UpdateNow()
    {
        var cam = SourceCamera();
        _camName.text = $"Camera: {(cam ? cam.name : "None")}";

        var s = VRViewStatsCore.CalculateFromCamera(cam, _includeInactive.value);

        _visRend.text = s.visibleRenderers.ToString("N0");
        _visVerts.text = s.visibleVerts.ToString("N0");
        _visTris.text = s.visibleTris.ToString("N0");

        _uniqueMats.text = s.uniqueMaterials.ToString("N0");
        _uniqueTex.text = s.uniqueTextures.ToString("N0");
        _texPixels.text = s.approxTexturePixels.ToString("N0");
        _texMB.text = s.approxTextureMemMB.ToString("F2");

        _approxDraws.text = s.approxDrawCalls.ToString("N0");
        _instEligible.text = s.instancingEligibleInstances.ToString("N0");
        _instGroups.text = s.instancingGroups.ToString("N0");
        _instEstDraws.text = s.estInstancedDraws.ToString("N0");
        _instSaved.text = s.estDrawSavings.ToString("N0");
    }

    // UI helpers
    static VisualElement Box(Action<VisualElement> fill)
    {
        var ve = new VisualElement();
        ve.style.borderTopWidth = 1; ve.style.borderBottomWidth = 1;
        ve.style.borderLeftWidth = 1; ve.style.borderRightWidth = 1;
        ve.style.paddingLeft = 6; ve.style.paddingRight = 6;
        ve.style.paddingTop = 4; ve.style.paddingBottom = 4;
        ve.style.marginBottom = 6;
        fill(ve);
        return ve;
    }
    static Label MiniHeader(string text)
    {
        var l = new Label(text);
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.marginBottom = 2;
        return l;
    }
    static VisualElement Row(string key, out Label valueLabel)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
        row.Add(new Label(key));
        valueLabel = new Label("0") { style = { unityTextAlign = TextAnchor.MiddleRight } };
        row.Add(valueLabel);
        return row;
    }
}
#endif
