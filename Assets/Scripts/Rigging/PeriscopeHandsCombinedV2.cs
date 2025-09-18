using UnityEngine;

[DisallowMultipleComponent]
public class PeriscopeHandsCombinedV3 : MonoBehaviour
{
    // ---------- Animation (Animator should be on the MODEL child) ----------
    [Header("Animation")]
    public Animator animator;
    public string stateName = "PeriscopeOpenClose";
    [Range(0, 1)] public float startPct = 0.15f;   // animation % at start (also sets handle position)
    [Range(0, 1)] public float minPct = 0.00f;   // clamp band
    [Range(0, 1)] public float maxPct = 1.00f;

    // ---------- Zones (Transforms on this prefab) ----------
    [Header("Zones (Transforms)")]
    public Transform baseZone;    // start of travel
    public Transform handleZone;  // moved to match % (visual handle)

    // ---------- Axis & range ----------
    public enum AxisMode { LocalX, LocalY, LocalZ, WorldX, WorldY, WorldZ }
    [Header("Extension Axis & Range")]
    public AxisMode extensionAxis = AxisMode.LocalZ;
    public bool invertAxis = false;          // tick for -X/-Y/-Z
    public float maxTravel = 0.70f;          // meters to go 0→1

    // ---------- Hands & attach pose (per-hand offsets) ----------
    [Header("Hands (assign OVR anchors or palm bones)")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Left-hand attach (local to hand)")]
    public Vector3 leftAttachLocalPos = new(0f, 0f, 0f);
    public Vector3 leftAttachLocalEuler = new(0f, 0f, 0f);

    [Header("Right-hand attach (local to hand)")]
    public Vector3 rightAttachLocalPos = new(0f, 0f, 0f);
    public Vector3 rightAttachLocalEuler = new(0f, 0f, 0f);

    [Header("Attach options")]
    public float baseAttachRadius = 0.08f;  // how close to base to latch
    public bool makeKinematicWhileHeld = true;
    public bool stickToFirstHand = true;   // first near base becomes owner

    // ---------- Cylinder-gated interaction ----------
    [Header("Interaction Gate (imaginary cylinder)")]
    public bool useCylinderGate = true;     // gate driving by cylinder volume
    public float cylRadius = 0.08f;    // radius (meters)
    public float enterSlack = 0.02f;    // extra slack beyond handle tip (m)
    public float leaveSlack = 0.03f;    // hysteresis on exit (m)
    public float pullDeadzonePct = 0.005f;   // ignore tiny changes

    [Header("Debug")]
    public bool debugDraw = false;

    // ---- runtime/state ----
    int _stateHash;
    Rigidbody _rb;
    Transform _baseHand;   // latched hand (parent)
    Transform _freeHand;   // other hand
    bool _attached;

    float _animPct;        // current animation % (already clamped to [min..max])
    bool _dragging;
    float _grabD0;         // axis distance at grab (m)
    float _pctAtGrab;      // anim % at grab

    // ----------------------------------------------------------------------

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb) { _rb.isKinematic = true; _rb.useGravity = false; }
    }

    void Start()
    {
        if (!animator) { Debug.LogError($"{name}: Animator not assigned."); enabled = false; return; }
        _stateHash = Animator.StringToHash(stateName);
        animator.speed = 0f;
        SetAnimPctImmediate(Mathf.Clamp01(startPct)); // samples animator + positions handle
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
            SetAnimPctImmediate(Mathf.Clamp01(startPct));
    }
#endif

    void Update()
    {
        // 1) Latch to a hand near base once
        if (!_attached && stickToFirstHand && baseZone && leftHand && rightHand)
        {
            Vector3 b = baseZone.position;
            float r2 = baseAttachRadius * baseAttachRadius;
            float dl = (leftHand.position - b).sqrMagnitude;
            float dr = (rightHand.position - b).sqrMagnitude;
            if (dl <= r2 || dr <= r2)
            {
                _baseHand = (dl <= dr) ? leftHand : rightHand;
                _freeHand = (_baseHand == leftHand) ? rightHand : leftHand;
                AttachToHand(_baseHand);
                _attached = true;
            }
        }

        if (!_attached || _freeHand == null || baseZone == null) return;

        // 2) Cylinder gating + relative drag
        Vector3 axis = AxisDirWorld();
        Vector3 basePt = baseZone.position;
        float openDist = _animPct * maxTravel; // current tip distance along axis

        // signed axis pos of free hand w.r.t base
        float s = Vector3.Dot(_freeHand.position - basePt, axis);

        // clamp to segment (with slack)
        float segMin = -enterSlack;
        float segMax = openDist + enterSlack;
        float sClamped = Mathf.Clamp(s, segMin, segMax);

        // radial distance to axis line at sClamped
        Vector3 closest = basePt + axis * sClamped;
        float radial = (_freeHand.position - closest).magnitude;

        bool inside = (!useCylinderGate) ||
                      (s >= -enterSlack && s <= openDist + enterSlack && radial <= cylRadius);

        // enter/exit hysteresis
        float exitSlack = useCylinderGate ? leaveSlack : 0f;
        bool shouldDrag = inside;
        if (_dragging && useCylinderGate)
        {
            // while dragging allow a bit more slack before dropping
            shouldDrag = (s >= -enterSlack - exitSlack) &&
                         (s <= openDist + enterSlack + exitSlack) &&
                         (radial <= cylRadius + exitSlack);
        }

        if (!_dragging && shouldDrag)
        {
            // begin drag: record reference
            _dragging = true;
            _grabD0 = Mathf.Clamp(s, 0f, openDist); // project to current segment
            _pctAtGrab = _animPct;
        }
        else if (_dragging && !shouldDrag)
        {
            _dragging = false;
        }

        if (_dragging)
        {
            float deltaD = s - _grabD0;               // how much the hand moved along axis since grab
            float newPct = _pctAtGrab + (deltaD / Mathf.Max(0.0001f, maxTravel));
            newPct = Mathf.Clamp(newPct, minPct, maxPct);

            if (Mathf.Abs(newPct - _animPct) >= pullDeadzonePct)
                SetAnimPctImmediate(newPct);
        }

        if (debugDraw)
        {
            // axis & current segment
            Debug.DrawLine(basePt, basePt + axis * maxTravel, Color.cyan);
            Color segCol = _dragging ? Color.green : Color.yellow;
            Debug.DrawLine(basePt, basePt + axis * openDist, segCol);

            // radial line
            Debug.DrawLine(_freeHand.position, closest, Color.magenta);

            // simple ring markers
            Vector3 up = Vector3.up;
            Vector3 ringN = Vector3.Cross(axis, up);
            if (ringN.sqrMagnitude < 1e-4f) ringN = Vector3.Cross(axis, Vector3.right);
            ringN.Normalize();
            Debug.DrawLine(closest - ringN * cylRadius, closest + ringN * cylRadius, Color.white);
        }
    }

    // ----------------------------------------------------------------------

    void AttachToHand(Transform hand)
    {
        if (makeKinematicWhileHeld && _rb) _rb.isKinematic = true;

        bool isLeft = (hand == leftHand);
        Vector3 lp = isLeft ? leftAttachLocalPos : rightAttachLocalPos;
        Vector3 leul = isLeft ? leftAttachLocalEuler : rightAttachLocalEuler;

        transform.SetParent(hand, worldPositionStays: false);
        transform.localPosition = lp;
        transform.localRotation = Quaternion.Euler(leul);

        // ensure visual separation at start
        SetAnimPctImmediate(Mathf.Clamp01(startPct));
    }

    void SetAnimPctImmediate(float pct01)
    {
        _animPct = Mathf.Clamp(pct01, minPct, maxPct);

        if (animator)
        {
            animator.speed = 0f;
            animator.Play(stateName, 0, _animPct);
            animator.Update(0f);
        }

        if (baseZone && handleZone)
        {
            Vector3 axis = AxisDirWorld();
            handleZone.position = baseZone.position + axis * (_animPct * maxTravel);
        }
    }

    Vector3 AxisDirWorld()
    {
        Vector3 dir =
            extensionAxis == AxisMode.LocalX ? transform.right :
            extensionAxis == AxisMode.LocalY ? transform.up :
            extensionAxis == AxisMode.LocalZ ? transform.forward :
            extensionAxis == AxisMode.WorldX ? Vector3.right :
            extensionAxis == AxisMode.WorldY ? Vector3.up :
                                               Vector3.forward;  // WorldZ
        if (invertAxis) dir = -dir;
        return dir.normalized;
    }
}
