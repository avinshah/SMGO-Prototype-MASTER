using UnityEngine;
using UnityEditor;
using System;

public class Quest3ShaderGUI : ShaderGUI
{
    private MaterialProperty useTextureArray;
    private MaterialProperty mainTex;
    private MaterialProperty bumpMap;
    private MaterialProperty metallicGlossMap;
    private MaterialProperty roughnessTexture;
    private MaterialProperty occlusionMap;

    private MaterialProperty baseMapArray;
    private MaterialProperty normalArray;
    private MaterialProperty metallicArray;
    private MaterialProperty roughnessArray;
    private MaterialProperty aoArray;

    private MaterialProperty textureIndex;
    private MaterialProperty useRandomPerObject;
    private MaterialProperty randomSeed;

    private MaterialProperty metallicStrength;
    private MaterialProperty roughnessStrength;
    private MaterialProperty normalStrength;
    private MaterialProperty aoStrength;
    private MaterialProperty tiling;

    private bool showTextureArraySection = true;
    private bool showSingleTextureSection = true;
    private bool showPBRControls = true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // Find all properties
        FindProperties(properties);

        // Get the material
        Material material = materialEditor.target as Material;

        EditorGUI.BeginChangeCheck();

        // Header
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Texture Mode", EditorStyles.boldLabel);

        // Texture Array Toggle
        materialEditor.ShaderProperty(useTextureArray, "Use Texture Arrays");
        bool useArrays = material.GetFloat("_UseTextureArray") > 0.5f;

        EditorGUILayout.Space();

        // Show appropriate texture section based on toggle
        if (useArrays)
        {
            // Texture Array Section
            showTextureArraySection = EditorGUILayout.BeginFoldoutHeaderGroup(showTextureArraySection, "Texture Arrays");
            if (showTextureArraySection)
            {
                EditorGUI.indentLevel++;

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Base Color Array", "Albedo texture array"),
                    baseMapArray);

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Normal Array", "Normal map array"),
                    normalArray);

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Metallic Array", "Metallic texture array"),
                    metallicArray);

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Roughness Array", "Roughness texture array"),
                    roughnessArray);

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("AO Array", "Ambient Occlusion array"),
                    aoArray);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Array Settings", EditorStyles.boldLabel);

                materialEditor.ShaderProperty(textureIndex, "Texture Index");
                materialEditor.ShaderProperty(useRandomPerObject, "Random Per Object");

                if (material.GetFloat("_UseRandomPerObject") > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(randomSeed, "Random Seed");
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        else
        {
            // Single Texture Section
            showSingleTextureSection = EditorGUILayout.BeginFoldoutHeaderGroup(showSingleTextureSection, "Textures");
            if (showSingleTextureSection)
            {
                EditorGUI.indentLevel++;

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Base Color", "Albedo (RGB)"),
                    mainTex);

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Normal Map", "Normal Map"),
                    bumpMap);

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Metallic", "Metallic (R)"),
                    metallicGlossMap);

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Roughness", "Roughness (R)"),
                    roughnessTexture);

                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Ambient Occlusion", "Occlusion (R)"),
                    occlusionMap);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        EditorGUILayout.Space();

        // PBR Controls Section
        showPBRControls = EditorGUILayout.BeginFoldoutHeaderGroup(showPBRControls, "PBR Controls");
        if (showPBRControls)
        {
            EditorGUI.indentLevel++;

            materialEditor.ShaderProperty(metallicStrength, "Metallic Strength");
            materialEditor.ShaderProperty(roughnessStrength, "Roughness Strength");
            materialEditor.ShaderProperty(normalStrength, "Normal Strength");
            materialEditor.ShaderProperty(aoStrength, "AO Strength");

            EditorGUILayout.Space();
            materialEditor.ShaderProperty(tiling, "Tiling and Offset");

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space();

        // Advanced Options
        EditorGUILayout.LabelField("Advanced Options", EditorStyles.boldLabel);
        materialEditor.RenderQueueField();
        materialEditor.EnableInstancingField();
        materialEditor.DoubleSidedGIField();

        // Info box
        EditorGUILayout.Space();
        if (useArrays)
        {
            EditorGUILayout.HelpBox(
                "Texture Array mode is active. Make sure your texture arrays are properly configured with matching dimensions.",
                MessageType.Info);

            if (material.GetFloat("_UseRandomPerObject") > 0.5f)
            {
                EditorGUILayout.HelpBox(
                    "Random per object is enabled. Textures will be randomly selected based on object world position.",
                    MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Single texture mode is active. This is more performant if you don't need texture variation.",
                MessageType.Info);
        }

        // Performance Tips
        if (GUILayout.Button("Show Performance Tips"))
        {
            ShowPerformanceTips();
        }

        if (EditorGUI.EndChangeCheck())
        {
            // Update keywords when properties change
            UpdateKeywords(material);
        }
    }

    private void FindProperties(MaterialProperty[] properties)
    {
        useTextureArray = FindProperty("_UseTextureArray", properties);

        // Single textures
        mainTex = FindProperty("_MainTex", properties);
        bumpMap = FindProperty("_BumpMap", properties);
        metallicGlossMap = FindProperty("_MetallicGlossMap", properties);
        roughnessTexture = FindProperty("_RoughnessTexture", properties);
        occlusionMap = FindProperty("_OcclusionMap", properties);

        // Texture arrays
        baseMapArray = FindProperty("_BaseMapArray", properties);
        normalArray = FindProperty("_NormalArray", properties);
        metallicArray = FindProperty("_MetallicArray", properties);
        roughnessArray = FindProperty("_RoughnessArray", properties);
        aoArray = FindProperty("_AOArray", properties);

        // Array settings
        textureIndex = FindProperty("_TextureIndex", properties);
        useRandomPerObject = FindProperty("_UseRandomPerObject", properties);
        randomSeed = FindProperty("_RandomSeed", properties);

        // PBR controls
        metallicStrength = FindProperty("_MetallicStrength", properties);
        roughnessStrength = FindProperty("_RoughnessStrength", properties);
        normalStrength = FindProperty("_NormalStrength", properties);
        aoStrength = FindProperty("_AOStrength", properties);
        tiling = FindProperty("_Tiling", properties);
    }

    private void UpdateKeywords(Material material)
    {
        // Update USE_TEXTURE_ARRAY keyword
        bool useArrays = material.GetFloat("_UseTextureArray") > 0.5f;
        if (useArrays)
        {
            material.EnableKeyword("USE_TEXTURE_ARRAY");
        }
        else
        {
            material.DisableKeyword("USE_TEXTURE_ARRAY");
        }
    }

    private void ShowPerformanceTips()
    {
        EditorUtility.DisplayDialog(
            "Quest 3 Performance Tips",
            "1. Use single textures when texture variation isn't needed\n\n" +
            "2. Keep texture resolutions reasonable (1024x1024 or 2048x2048)\n\n" +
            "3. Disable shadows in URP settings for better performance\n\n" +
            "4. Use baked lighting instead of realtime lights\n\n" +
            "5. Keep Normal Strength around 1.0\n\n" +
            "6. Texture arrays should have all textures at the same resolution\n\n" +
            "7. Enable GPU Instancing for repeated objects",
            "OK"
        );
    }
}