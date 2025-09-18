using UnityEngine;

public class PeriscopeGrab : MonoBehaviour
{
    [Header("Animation")]
    public Animator animator;                  // root animator
    public string stateName = "Action.003";    // the state name in your Animator

    [Header("Handles & Axis")]
    public Transform baseGrab;                 // empty/collider at the base end
    public Transform sliderGrab;               // empty/collider at the top end
    public Transform axisStart;                // start of travel (closed)
    public Transform axisEnd;                  // end of travel (open)
    public bool requireBaseToMoveSlider = false;

    [Header("OVR hand anchors")]
    public Transform leftHandAnchor;           // OVRCameraRig/TrackingSpace/LeftHandAnchor
    public Transform rightHandAnchor;          // OVRCameraRig/TrackingSpace/RightHandAnchor

    [Header("Tuning")]
    public float grabStartDistance = 0.08f;    // meters to start grab
    public float animatorDamp = 0.0f;          // >0 to smooth (0 = instant)

    // --- internal
    int stateHash;
    Vector3 axisOrigin, axisDir;
    float axisLen;

    bool baseHeld; Transform baseHand; Vector3 baseOffPos; Quaternion baseOffRot;
    bool sliderHeld; Transform sliderHand;

    float t; // 0..1 current open value

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        stateHash = Animator.StringToHash(stateName);
    }

    void Start()
    {
        RecalcAxis();
        // pause the animator – we’ll sample it manually
        if (animator) animator.speed = 0f;
        SetAnim(t, true);
    }

    public void RecalcAxis()
    {
        axisOrigin = axisStart.position;
        Vector3 end = axisEnd.position;
        axisDir = (end - axisOrigin);
        axisLen = axisDir.magnitude;
        axisDir = axisLen > 1e-6f ? axisDir / axisLen : Vector3.up;
    }

    void Update()
    {
        if (!animator) return;
        if (animator.speed != 0f) animator.speed = 0f; // keep paused

        // Try start/continue base grab
        if (!baseHeld) { TryStartBaseGrab(leftHandAnchor); TryStartBaseGrab(rightHandAnchor); }
        else { UpdateBaseGrab(); }

        // Try start/continue slider grab
        if (!sliderHeld) { TryStartSliderGrab(leftHandAnchor); TryStartSliderGrab(rightHandAnchor); }
        else { UpdateSliderGrab(); }
    }

    // --- Base (carry the whole object)
    void TryStartBaseGrab(Transform hand)
    {
        if (!hand || baseHeld) return;
        if (Vector3.Distance(hand.position, baseGrab.position) > grabStartDistance) return;
        if (!GripDown(hand)) return;

        baseHeld = true; baseHand = hand;
        baseOffPos = hand.InverseTransformPoint(transform.position);
        baseOffRot = Quaternion.Inverse(hand.rotation) * transform.rotation;
    }

    void UpdateBaseGrab()
    {
        if (!Grip(baseHand)) { baseHeld = false; baseHand = null; return; }
        // follow hand (pos+rot); remove rot if you want position-only
        transform.position = baseHand.TransformPoint(baseOffPos);
        transform.rotation = baseHand.rotation * baseOffRot;
    }

    // --- Slider (map along axis -> 0..1)
    void TryStartSliderGrab(Transform hand)
    {
        if (!hand || sliderHeld) return;
        if (requireBaseToMoveSlider && !baseHeld) return;
        if (Vector3.Distance(hand.position, sliderGrab.position) > grabStartDistance) return;
        if (!GripDown(hand)) return;

        sliderHeld = true; sliderHand = hand;
    }

    void UpdateSliderGrab()
    {
        if (!Grip(sliderHand)) { sliderHeld = false; sliderHand = null; return; }

        Vector3 p = sliderHand.position;
        float proj = Vector3.Dot(p - axisOrigin, axisDir);      // distance along axis
        float targetT = Mathf.InverseLerp(0f, axisLen, proj);   // 0..1
        SetAnim(targetT, false);
    }

    void SetAnim(float target, bool instant)
    {
        t = Mathf.Clamp01(instant || animatorDamp <= 0f
             ? target
             : Mathf.Lerp(t, target, 1f - Mathf.Exp(-animatorDamp * Time.deltaTime)));

        animator.Play(stateHash, 0, t);
        animator.Update(0f); // sample immediately

        // optional: move a visible slider handle object
        if (sliderGrab) sliderGrab.position = Vector3.Lerp(axisStart.position, axisEnd.position, t);
    }

    // --- OVR grip helpers (controllers’ grip buttons)
    bool GripDown(Transform hand)
    {
#if OVRPLUGIN_PRESENT
        if (hand == rightHandAnchor) return OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger);
        if (hand == leftHandAnchor)  return OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger);
#endif
        // fallback: always false if OVR not present
        return false;
    }
    bool Grip(Transform hand)
    {
#if OVRPLUGIN_PRESENT
        if (hand == rightHandAnchor) return OVRInput.Get(OVRInput.Button.SecondaryHandTrigger);
        if (hand == leftHandAnchor)  return OVRInput.Get(OVRInput.Button.PrimaryHandTrigger);
#endif
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (axisStart && axisEnd)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(axisStart.position, axisEnd.position);
            Gizmos.DrawSphere(axisStart.position, 0.005f);
            Gizmos.DrawSphere(axisEnd.position, 0.005f);
        }
        if (baseGrab) { Gizmos.color = Color.green; Gizmos.DrawWireCube(baseGrab.position, Vector3.one * 0.04f); }
        if (sliderGrab) { Gizmos.color = Color.yellow; Gizmos.DrawWireCube(sliderGrab.position, Vector3.one * 0.04f); }
    }
}
