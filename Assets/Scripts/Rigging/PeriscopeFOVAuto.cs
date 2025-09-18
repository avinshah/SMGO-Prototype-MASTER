using UnityEngine;

[DisallowMultipleComponent]
public class PeriscopeFOVAuto : MonoBehaviour
{
    [Header("Camera")]
    public Camera periscopeCam;               // the periscope camera (monoscopic RT)

    [Header("Separation (pick a mode)")]
    public bool useAnimationPercent = false;  // if true, use percent * maxTravel
    public MonoBehaviour percentDriver;       // optional: script that exposes percent & maxTravel (see notes)
    public string percentProperty = "CurrentPercent"; // public float getter name
    public string travelProperty = "MaxTravelMeters"; // public float getter name

    [Header("Or: Measure from transforms")]
    public Transform topMount;                // near the top mirror/cam
    public Transform baseScreen;              // near the base mirror/screen
    public bool projectOnBaseNormal = true;   // use component along baseScreen.forward
    public float separationOffset = 0.0f;     // add fudge (meters) for offsets in your rig

    [Header("Mirror aperture (vertical)")]
    public float mirrorHeightMeters = 0.15f;  // vertical aperture size at the mirror

    [Header("FOV limits & feel")]
    [Range(0.5f, 1.0f)] public float safetyScale = 0.95f; // shrink a bit to avoid edge clipping
    public float minFOV = 5f;                 // deg
    public float maxFOV = 60f;                // deg
    public float smoothTime = 0.08f;          // seconds (0 = snap)

    float _currentFov;

    void Reset()
    {
        periscopeCam = GetComponentInChildren<Camera>(true);
    }

    void Start()
    {
        if (!periscopeCam)
        {
            Debug.LogWarning($"{name}: PeriscopeFOVAuto has no camera assigned.");
            enabled = false; return;
        }
        _currentFov = periscopeCam.fieldOfView;
    }

    void LateUpdate()
    {
        float D = ComputeSeparationMeters();
        if (D <= 0f) return;

        // vFOV = 2 * atan((H/2) / D)
        float vFovTarget = 2f * Mathf.Rad2Deg * Mathf.Atan((mirrorHeightMeters * 0.5f) / D);
        vFovTarget *= safetyScale;
        vFovTarget = Mathf.Clamp(vFovTarget, minFOV, maxFOV);

        if (smoothTime <= 0f)
        {
            _currentFov = vFovTarget;
        }
        else
        {
            // critically-damped-ish smoothing: f = 1 - exp(-dt / tau)
            float a = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, smoothTime));
            _currentFov = Mathf.Lerp(_currentFov, vFovTarget, a);
        }

        periscopeCam.fieldOfView = _currentFov;
    }

    float ComputeSeparationMeters()
    {
        // Mode A: animation percent * maxTravel (read via reflection so you don't have to refactor)
        if (useAnimationPercent && percentDriver)
        {
            float pct = GetFloatProp(percentDriver, percentProperty, fallback: -1f);
            float span = GetFloatProp(percentDriver, travelProperty, fallback: -1f);
            if (pct >= 0f && span >= 0f)
            {
                float D = Mathf.Max(0.01f, pct * span + separationOffset);
                return D;
            }
        }

        // Mode B: measure from transforms
        if (!topMount || !baseScreen) return 0f;

        Vector3 delta = topMount.position - baseScreen.position;
        float Dm = projectOnBaseNormal
                 ? Mathf.Abs(Vector3.Dot(delta, baseScreen.forward))
                 : delta.magnitude;

        return Mathf.Max(0.01f, Dm + separationOffset);
    }

    static float GetFloatProp(object obj, string propName, float fallback)
    {
        var t = obj.GetType();
        var pi = t.GetProperty(propName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (pi != null && pi.PropertyType == typeof(float))
            return (float)pi.GetValue(obj);
        var fi = t.GetField(propName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (fi != null && fi.FieldType == typeof(float))
            return (float)fi.GetValue(obj);
        return fallback;
    }
}
