// Assets/Editor/SceneStatsOverlayAndWindow.cs
// Unity 2022+: Scene View overlay + full EditorWindow (resizable/dockable)
// Shows live tris/verts/materials/textures for selection and optional scene totals.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEditor.UIElements; // For HelpBox in UI Toolkit
#endif

// ----------------- Shared util -----------------
static class SceneStatsUtil
{
    public struct MeshCounts
    {
        public long verts, tris;
        public int meshes;
        public int uniqueMaterials, uniqueTextures;
    }

    public static MeshCounts CalculateSelection()
    {
        MeshCounts c = default;
        var matSet = new HashSet<Material>();
        var texSet = new HashSet<Texture>();

        foreach (var go in Selection.gameObjects)
        {
            if (!go) continue;

            // MeshFilter meshes
            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in mfs)
            {
                if (!mf || !mf.sharedMesh) continue;
                if (!mf.gameObject.activeInHierarchy) continue;
                AccumulateMesh(mf.sharedMesh, ref c.verts, ref c.tris, ref c.meshes);
            }

            // Renderers: materials/textures + skinned meshes
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (!r) continue;

                foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue;
                    matSet.Add(m);
                    var tex = m.mainTexture;
                    if (tex) texSet.Add(tex);
                }

                var sk = r as SkinnedMeshRenderer;
                if (sk && sk.sharedMesh)
                {
                    if (!sk.enabled || !sk.gameObject.activeInHierarchy) continue;
                    AccumulateMesh(sk.sharedMesh, ref c.verts, ref c.tris, ref c.meshes);
                }
            }
        }

        c.uniqueMaterials = matSet.Count;
        c.uniqueTextures = texSet.Count;
        return c;
    }

    public static MeshCounts CalculateSceneTotals()
    {
        MeshCounts c = default;

        foreach (var mf in Object.FindObjectsOfType<MeshFilter>())
        {
            if (!mf || !mf.sharedMesh) continue;
            if (!mf.gameObject.activeInHierarchy) continue;
            AccumulateMesh(mf.sharedMesh, ref c.verts, ref c.tris, ref c.meshes);
        }

        foreach (var sk in Object.FindObjectsOfType<SkinnedMeshRenderer>())
        {
            if (!sk || !sk.sharedMesh) continue;
            if (!sk.enabled || !sk.gameObject.activeInHierarchy) continue;
            AccumulateMesh(sk.sharedMesh, ref c.verts, ref c.tris, ref c.meshes);
        }

        return c;
    }

    static void AccumulateMesh(Mesh mesh, ref long verts, ref long tris, ref int meshCount)
    {
        verts += mesh.vertexCount;
        long triCount = 0;
        int subCount = mesh.subMeshCount;
        for (int i = 0; i < subCount; i++)
            triCount += (long)mesh.GetIndexCount(i) / 3L;
        tris += triCount;
        meshCount++;
    }
}

#if UNITY_2021_2_OR_NEWER
// ----------------- Scene View Overlay -----------------
[Overlay(typeof(SceneView), "Selected Stats", true)]
public class SceneStatsOverlay : Overlay
{
    Toggle _sceneTotalsToggle;
    Button _openWindowBtn;
    Label _selMeshes, _selVerts, _selTris, _selMats, _selTex;
    Label _scMeshes, _scVerts, _scTris;
    VisualElement _sceneBox;

    bool _showSceneTotals = false;

    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement();
        root.style.minWidth = 240;
        root.style.paddingLeft = 6;
        root.style.paddingRight = 6;
        root.style.paddingTop = 6;
        root.style.paddingBottom = 6;

        var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
        var title = new Label("Edit-Mode Mesh Stats") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
        _openWindowBtn = new Button(() => SceneStatsWindow.OpenWindow()) { text = "Open as Window" };
        headerRow.Add(title);
        headerRow.Add(_openWindowBtn);
        root.Add(headerRow);

        // Selection box
        var selBox = new VisualElement();
        selBox.Add(MiniHeader("Selection"));
        selBox.Add(_selMeshes = KV("Meshes", "0"));
        selBox.Add(_selVerts = KV("Vertices", "0"));
        selBox.Add(_selTris = KV("Triangles", "0"));
        selBox.Add(_selMats = KV("Materials (unique)", "0"));
        selBox.Add(_selTex = KV("Main Textures (unique)", "0"));
        Boxify(selBox);
        selBox.style.marginTop = 6;
        selBox.style.marginBottom = 6;
        root.Add(selBox);

        _sceneTotalsToggle = new Toggle("Show Scene Totals (active objects)");
        _sceneTotalsToggle.RegisterValueChangedCallback(evt =>
        {
            _showSceneTotals = evt.newValue;
            _sceneBox.style.display = _showSceneTotals ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateNow();
        });
        root.Add(_sceneTotalsToggle);

        // Scene box
        _sceneBox = new VisualElement();
        _sceneBox.Add(MiniHeader("Scene Totals"));
        _sceneBox.Add(_scMeshes = KV("Meshes", "0"));
        _sceneBox.Add(_scVerts = KV("Vertices", "0"));
        _sceneBox.Add(_scTris = KV("Triangles", "0"));
        Boxify(_sceneBox);
        _sceneBox.style.display = DisplayStyle.None;
        root.Add(_sceneBox);

        var help = new HelpBox(
            "Tip: Scene view → Shading → Shaded Wireframe to spot dense meshes. Use Profiler → Rendering for frame stats.",
            HelpBoxMessageType.Info);
        help.style.marginTop = 6;
        root.Add(help);

        // Live updates
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
        var s = SceneStatsUtil.CalculateSelection();
        _selMeshes.text = s.meshes.ToString();
        _selVerts.text = s.verts.ToString("N0");
        _selTris.text = s.tris.ToString("N0");
        _selMats.text = s.uniqueMaterials.ToString();
        _selTex.text = s.uniqueTextures.ToString();

        if (_showSceneTotals)
        {
            var sc = SceneStatsUtil.CalculateSceneTotals();
            _scMeshes.text = sc.meshes.ToString();
            _scVerts.text = sc.verts.ToString("N0");
            _scTris.text = sc.tris.ToString("N0");
        }
    }

    // UI helpers
    static Label MiniHeader(string text)
    {
        var l = new Label(text);
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.marginBottom = 2;
        return l;
    }
    static void Boxify(VisualElement ve)
    {
        ve.style.borderTopWidth = 1;
        ve.style.borderBottomWidth = 1;
        ve.style.borderLeftWidth = 1;
        ve.style.borderRightWidth = 1;
        ve.style.paddingLeft = 6;
        ve.style.paddingRight = 6;
        ve.style.paddingTop = 4;
        ve.style.paddingBottom = 4;
    }
    static Label KV(string key, string val)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
        var k = new Label(key);
        var v = new Label(val) { style = { unityTextAlign = TextAnchor.MiddleRight } };
        row.Add(k);
        row.Add(v);
        return v; // return value label for updates
    }
}

// ----------------- Resizable Window -----------------
public class SceneStatsWindow : EditorWindow
{
    [MenuItem("Window/Analysis/Selected Stats (Window)")]
    public static void OpenWindow()
    {
        var w = GetWindow<SceneStatsWindow>("Selected Stats");
        w.minSize = new Vector2(260, 140);
        w.Show();
    }

    Toggle _sceneTotalsToggle;
    Label _selMeshes, _selVerts, _selTris, _selMats, _selTex;
    Label _scMeshes, _scVerts, _scTris;
    VisualElement _sceneBox;

    void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingLeft = 8;
        root.style.paddingRight = 8;
        root.style.paddingTop = 8;
        root.style.paddingBottom = 8;

        var title = new Label("Edit-Mode Mesh Stats") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } };
        root.Add(title);

        var selBox = new VisualElement();
        selBox.Add(MiniHeader("Selection"));
        selBox.Add(_selMeshes = KV("Meshes", "0"));
        selBox.Add(_selVerts = KV("Vertices", "0"));
        selBox.Add(_selTris = KV("Triangles", "0"));
        selBox.Add(_selMats = KV("Materials (unique)", "0"));
        selBox.Add(_selTex = KV("Main Textures (unique)", "0"));
        Boxify(selBox);
        selBox.style.marginBottom = 6;
        root.Add(selBox);

        _sceneTotalsToggle = new Toggle("Show Scene Totals (active objects)");
        _sceneTotalsToggle.RegisterValueChangedCallback(evt =>
        {
            _sceneBox.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateNow();
        });
        root.Add(_sceneTotalsToggle);

        _sceneBox = new VisualElement();
        _sceneBox.Add(MiniHeader("Scene Totals"));
        _sceneBox.Add(_scMeshes = KV("Meshes", "0"));
        _sceneBox.Add(_scVerts = KV("Vertices", "0"));
        _sceneBox.Add(_scTris = KV("Triangles", "0"));
        Boxify(_sceneBox);
        _sceneBox.style.display = DisplayStyle.None;
        root.Add(_sceneBox);

        var help = new HelpBox(
            "Dock and resize this window anywhere. Updates live in Edit Mode.",
            HelpBoxMessageType.Info);
        help.style.marginTop = 6;
        root.Add(help);

        // Live updates
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
        var s = SceneStatsUtil.CalculateSelection();
        _selMeshes.text = s.meshes.ToString();
        _selVerts.text = s.verts.ToString("N0");
        _selTris.text = s.tris.ToString("N0");
        _selMats.text = s.uniqueMaterials.ToString();
        _selTex.text = s.uniqueTextures.ToString();

        if (_sceneTotalsToggle != null && _sceneTotalsToggle.value)
        {
            var sc = SceneStatsUtil.CalculateSceneTotals();
            _scMeshes.text = sc.meshes.ToString();
            _scVerts.text = sc.verts.ToString("N0");
            _scTris.text = sc.tris.ToString("N0");
        }
    }

    // UI helpers
    static Label MiniHeader(string text)
    {
        var l = new Label(text);
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.marginBottom = 2;
        return l;
    }
    static void Boxify(VisualElement ve)
    {
        ve.style.borderTopWidth = 1;
        ve.style.borderBottomWidth = 1;
        ve.style.borderLeftWidth = 1;
        ve.style.borderRightWidth = 1;
        ve.style.paddingLeft = 6;
        ve.style.paddingRight = 6;
        ve.style.paddingTop = 4;
        ve.style.paddingBottom = 4;
    }
    static Label KV(string key, string val)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
        var k = new Label(key);
        var v = new Label(val) { style = { unityTextAlign = TextAnchor.MiddleRight } };
        row.Add(k);
        row.Add(v);
        return v; // return the value label for updates
    }
}
#endif
