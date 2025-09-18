using UnityEngine;

public class StickyPeriscopeHandsLite : MonoBehaviour
{
    [Header("Animation")]
    // in StickyPeriscopeHandsLite
    [SerializeField] Animator animator;
    [SerializeField] string stateName = "PeriscopeOpenClose";
    [SerializeField, Range(0f, 1f)] float startPct = 0.25f;
    float pct;
    [Range(0, 1)] public float minPct = 0.05f;   // clamp band min
    [Range(0, 1)] public float maxPct = 1.00f;   // clamp band max

    [Header("Zones on the periscope (colliders on THIS prefab)")]
    public Collider baseZone;                 // collider around base area
    public Collider handleZone;               // collider at the far/handle end
    public float baseAttachRadius = 0.08f;    // meters
    public float handleRadius = 0.08f;        // meters

    public enum AxisRef { LocalZ, LocalY, LocalX, WorldX, WorldY, WorldZ }
    [Header("Extension axis")]
    public AxisRef extensionAxis = AxisRef.LocalZ; // your prefab extends along local +Z
    public float maxTravel = 0.70f;                // meters
    public bool computeFromZoneCentersAtStart = true;

    [Header("Hands (assign ACTUAL palm transforms)")]
    public Transform leftPalm;                // e.g., LeftHandAnchor or palm bone
    public Transform rightPalm;               // e.g., RightHandAnchor or palm bone

    [Header("Mount when latched")]
    public bool attachInWorldPose = true;     // keep current world pose when parenting (prevents floor snap)
    public Vector3 mountLocalPosition = new(0f, 0f, -0.05f);
    public Vector3 mountLocalEuler = Vector3.zero;
    public bool mirrorXForLeftHand = true;

    [Header("Smoothing")]
    [Range(0f, 20f)] public float tDamp = 0f;

    // runtime
    int _stateHash;
    Transform _baseHand;    // latched hand
    Transform _sliderHand;  // other hand while near handle
    float _t;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        // animator settings that avoid surprises
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
    }

    void OnEnable()
    {
        pct = Mathf.Clamp01(startPct);
        // pause the state machine and sample the exact normalized time
        animator.speed = 0f;
        animator.Play(stateName, 0, pct);
        animator.Update(0f); // force-evaluate this frame
    }

    void Start()
    {
        animator.speed = 0f;
        animator.Play(_stateHash, 0, 0f);
        animator.Update(0f);

        if (computeFromZoneCentersAtStart && baseZone && handleZone)
        {
            Vector3 ax = GetAxisDir();
            Vector3 a = baseZone.bounds.center;
            Vector3 b = handleZone.bounds.center;
            maxTravel = Mathf.Abs(Vector3.Dot(b - a, ax));
        }
    }

    void Update()
    {
        // Trigger-less path (works even if you keep ZoneRelay): latch & drive based on proximity
        if (_baseHand == null)
        {
            TryLatch(leftPalm);
            TryLatch(rightPalm);
            return;
        }

        var other = (_baseHand == leftPalm) ? rightPalm : leftPalm;
        if (other && Near(other.position, handleZone, handleRadius))
            _sliderHand = other;
        else if (_sliderHand == other && !Near(other.position, handleZone, handleRadius))
            _sliderHand = null;

        if (_sliderHand != null)
        {
            Vector3 ax = GetAxisDir();
            float d = Vector3.Dot(_sliderHand.position - _baseHand.position, ax);
            float minD = minPct * Mathf.Max(1e-4f, maxTravel);
            float maxD = maxPct * Mathf.Max(1e-4f, maxTravel);
            float target = Mathf.InverseLerp(minD, maxD, d);
            SetT(target);
        }
        else
        {
            SetT(_t);
        }
    }

    // -------- Optional: called by ZoneRelay if you keep it --------
    public void ZoneEnter(ZoneRelay.Kind kind, Collider other)
    {
        var palm = FindPalmFromCollider(other);
        if (!palm) return;

        if (kind == ZoneRelay.Kind.Base && _baseHand == null)
        {
            AttachToHand(palm, palm == leftPalm);
        }
        else if (kind == ZoneRelay.Kind.Handle && _baseHand != null && palm != _baseHand)
        {
            _sliderHand = palm;
        }
    }

    public void ZoneExit(ZoneRelay.Kind kind, Collider other)
    {
        if (kind != ZoneRelay.Kind.Handle || _sliderHand == null) return;
        var palm = FindPalmFromCollider(other);
        if (palm && palm == _sliderHand) _sliderHand = null;
    }
    // ---------------------------------------------------------------

    void TryLatch(Transform palm)
    {
        if (!palm || !baseZone) return;
        if (!Near(palm.position, baseZone, baseAttachRadius)) return;
        AttachToHand(palm, palm == leftPalm);
    }

    void AttachToHand(Transform palm, bool isLeft)
    {
        _baseHand = palm;

        if (attachInWorldPose)
        {
            // preserve world pose when changing parent (prevents snapping to floor/origin)
            Vector3 wp = transform.position;
            Quaternion wr = transform.rotation;
            transform.SetParent(palm, worldPositionStays: true);
            transform.position = wp;
            transform.rotation = wr;
        }
        else
        {
            transform.SetParent(palm, worldPositionStays: false);
        }

        // apply mount offset/orientation relative to palm
        var pos = mountLocalPosition;
        if (isLeft && mirrorXForLeftHand) pos.x = -pos.x;
        transform.localPosition = pos;

        var rot = Quaternion.Euler(mountLocalEuler);
        if (isLeft && mirrorXForLeftHand)
            rot = Quaternion.Euler(mountLocalEuler.x, -mountLocalEuler.y, -mountLocalEuler.z);
        transform.localRotation = rot;

        // start slightly open so handle sits above base zone
        SetT(Mathf.Clamp01(startPct));
    }

    Vector3 GetAxisDir()
    {
        return extensionAxis switch
        {
            AxisRef.LocalZ => transform.TransformDirection(Vector3.forward),
            AxisRef.LocalY => transform.TransformDirection(Vector3.up),
            AxisRef.LocalX => transform.TransformDirection(Vector3.right),
            AxisRef.WorldX => Vector3.right,
            AxisRef.WorldY => Vector3.up,
            AxisRef.WorldZ => Vector3.forward,
            _ => transform.TransformDirection(Vector3.forward)
        };
    }

    void SetT(float t01)
    {
        float target = Mathf.Clamp01(t01);
        _t = (tDamp <= 0f) ? target : Mathf.Lerp(_t, target, 1f - Mathf.Exp(-tDamp * Time.deltaTime));
        animator.Play(_stateHash, 0, _t);
        animator.Update(0f);
    }

    static bool Near(Vector3 point, Collider zone, float radius)
    {
        if (!zone) return false;
        Vector3 closest = zone.ClosestPoint(point);
        return (point - closest).sqrMagnitude <= radius * radius;
    }

    // identify which palm a collider belongs to (works with OVR hand hierarchies)
    Transform FindPalmFromCollider(Collider other)
    {
        if (!other) return null;
        var t = other.transform;
        if (IsDescendantOf(t, leftPalm)) return leftPalm;
        if (IsDescendantOf(t, rightPalm)) return rightPalm;
        return null;
    }
    static bool IsDescendantOf(Transform t, Transform root)
    {
        if (!t || !root) return false;
        for (var p = t; p != null; p = p.parent)
            if (p == root) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        // visualize axis and band
        Vector3 ax = GetAxisDir();
        Vector3 a = baseZone ? baseZone.bounds.center : transform.position;
        float minD = minPct * Mathf.Max(1e-4f, maxTravel);
        float maxD = maxPct * Mathf.Max(1e-4f, maxTravel);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a + ax * minD, a + ax * maxD);
    }
}
