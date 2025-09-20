// Assets/Editor/VRPerformanceStatsEnhanced.cs
// Enhanced Unity 2022+ VR Performance Stats with Quest 3 resource estimation
// Includes LOD calculations, CPU/GPU/Memory percentage estimates

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
#endif

// Quest 3 Hardware Specs (conservative estimates for safety)
public static class Quest3Specs
{
    // Memory
    public const float TotalRAM_GB = 8f; // 8GB total
    public const float AvailableRAM_GB = 4.7f; // ~4.7GB available for apps
    public const float AvailableVRAM_GB = 2.5f; // Shared, estimate for graphics

    // Performance targets (per eye)
    public const int TargetFPS = 90;
    public const float FrameTimeMS = 1000f / TargetFPS; // 11.11ms

    // GPU/CPU budgets (milliseconds per frame)
    public const float GPUBudgetMS = 10f; // Leave 1ms headroom
    public const float CPUBudgetMS = 10f;

    // Draw call limits
    public const int MaxDrawCalls = 150; // Conservative for Quest 3
    public const int MaxTriangles = 750000; // Per eye
    public const int MaxVertices = 750000; // Per eye

    // Texture limits
    public const int MaxTextureMemoryMB = 1536; // 1.5GB texture budget

    // Multipliers for stereo rendering
    public const float StereoMultiplier = 2f; // Most costs double for stereo
}

// Enhanced stats structure
public class VRPerformanceStats
{
    // Basic visible stats
    public int visibleRenderers;
    public long visibleVerts;
    public long visibleTris;
    public int meshCount;

    // LOD stats
    public int currentLODLevel;
    public long LODAdjustedTris;
    public long LODAdjustedVerts;
    public Dictionary<int, int> LODDistribution = new Dictionary<int, int>();

    // Materials and textures
    public int uniqueMaterials;
    public int materialInstances;
    public int uniqueShaders;
    public int uniqueTextures;
    public long totalTexturePixels;
    public float textureMemoryMB;
    public Dictionary<TextureFormat, int> textureFormatCounts = new Dictionary<TextureFormat, int>();

    // Draw calls and batching
    public int estimatedDrawCalls;
    public int shadowCasterDrawCalls;
    public int transparentDrawCalls;
    public int instancingGroups;
    public int instancedSaves;
    public int dynamicBatchCandidates;
    public int SRPBatcherCompatible;

    // Performance percentages
    public float cpuUsagePercent;
    public float gpuUsagePercent;
    public float ramUsagePercent;
    public float vramUsagePercent;

    // Per-category breakdowns
    public float meshProcessingGPU;
    public float textureMemoryGPU;
    public float drawCallCPU;
    public float animationCPU;

    // Warnings
    public List<string> performanceWarnings = new List<string>();

    // Overall score (0-100)
    public float performanceScore;
    public PerformanceRating rating;

    public enum PerformanceRating
    {
        Excellent,  // < 50% resources
        Good,       // 50-70%
        Warning,    // 70-85%
        Critical,   // 85-95%
        Unplayable  // > 95%
    }
}

// Core calculation engine
public static class VRPerformanceCore
{
    static readonly Dictionary<Mesh, MeshLODInfo> _meshLODCache = new Dictionary<Mesh, MeshLODInfo>();

    public class MeshLODInfo
    {
        public int[] trianglesPerLOD;
        public int[] verticesPerLOD;
        public int subMeshCount;
    }

    // Calculate bytes per pixel based on format
    static int GetBytesPerPixel(TextureFormat format)
    {
        switch (format)
        {
            case TextureFormat.RGBA32:
            case TextureFormat.ARGB32:
            case TextureFormat.BGRA32:
                return 4;
            case TextureFormat.RGB24:
                return 3;
            case TextureFormat.RG16:
            case TextureFormat.R16:
                return 2;
            case TextureFormat.R8:
            case TextureFormat.Alpha8:
                return 1;
            case TextureFormat.DXT1:
            case TextureFormat.DXT1Crunched:
                return 0.5f > 0 ? 1 : 0; // ~0.5 bytes per pixel
            case TextureFormat.DXT5:
            case TextureFormat.DXT5Crunched:
                return 1; // ~1 byte per pixel
            case TextureFormat.ASTC_4x4:
            case TextureFormat.ASTC_5x5:
            case TextureFormat.ASTC_6x6:
                return 1; // Varies, approximate
            default:
                return 4; // Conservative estimate
        }
    }

    // Get LOD info for a renderer
    // Fixed LOD calculation that avoids double-counting
    static (int lodLevel, long tris, long verts) GetLODInfo(Renderer renderer, Camera camera)
    {
        // CRITICAL: Skip if this renderer is part of a LODGroup but not the active LOD
        var lodGroup = renderer.GetComponentInParent<LODGroup>();
        if (lodGroup)
        {
            var lods = lodGroup.GetLODs();
            float distance = Vector3.Distance(camera.transform.position, lodGroup.transform.position);

            // Calculate which LOD should be active
            float relativeHeight = lodGroup.size / distance;
            float screenHeight = relativeHeight * camera.pixelHeight * 0.5f;
            float normalizedSize = screenHeight / camera.pixelHeight;

            // Find active LOD level
            int activeLOD = lods.Length - 1; // Default to last (culled)
            for (int i = 0; i < lods.Length; i++)
            {
                if (normalizedSize >= lods[i].screenRelativeTransitionHeight)
                {
                    activeLOD = i;
                    break;
                }
            }

            // Check if this renderer is in the active LOD
            bool isActiveRenderer = false;
            if (activeLOD < lods.Length)
            {
                foreach (var r in lods[activeLOD].renderers)
                {
                    if (r == renderer)
                    {
                        isActiveRenderer = true;
                        break;
                    }
                }
            }

            // If this renderer is NOT in the active LOD, return 0 (skip it)
            if (!isActiveRenderer)
                return (-1, 0, 0); // -1 indicates "skip this renderer"

            // Return the mesh data for the active LOD
            Mesh mesh = null;
            if (renderer is MeshRenderer mr && mr.GetComponent<MeshFilter>())
                mesh = mr.GetComponent<MeshFilter>().sharedMesh;
            else if (renderer is SkinnedMeshRenderer smr)
                mesh = smr.sharedMesh;

            if (mesh)
                return (activeLOD, mesh.triangles.Length / 3, mesh.vertexCount);
        }

        // No LOD group - process normally
        Mesh normalMesh = null;
        if (renderer is MeshRenderer mr2 && mr2.GetComponent<MeshFilter>())
            normalMesh = mr2.GetComponent<MeshFilter>().sharedMesh;
        else if (renderer is SkinnedMeshRenderer smr2)
            normalMesh = smr2.sharedMesh;

        if (normalMesh)
            return (0, normalMesh.triangles.Length / 3, normalMesh.vertexCount);

        return (0, 0, 0);
    }

    public static VRPerformanceStats CalculateStats(Camera camera, bool includeInactive = false)
    {
        var stats = new VRPerformanceStats();
        if (!camera) return stats;

        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        int cullingMask = camera.cullingMask;

        // Collections for tracking unique assets
        var uniqueMaterials = new HashSet<Material>();
        var materialInstances = new HashSet<int>();
        var uniqueShaders = new HashSet<Shader>();
        var uniqueTextures = new HashSet<Texture>();
        var uniqueMeshes = new HashSet<Mesh>();
        var instancingGroups = new Dictionary<(Mesh, Material), int>();

        // Process all renderers
        var allRenderers = GameObject.FindObjectsOfType<Renderer>(includeInactive);

        foreach (var renderer in allRenderers)
        {
            if (!renderer) continue;

            var go = renderer.gameObject;
            if (!includeInactive && !go.activeInHierarchy) continue;
            if (!renderer.enabled) continue;

            // Layer culling
            if ((cullingMask & (1 << go.layer)) == 0) continue;

            // Frustum culling
            if (!GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;

            stats.visibleRenderers++;

            // Get LOD-adjusted geometry
            var (lodLevel, tris, verts) = GetLODInfo(renderer, camera);

            // SKIP if this is an inactive LOD child (lodLevel == -1)
            if (lodLevel == -1) continue;

            stats.LODAdjustedTris += tris;
            stats.LODAdjustedVerts += verts;

            // Track LOD distribution
            if (!stats.LODDistribution.ContainsKey(lodLevel))
                stats.LODDistribution[lodLevel] = 0;
            stats.LODDistribution[lodLevel]++;

            // Get mesh
            Mesh mesh = null;
            if (renderer is MeshRenderer mr && mr.GetComponent<MeshFilter>())
            {
                mesh = mr.GetComponent<MeshFilter>().sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer smr)
            {
                mesh = smr.sharedMesh;
                stats.animationCPU += 0.5f; // Rough estimate for skinned mesh overhead
            }

            if (mesh)
            {
                uniqueMeshes.Add(mesh);
                stats.visibleTris += mesh.triangles.Length / 3;
                stats.visibleVerts += mesh.vertexCount;
            }

            // Process materials
            foreach (var mat in renderer.sharedMaterials)
            {
                if (!mat) continue;

                uniqueMaterials.Add(mat);
                materialInstances.Add(mat.GetInstanceID());

                if (mat.shader)
                {
                    uniqueShaders.Add(mat.shader);

                    // Check SRP Batcher compatibility
                    if (mat.shader.name.Contains("Universal Render Pipeline"))
                        stats.SRPBatcherCompatible++;
                }

                // Track textures
                var textures = new List<Texture>();

                if (mat.mainTexture != null)
                    textures.Add(mat.mainTexture);

                if (mat.HasProperty("_BumpMap"))
                    textures.Add(mat.GetTexture("_BumpMap"));

                if (mat.HasProperty("_MetallicGlossMap"))
                    textures.Add(mat.GetTexture("_MetallicGlossMap"));

                if (mat.HasProperty("_OcclusionMap"))
                    textures.Add(mat.GetTexture("_OcclusionMap"));

                if (mat.HasProperty("_EmissionMap"))
                    textures.Add(mat.GetTexture("_EmissionMap"));


                foreach (var tex in textures)
                {
                    if (tex && !uniqueTextures.Contains(tex))
                    {
                        uniqueTextures.Add(tex);

                        if (tex is Texture2D tex2D)
                        {
                            long pixels = (long)tex2D.width * tex2D.height;
                            stats.totalTexturePixels += pixels;

                            var format = tex2D.format;
                            if (!stats.textureFormatCounts.ContainsKey(format))
                                stats.textureFormatCounts[format] = 0;
                            stats.textureFormatCounts[format]++;

                            float bytesPerPixel = GetBytesPerPixel(format);
                            stats.textureMemoryMB += (pixels * bytesPerPixel) / (1024f * 1024f);
                        }
                        else if (tex is RenderTexture rt)
                        {
                            long pixels = (long)rt.width * rt.height;
                            stats.totalTexturePixels += pixels;
                            stats.textureMemoryMB += (pixels * 4) / (1024f * 1024f); // Assume 32bpp
                        }
                    }
                }

                // Check for instancing
                // Check for instancing - DON'T count draw calls here yet
                if (mat.enableInstancing && mesh)
                {
                    var key = (mesh, mat);
                    instancingGroups[key] = instancingGroups.ContainsKey(key) ? instancingGroups[key] + 1 : 1;
                }
                else if (mesh)
                {
                    // Only count draw calls for non-instanced materials
                    stats.estimatedDrawCalls += mesh.subMeshCount;
                }

                // Check transparency
                if (mat.renderQueue >= 3000)
                    stats.transparentDrawCalls++;

                // Shadow casting
                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                    stats.shadowCasterDrawCalls++;
            }

            
        }

        // Calculate instancing savings and actual draw calls
        stats.instancingGroups = instancingGroups.Count;
        foreach (var group in instancingGroups)
        {
            // Each instanced group = 1 draw call total
            stats.estimatedDrawCalls += 1;
            if (group.Value > 1)
                stats.instancedSaves += group.Value - 1;
        }

        // Fill in unique counts
        stats.uniqueMaterials = uniqueMaterials.Count;
        stats.materialInstances = materialInstances.Count;
        stats.uniqueShaders = uniqueShaders.Count;
        stats.uniqueTextures = uniqueTextures.Count;
        stats.meshCount = uniqueMeshes.Count;

        // Calculate resource usage percentages for Quest 3
        CalculatePerformanceMetrics(stats);

        return stats;
    }

    static void CalculatePerformanceMetrics(VRPerformanceStats stats)
    {
        // Account for stereo rendering
        float stereoMultiplier = Quest3Specs.StereoMultiplier;

        // GPU Usage Estimation
        float triangleGPU = (stats.LODAdjustedTris * stereoMultiplier / Quest3Specs.MaxTriangles) * 100f;
        float vertexGPU = (stats.LODAdjustedVerts * stereoMultiplier / Quest3Specs.MaxVertices) * 100f;
        float textureGPU = (stats.textureMemoryMB / Quest3Specs.MaxTextureMemoryMB) * 100f;
        stats.meshProcessingGPU = Mathf.Max(triangleGPU, vertexGPU);
        stats.textureMemoryGPU = textureGPU;
        stats.gpuUsagePercent = stats.meshProcessingGPU * 0.6f + textureGPU * 0.4f;

        // CPU Usage Estimation
        float drawCallCPU = (stats.estimatedDrawCalls * stereoMultiplier / Quest3Specs.MaxDrawCalls) * 100f;
        float shadowCPU = (stats.shadowCasterDrawCalls / 50f) * 100f; // Shadows are expensive
        float transparencyCPU = (stats.transparentDrawCalls / 30f) * 100f; // Transparency is very expensive
        stats.drawCallCPU = drawCallCPU;
        stats.cpuUsagePercent = drawCallCPU * 0.5f + shadowCPU * 0.3f + transparencyCPU * 0.2f + stats.animationCPU;

        // Memory Usage
        float meshMemoryMB = (stats.visibleVerts * 32) / (1024f * 1024f); // ~32 bytes per vertex
        float totalMemoryMB = meshMemoryMB + stats.textureMemoryMB;
        stats.ramUsagePercent = (totalMemoryMB / (Quest3Specs.AvailableRAM_GB * 1024f)) * 100f;
        stats.vramUsagePercent = (stats.textureMemoryMB / (Quest3Specs.AvailableVRAM_GB * 1024f)) * 100f;

        // Generate warnings
        if (stats.gpuUsagePercent > 70)
            stats.performanceWarnings.Add($"GPU usage high: {stats.gpuUsagePercent:F1}% - Reduce triangles/textures");

        if (stats.cpuUsagePercent > 70)
            stats.performanceWarnings.Add($"CPU usage high: {stats.cpuUsagePercent:F1}% - Reduce draw calls");

        if (stats.estimatedDrawCalls * stereoMultiplier > Quest3Specs.MaxDrawCalls)
            stats.performanceWarnings.Add($"Too many draw calls: {stats.estimatedDrawCalls * stereoMultiplier} (max: {Quest3Specs.MaxDrawCalls})");

        if (stats.LODAdjustedTris * stereoMultiplier > Quest3Specs.MaxTriangles)
            stats.performanceWarnings.Add($"Too many triangles: {stats.LODAdjustedTris * stereoMultiplier:N0} (max: {Quest3Specs.MaxTriangles:N0})");

        if (stats.textureMemoryMB > Quest3Specs.MaxTextureMemoryMB)
            stats.performanceWarnings.Add($"Texture memory exceeded: {stats.textureMemoryMB:F1}MB (max: {Quest3Specs.MaxTextureMemoryMB}MB)");

        if (stats.transparentDrawCalls > 20)
            stats.performanceWarnings.Add($"Too many transparent objects: {stats.transparentDrawCalls} (recommended: <20)");

        if (stats.shadowCasterDrawCalls > 30)
            stats.performanceWarnings.Add($"Too many shadow casters: {stats.shadowCasterDrawCalls} (recommended: <30)");

        // Calculate overall performance score
        float maxUsage = Mathf.Max(stats.gpuUsagePercent, stats.cpuUsagePercent, stats.ramUsagePercent, stats.vramUsagePercent);
        stats.performanceScore = 100f - maxUsage;

        // Determine rating
        if (maxUsage < 50) stats.rating = VRPerformanceStats.PerformanceRating.Excellent;
        else if (maxUsage < 70) stats.rating = VRPerformanceStats.PerformanceRating.Good;
        else if (maxUsage < 85) stats.rating = VRPerformanceStats.PerformanceRating.Warning;
        else if (maxUsage < 95) stats.rating = VRPerformanceStats.PerformanceRating.Critical;
        else stats.rating = VRPerformanceStats.PerformanceRating.Unplayable;
    }
}

#if UNITY_2021_2_OR_NEWER
// Enhanced Overlay with performance metrics
[Overlay(typeof(SceneView), "VR Performance Analyzer", true)]
public class VRPerformanceOverlay : Overlay
{
    Toggle _useSceneViewCam;
    ObjectField _cameraPick;
    Toggle _includeInactive;
    Button _openWindowBtn;

    // Display labels
    Label _overallScore, _rating;
    Label _cpuPercent, _gpuPercent, _ramPercent, _vramPercent;
    Label _visibleTris, _lodTris, _drawCalls;
    Label _uniqueMats, _texMemory;
    VisualElement _warningsContainer;

    // Progress bars
    ProgressBar _cpuBar, _gpuBar, _ramBar, _vramBar;

    Camera SourceCamera()
    {
        if (_useSceneViewCam.value)
        {
            var sv = SceneView.lastActiveSceneView;
            return sv ? sv.camera : null;
        }
        return _cameraPick.value as Camera ?? Camera.main;
    }

    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement();
        root.style.minWidth = 350;
        root.style.paddingLeft = 8;
        root.style.paddingRight = 8;
        root.style.paddingTop = 8;
        root.style.paddingBottom = 8;

        // Header with window button
        var header = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
        header.Add(new Label("VR Performance Analyzer") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        _openWindowBtn = new Button(() => VRPerformanceWindow.OpenWindow()) { text = "Open Window" };
        header.Add(_openWindowBtn);
        root.Add(header);

        // Camera selection
        _useSceneViewCam = new Toggle("Use Scene View Camera") { value = true };
        _useSceneViewCam.RegisterValueChangedCallback(_ => UpdateStats());
        root.Add(_useSceneViewCam);

        _cameraPick = new ObjectField("Camera") { objectType = typeof(Camera), allowSceneObjects = true };
        _cameraPick.RegisterValueChangedCallback(_ => UpdateStats());
        root.Add(_cameraPick);

        _includeInactive = new Toggle("Include Inactive") { value = false };
        _includeInactive.RegisterValueChangedCallback(_ => UpdateStats());
        root.Add(_includeInactive);

        root.Add(new Label("")); // Spacer

        // Overall Performance Score
        var scoreBox = new VisualElement();
        scoreBox.style.borderTopWidth = scoreBox.style.borderBottomWidth =
            scoreBox.style.borderLeftWidth = scoreBox.style.borderRightWidth = 1;
        scoreBox.style.paddingLeft = 6;
        scoreBox.style.paddingRight = 6;
        scoreBox.style.paddingTop = 6;
        scoreBox.style.paddingBottom = 6;
        scoreBox.style.marginBottom = 8;

        var scoreRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
        scoreRow.Add(new Label("Performance Score") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        _overallScore = new Label("--") { style = { fontSize = 18 } };
        scoreRow.Add(_overallScore);
        scoreBox.Add(scoreRow);

        _rating = new Label("--") { style = { unityTextAlign = TextAnchor.MiddleCenter, marginTop = 4 } };
        scoreBox.Add(_rating);
        root.Add(scoreBox);

        // Resource Usage Bars
        root.Add(new Label("Resource Usage (%)") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });

        _cpuBar = CreateProgressBar("CPU", out _cpuPercent);
        root.Add(_cpuBar);

        _gpuBar = CreateProgressBar("GPU", out _gpuPercent);
        root.Add(_gpuBar);

        _ramBar = CreateProgressBar("RAM", out _ramPercent);
        root.Add(_ramBar);

        _vramBar = CreateProgressBar("VRAM", out _vramPercent);
        root.Add(_vramBar);

        // Key Stats
        root.Add(new Label("Key Metrics") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });

        var statsGrid = new VisualElement();
        statsGrid.Add(CreateStatRow("Visible Tris", out _visibleTris));
        statsGrid.Add(CreateStatRow("LOD Adjusted Tris", out _lodTris));
        statsGrid.Add(CreateStatRow("Est. Draw Calls", out _drawCalls));
        statsGrid.Add(CreateStatRow("Unique Materials", out _uniqueMats));
        statsGrid.Add(CreateStatRow("Texture Memory (MB)", out _texMemory));
        root.Add(statsGrid);

        // Warnings
        root.Add(new Label("Warnings") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });
        _warningsContainer = new VisualElement();
        root.Add(_warningsContainer);

        // Help text
        var help = new HelpBox(
            "Quest 3 Targets:\n" +
            "• Draw Calls: <150 (both eyes)\n" +
            "• Triangles: <750k per eye\n" +
            "• Texture Memory: <1.5GB\n" +
            "• 90 FPS (11.1ms frame time)",
            HelpBoxMessageType.Info);
        help.style.marginTop = 8;
        root.Add(help);

        // Register update callbacks
        EditorApplication.update += UpdateStats;
        SceneView.duringSceneGui += OnSceneGUI;

        UpdateStats();
        return root;
    }

    void OnSceneGUI(SceneView sv)
    {
        // Could add scene view overlays here if needed
    }

    public override void OnWillBeDestroyed()
    {
        EditorApplication.update -= UpdateStats;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    ProgressBar CreateProgressBar(string label, out Label percentLabel)
    {
        var bar = new ProgressBar();
        bar.title = label;
        bar.style.height = 20;
        bar.style.marginBottom = 4;

        percentLabel = new Label("0%");
        percentLabel.style.position = Position.Absolute;
        percentLabel.style.right = 4;
        percentLabel.style.top = 0;
        bar.Add(percentLabel);

        return bar;
    }

    VisualElement CreateStatRow(string label, out Label value)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
        row.Add(new Label(label));
        value = new Label("0");
        row.Add(value);
        return row;
    }

    void UpdateStats()
    {
        var cam = SourceCamera();
        if (!cam)
        {
            _overallScore.text = "--";
            _rating.text = "No Camera";
            return;
        }

        var stats = VRPerformanceCore.CalculateStats(cam, _includeInactive.value);

        // Update score and rating
        _overallScore.text = stats.performanceScore.ToString("F0");
        _rating.text = stats.rating.ToString();

        // Color code the rating
        Color ratingColor = stats.rating switch
        {
            VRPerformanceStats.PerformanceRating.Excellent => Color.green,
            VRPerformanceStats.PerformanceRating.Good => new Color(0.5f, 1f, 0.5f),
            VRPerformanceStats.PerformanceRating.Warning => Color.yellow,
            VRPerformanceStats.PerformanceRating.Critical => new Color(1f, 0.5f, 0f),
            VRPerformanceStats.PerformanceRating.Unplayable => Color.red,
            _ => Color.white
        };
        _rating.style.color = ratingColor;
        _overallScore.style.color = ratingColor;

        // Update progress bars
        UpdateProgressBar(_cpuBar, _cpuPercent, stats.cpuUsagePercent);
        UpdateProgressBar(_gpuBar, _gpuPercent, stats.gpuUsagePercent);
        UpdateProgressBar(_ramBar, _ramPercent, stats.ramUsagePercent);
        UpdateProgressBar(_vramBar, _vramPercent, stats.vramUsagePercent);

        // Update stats
        _visibleTris.text = stats.visibleTris.ToString("N0");
        _lodTris.text = $"{stats.LODAdjustedTris:N0} (×2 stereo)";
        _drawCalls.text = $"{stats.estimatedDrawCalls} (×2 stereo)";
        _uniqueMats.text = stats.uniqueMaterials.ToString();
        _texMemory.text = stats.textureMemoryMB.ToString("F1");

        // Update warnings
        _warningsContainer.Clear();
        if (stats.performanceWarnings.Count == 0)
        {
            _warningsContainer.Add(new Label("No warnings - performance looks good!")
            {
                style = { color = Color.green }
            });
        }
        else
        {
            foreach (var warning in stats.performanceWarnings.Take(5)) // Show max 5 warnings
            {
                var warningLabel = new Label("• " + warning)
                {
                    style = { color = Color.yellow, whiteSpace = WhiteSpace.Normal }
                };
                _warningsContainer.Add(warningLabel);
            }
        }
    }

    void UpdateProgressBar(ProgressBar bar, Label label, float percent)
    {
        bar.value = percent;
        label.text = $"{percent:F1}%";

        // Color code based on usage
        Color barColor;
        if (percent < 50) barColor = Color.green;
        else if (percent < 70) barColor = new Color(0.5f, 1f, 0.5f);
        else if (percent < 85) barColor = Color.yellow;
        else if (percent < 95) barColor = new Color(1f, 0.5f, 0f);
        else barColor = Color.red;

        // Note: ProgressBar color styling is limited in UI Toolkit
        // Setting background color as a visual indicator
        var bgColor = barColor * 0.3f;
        bgColor.a = 0.3f;
        bar.style.backgroundColor = new StyleColor(bgColor);
    }
}

// Window version
public class VRPerformanceWindow : EditorWindow
{
    [MenuItem("Window/Analysis/VR Performance Analyzer")]
    public static void OpenWindow()
    {
        var window = GetWindow<VRPerformanceWindow>("VR Performance");
        window.minSize = new Vector2(400, 600);
        window.Show();
    }

    // Implementation would be similar to overlay but with more detailed views
    // Keeping this simple for space constraints
    void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingLeft = 10;
        root.style.paddingRight = 10;
        root.style.paddingTop = 10;
        root.style.paddingBottom = 10;

        var label = new Label("VR Performance Analyzer - Window Mode");
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 14;
        label.style.marginBottom = 10;
        root.Add(label);

        var helpText = new Label("The full window implementation would include:\n" +
            "• Detailed performance breakdowns\n" +
            "• Historical graphs\n" +
            "• Per-object analysis\n" +
            "• Export functionality");
        helpText.style.whiteSpace = WhiteSpace.Normal;
        root.Add(helpText);

        var note = new Label("\nFor now, use the Scene View overlay for the complete functionality.");
        note.style.whiteSpace = WhiteSpace.Normal;
        note.style.color = new StyleColor(Color.gray);
        root.Add(note);
    }
}
#endif