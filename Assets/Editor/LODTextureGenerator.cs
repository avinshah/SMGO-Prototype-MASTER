using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class LODTextureGenerator : EditorWindow
{
    [System.Serializable]
    public class LODSettings
    {
        public bool enabled = true;
        public int targetSize = 1024;
        public string suffix = "_LOD0";
        public TextureImporterCompression compression = TextureImporterCompression.Compressed;
        public int compressionQuality = 50;
    }

    [System.Serializable]
    public class MaterialSettings
    {
        public bool generateMaterials = true;
        public string materialBaseName = "Material";
        public Texture2D baseMap;
        public Texture2D metallicMap;
        public Texture2D normalMap;
        public Texture2D heightMap;
        public Texture2D occlusionMap;
    }

    public LODSettings lod0 = new LODSettings { targetSize = 2048, suffix = "_LOD0" };
    public LODSettings lod1 = new LODSettings { targetSize = 1024, suffix = "_LOD1" };
    public LODSettings lod2 = new LODSettings { targetSize = 512, suffix = "_LOD2" };

    public MaterialSettings materialSettings = new MaterialSettings();

    private Vector2 scrollPos;
    private bool autoRefresh = true;
    private bool generateMipmaps = true;
    private bool showMaterialSettings = true;

    [MenuItem("Tools/LOD Texture Generator")]
    static void ShowWindow()
    {
        LODTextureGenerator window = GetWindow<LODTextureGenerator>("LOD Texture Generator");
        window.minSize = new Vector2(450, 700);
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.LabelField("LOD Texture Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Global Settings
        EditorGUILayout.LabelField("Global Settings", EditorStyles.boldLabel);
        autoRefresh = EditorGUILayout.Toggle("Auto Refresh Assets", autoRefresh);
        generateMipmaps = EditorGUILayout.Toggle("Generate Mipmaps", generateMipmaps);
        EditorGUILayout.Space();

        // LOD0 Settings
        EditorGUILayout.LabelField("LOD0 Settings (Highest Quality)", EditorStyles.boldLabel);
        DrawLODSettings(lod0);
        EditorGUILayout.Space();

        // LOD1 Settings  
        EditorGUILayout.LabelField("LOD1 Settings (Medium Quality)", EditorStyles.boldLabel);
        DrawLODSettings(lod1);
        EditorGUILayout.Space();

        // LOD2 Settings
        EditorGUILayout.LabelField("LOD2 Settings (Low Quality)", EditorStyles.boldLabel);
        DrawLODSettings(lod2);
        EditorGUILayout.Space();

        // Material Settings
        showMaterialSettings = EditorGUILayout.Foldout(showMaterialSettings, "Material Generation Settings", true);
        if (showMaterialSettings)
        {
            DrawMaterialSettings();
        }
        EditorGUILayout.Space();

        // Show what textures will be processed
        List<Texture2D> texturesToProcess = GetTexturesToProcess();
        if (texturesToProcess.Count > 0)
        {
            EditorGUILayout.LabelField("Textures to Process:", EditorStyles.boldLabel);
            foreach (var tex in texturesToProcess)
            {
                if (tex != null)
                {
                    EditorGUILayout.LabelField($"• {tex.name} ({tex.width}x{tex.height})");
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No textures assigned. Assign textures in the Material Generation Settings above.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // Generate Button
        if (texturesToProcess.Count > 0)
        {
            if (GUILayout.Button("Generate LOD Textures & Materials", GUILayout.Height(30)))
            {
                GenerateLODTexturesAndMaterials();
            }
        }

        EditorGUILayout.Space();

        // Instructions
        EditorGUILayout.HelpBox(
            "Instructions:\n" +
            "1. Configure LOD settings as needed\n" +
            "2. Assign textures to material slots in Material Generation Settings\n" +
            "3. Set material base name\n" +
            "4. Click 'Generate LOD Textures & Materials'\n\n" +
            "This will create RESIZED LOD variants of assigned textures and URP materials that use them.",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    void DrawLODSettings(LODSettings settings)
    {
        EditorGUI.indentLevel++;
        settings.enabled = EditorGUILayout.Toggle("Enabled", settings.enabled);

        if (settings.enabled)
        {
            settings.suffix = EditorGUILayout.TextField("Suffix", settings.suffix);
            settings.targetSize = EditorGUILayout.IntSlider("Target Size (pixels)", settings.targetSize, 32, 4096);
            settings.compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", settings.compression);

            if (settings.compression == TextureImporterCompression.Compressed)
            {
                settings.compressionQuality = EditorGUILayout.IntSlider("Quality", settings.compressionQuality, 0, 100);
            }
        }
        EditorGUI.indentLevel--;
    }

    void DrawMaterialSettings()
    {
        EditorGUI.indentLevel++;
        materialSettings.generateMaterials = EditorGUILayout.Toggle("Generate Materials", materialSettings.generateMaterials);

        if (materialSettings.generateMaterials)
        {
            materialSettings.materialBaseName = EditorGUILayout.TextField("Material Base Name", materialSettings.materialBaseName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Texture Assignments:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Assign textures here. LOD variants will be created for each assigned texture.", MessageType.Info);

            materialSettings.baseMap = (Texture2D)EditorGUILayout.ObjectField("Base Map (Albedo)", materialSettings.baseMap, typeof(Texture2D), false);
            materialSettings.metallicMap = (Texture2D)EditorGUILayout.ObjectField("Metallic Map", materialSettings.metallicMap, typeof(Texture2D), false);
            materialSettings.normalMap = (Texture2D)EditorGUILayout.ObjectField("Normal Map", materialSettings.normalMap, typeof(Texture2D), false);
            materialSettings.heightMap = (Texture2D)EditorGUILayout.ObjectField("Height Map", materialSettings.heightMap, typeof(Texture2D), false);
            materialSettings.occlusionMap = (Texture2D)EditorGUILayout.ObjectField("Occlusion Map", materialSettings.occlusionMap, typeof(Texture2D), false);
        }
        EditorGUI.indentLevel--;
    }

    List<Texture2D> GetTexturesToProcess()
    {
        List<Texture2D> textures = new List<Texture2D>();

        if (materialSettings.baseMap != null) textures.Add(materialSettings.baseMap);
        if (materialSettings.metallicMap != null) textures.Add(materialSettings.metallicMap);
        if (materialSettings.normalMap != null) textures.Add(materialSettings.normalMap);
        if (materialSettings.heightMap != null) textures.Add(materialSettings.heightMap);
        if (materialSettings.occlusionMap != null) textures.Add(materialSettings.occlusionMap);

        return textures.Distinct().ToList();
    }

    void GenerateLODTexturesAndMaterials()
    {
        List<Texture2D> texturesToProcess = GetTexturesToProcess();

        if (texturesToProcess.Count == 0)
        {
            Debug.LogWarning("No textures assigned!");
            return;
        }

        Debug.Log($"Starting generation of LOD textures for {texturesToProcess.Count} textures");

        try
        {
            // Step 1: Generate LOD textures
            foreach (Texture2D sourceTexture in texturesToProcess)
            {
                GenerateLODVariantsForTexture(sourceTexture);
            }

            // Step 2: Generate materials if enabled
            if (materialSettings.generateMaterials)
            {
                GenerateMaterials();
            }

            AssetDatabase.Refresh();
            Debug.Log("LOD generation completed successfully!");
            EditorUtility.DisplayDialog("Success", "LOD textures and materials generated successfully!", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error generating LOD textures: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to generate LOD textures: {e.Message}", "OK");
        }
    }

    void GenerateLODVariantsForTexture(Texture2D sourceTexture)
    {
        // Make texture readable
        MakeTextureReadable(sourceTexture);

        string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
        string directory = Path.GetDirectoryName(sourcePath);
        string fileName = Path.GetFileNameWithoutExtension(sourcePath);

        LODSettings[] lodSettings = { lod0, lod1, lod2 };

        foreach (LODSettings lod in lodSettings)
        {
            if (!lod.enabled) continue;

            string newFileName = fileName + lod.suffix + ".png";
            string newPath = Path.Combine(directory, newFileName).Replace('\\', '/');

            Debug.Log($"Generating: {newPath} at {lod.targetSize}x{lod.targetSize}");

            // Create resized texture
            Texture2D resizedTexture = ResizeTexture(sourceTexture, lod.targetSize);

            // Save as PNG
            byte[] pngData = resizedTexture.EncodeToPNG();
            File.WriteAllBytes(newPath, pngData);

            // Clean up
            DestroyImmediate(resizedTexture);

            // Import with settings
            AssetDatabase.ImportAsset(newPath);
            ConfigureTextureImportSettings(newPath, lod, sourceTexture);
        }
    }

    void GenerateMaterials()
    {
        string materialDirectory = GetMaterialDirectory();
        LODSettings[] lodSettings = { lod0, lod1, lod2 };

        Debug.Log($"Creating materials in: {materialDirectory}");

        foreach (LODSettings lod in lodSettings)
        {
            if (!lod.enabled) continue;

            // Create material
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                urpShader = Shader.Find("Standard");
                Debug.LogWarning("URP shader not found, using Standard shader");
            }

            Material material = new Material(urpShader);
            material.name = materialSettings.materialBaseName + lod.suffix;

            // Assign textures
            AssignTextureToMaterial(material, "_BaseMap", materialSettings.baseMap, lod.suffix);
            AssignTextureToMaterial(material, "_MetallicGlossMap", materialSettings.metallicMap, lod.suffix);
            AssignTextureToMaterial(material, "_BumpMap", materialSettings.normalMap, lod.suffix);
            AssignTextureToMaterial(material, "_ParallaxMap", materialSettings.heightMap, lod.suffix);
            AssignTextureToMaterial(material, "_OcclusionMap", materialSettings.occlusionMap, lod.suffix);

            // Save material
            string materialName = materialSettings.materialBaseName + lod.suffix + ".mat";
            string materialPath = Path.Combine(materialDirectory, materialName).Replace('\\', '/');

            AssetDatabase.CreateAsset(material, materialPath);
            Debug.Log($"Created material: {materialPath}");
        }
    }

    void AssignTextureToMaterial(Material material, string propertyName, Texture2D sourceTexture, string lodSuffix)
    {
        if (sourceTexture == null || material == null) return;

        // Try to find LOD variant first
        string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
        string directory = Path.GetDirectoryName(sourcePath);
        string fileName = Path.GetFileNameWithoutExtension(sourcePath);
        string lodTexturePath = Path.Combine(directory, fileName + lodSuffix + ".png").Replace('\\', '/');

        Texture2D textureToUse = AssetDatabase.LoadAssetAtPath<Texture2D>(lodTexturePath);
        if (textureToUse == null)
        {
            textureToUse = sourceTexture; // Fallback to original
            Debug.LogWarning($"LOD texture not found: {lodTexturePath}, using original");
        }

        material.SetTexture(propertyName, textureToUse);
        Debug.Log($"Assigned {textureToUse.name} to {propertyName}");

        // Enable normal map keyword
        if (propertyName == "_BumpMap")
        {
            material.EnableKeyword("_NORMALMAP");
        }
    }

    void MakeTextureReadable(Texture2D texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            AssetDatabase.ImportAsset(path);
        }
    }

    string GetMaterialDirectory()
    {
        Texture2D firstTexture = materialSettings.baseMap ?? materialSettings.metallicMap ??
                                 materialSettings.normalMap ?? materialSettings.heightMap ??
                                 materialSettings.occlusionMap;

        if (firstTexture != null)
        {
            return Path.GetDirectoryName(AssetDatabase.GetAssetPath(firstTexture));
        }

        return "Assets";
    }

    Texture2D ResizeTexture(Texture2D source, int targetSize)
    {
        int newWidth, newHeight;
        if (source.width >= source.height)
        {
            newWidth = targetSize;
            newHeight = Mathf.RoundToInt((float)source.height / source.width * targetSize);
        }
        else
        {
            newHeight = targetSize;
            newWidth = Mathf.RoundToInt((float)source.width / source.height * targetSize);
        }

        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        RenderTexture.active = rt;

        Graphics.Blit(source, rt);

        Texture2D resized = new Texture2D(newWidth, newHeight);
        resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resized.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return resized;
    }

    void ConfigureTextureImportSettings(string assetPath, LODSettings lod, Texture2D sourceTexture)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.maxTextureSize = lod.targetSize;
        importer.textureCompression = lod.compression;
        importer.compressionQuality = lod.compressionQuality;
        importer.mipmapEnabled = generateMipmaps;
        importer.isReadable = false;

        // Copy texture type from source
        TextureImporter sourceImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sourceTexture)) as TextureImporter;
        if (sourceImporter != null)
        {
            importer.textureType = sourceImporter.textureType;
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }
}