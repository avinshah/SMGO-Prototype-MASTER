using UnityEngine;

[System.Serializable]
public class TextureSet
{
    public Texture2D baseColor;
    public Texture2D normal;
    public Texture2D metallic;
    public Texture2D roughness;
    public Texture2D ambientOcclusion;
}

public class Quest3TextureArrayManager : MonoBehaviour
{
    [Header("Texture Sets (up to 16 for Quest 3 optimization)")]
    [SerializeField] private TextureSet[] textureSets;

    [Header("Material to Update")]
    [SerializeField] private Material targetMaterial;

    [Header("Settings")]
    [SerializeField] private bool useTextureArrays = true;
    [SerializeField] private bool createOnStart = true;

    void Start()
    {
        if (createOnStart && textureSets != null && textureSets.Length > 0)
        {
            CreateAndAssignTextureArrays();
        }
    }

    [ContextMenu("Create Texture Arrays")]
    public void CreateAndAssignTextureArrays()
    {
        if (textureSets == null || textureSets.Length == 0)
        {
            Debug.LogError("No texture sets assigned!");
            return;
        }

        if (targetMaterial == null)
        {
            Debug.LogError("No target material assigned!");
            return;
        }

        // Validate all texture sets have the same dimensions
        if (!ValidateTextureDimensions())
        {
            Debug.LogError("All textures must have the same dimensions!");
            return;
        }

        // Create arrays for each texture type
        var baseColorTextures = ExtractTexturesOfType(set => set.baseColor);
        var normalTextures = ExtractTexturesOfType(set => set.normal);
        var metallicTextures = ExtractTexturesOfType(set => set.metallic);
        var roughnessTextures = ExtractTexturesOfType(set => set.roughness);
        var aoTextures = ExtractTexturesOfType(set => set.ambientOcclusion);

        // Create texture arrays
        var baseColorArray = CreateTextureArray(baseColorTextures);
        var normalArray = CreateTextureArray(normalTextures);
        var metallicArray = CreateTextureArray(metallicTextures);
        var roughnessArray = CreateTextureArray(roughnessTextures);
        var aoArray = CreateTextureArray(aoTextures);

        // Assign to material
        targetMaterial.SetTexture("_BaseMapArray", baseColorArray);
        targetMaterial.SetTexture("_NormalArray", normalArray);
        targetMaterial.SetTexture("_MetallicArray", metallicArray);
        targetMaterial.SetTexture("_RoughnessArray", roughnessArray);
        targetMaterial.SetTexture("_AOArray", aoArray);

        // Enable texture array mode
        if (useTextureArrays)
        {
            targetMaterial.EnableKeyword("USE_TEXTURE_ARRAY");
        }
        else
        {
            targetMaterial.DisableKeyword("USE_TEXTURE_ARRAY");
        }

        Debug.Log($"Created texture arrays with {textureSets.Length} textures each");
    }

    private bool ValidateTextureDimensions()
    {
        if (textureSets.Length == 0) return false;

        var firstSet = textureSets[0];
        if (firstSet.baseColor == null) return false;

        int width = firstSet.baseColor.width;
        int height = firstSet.baseColor.height;

        foreach (var set in textureSets)
        {
            if (set.baseColor != null && (set.baseColor.width != width || set.baseColor.height != height)) return false;
            if (set.normal != null && (set.normal.width != width || set.normal.height != height)) return false;
            if (set.metallic != null && (set.metallic.width != width || set.metallic.height != height)) return false;
            if (set.roughness != null && (set.roughness.width != width || set.roughness.height != height)) return false;
            if (set.ambientOcclusion != null && (set.ambientOcclusion.width != width || set.ambientOcclusion.height != height)) return false;
        }

        return true;
    }

    private Texture2D[] ExtractTexturesOfType(System.Func<TextureSet, Texture2D> selector)
    {
        var textures = new Texture2D[textureSets.Length];
        for (int i = 0; i < textureSets.Length; i++)
        {
            textures[i] = selector(textureSets[i]);

            // Use white texture as fallback if null
            if (textures[i] == null)
            {
                textures[i] = Texture2D.whiteTexture;
            }
        }
        return textures;
    }

    // Your original method - now being used!
    Texture2DArray CreateTextureArray(Texture2D[] textures)
    {
        if (textures == null || textures.Length == 0)
        {
            Debug.LogError("Cannot create texture array from null or empty texture array!");
            return null;
        }

        // Ensure we have valid textures
        var firstValidTexture = System.Array.Find(textures, t => t != null);
        if (firstValidTexture == null)
        {
            Debug.LogError("No valid textures found in array!");
            return null;
        }

        Texture2DArray array = new Texture2DArray(
            firstValidTexture.width,
            firstValidTexture.height,
            textures.Length,
            firstValidTexture.format,
            true);

        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null)
            {
                Graphics.CopyTexture(textures[i], 0, array, i);
            }
        }

        return array;
    }

    [ContextMenu("Switch to Single Textures")]
    public void SwitchToSingleTextures()
    {
        if (targetMaterial == null || textureSets == null || textureSets.Length == 0) return;

        // Use first texture set for single texture mode
        var firstSet = textureSets[0];

        if (firstSet.baseColor != null) targetMaterial.SetTexture("_MainTex", firstSet.baseColor);
        if (firstSet.normal != null) targetMaterial.SetTexture("_BumpMap", firstSet.normal);
        if (firstSet.metallic != null) targetMaterial.SetTexture("_MetallicGlossMap", firstSet.metallic);
        if (firstSet.roughness != null) targetMaterial.SetTexture("_RoughnessTexture", firstSet.roughness);
        if (firstSet.ambientOcclusion != null) targetMaterial.SetTexture("_OcclusionMap", firstSet.ambientOcclusion);

        targetMaterial.DisableKeyword("USE_TEXTURE_ARRAY");

        Debug.Log("Switched to single texture mode");
    }
}