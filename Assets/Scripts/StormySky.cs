using System.Collections.Generic;   // <-- needed for IEnumerator
using UnityEngine;


[ExecuteAlways]
public class StormySky : MonoBehaviour
{
    [Tooltip("Skybox/Panoramic or Skybox/Cubemap material used by RenderSettings.skybox")]
    public Material skyboxMat;

    [Tooltip("Color cycle for storm mood. Left = brightest, Right = darkest.")]
    public Gradient stormColors;

    [Range(0f, 5f)]
    public float cycleSpeed = 0.1f;

    [Tooltip("Degrees per second skybox spins (fake cloud drift). Set 0 to disable.")]
    public float rotationDegPerSec = 2f;

    [Tooltip("Tick if you use Skybox/Procedural so the tint property name matches.")]
    public bool usingProceduralShader = false;

    // Cache property IDs (faster & avoids typos)
    static readonly int _TintID = Shader.PropertyToID("_Tint");      // Panoramic/Cubemap
    static readonly int _SkyTintID = Shader.PropertyToID("_SkyTint");   // Procedural
    static readonly int _RotID = Shader.PropertyToID("_Rotation");

    void OnEnable()
    {
        if (skyboxMat != null)
        {
            // Ensure the scene actually uses THIS material
            RenderSettings.skybox = skyboxMat;
        }
    }

    void Update()
    {
        if (skyboxMat == null) return;

        float t = Mathf.PingPong(Time.time * cycleSpeed, 1f);
        Color c = stormColors.Evaluate(t);

        // Correct tint property depending on shader
        if (usingProceduralShader)
            skyboxMat.SetColor(_SkyTintID, c);
        else
            skyboxMat.SetColor(_TintID, c);

        if (rotationDegPerSec != 0f)
            skyboxMat.SetFloat(_RotID, (rotationDegPerSec * Time.time) % 360f);

        // NOTE: Avoid DynamicGI.UpdateEnvironment() on mobile; it’s expensive.
    }
}
