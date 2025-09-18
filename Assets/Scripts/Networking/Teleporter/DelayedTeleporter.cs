using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
#if UNITY_2019_4_OR_NEWER
using UnityEngine.XR;
#endif
#if UNITY_XR_OCULUS
using Unity.XR.Oculus;
#endif

/// <summary>
/// Simple teleporter used by the lobby to move player objects to destinations,
/// with an optional delay and optional floor-aware alignment for the local rig.
/// Works without floor colliders: prefers headset "FloorLevel" tracking origin.
/// </summary>
public class DelayedTeleporter : MonoBehaviour
{
    [Header("After-Teleport Alignment (Local Rig)")]
    [Tooltip("If true, when the LOCAL player's object is teleported, also align the XR rig head to the destination floor (collider-free).")]
    [SerializeField] private bool alignLocalRigAfterTeleport = true;

    [Tooltip("Extra Y offset applied when aligning the local rig. Use -0.40 for seated testing, 0 for standing.")]
    [SerializeField] private float extraFloorYOffset = 0f;

    [Tooltip("Prefer setting/using the device's Floor-level tracking origin (Quest/OpenXR) for accurate floor height.")]
    [SerializeField] private bool preferQuestFloorLevel = true;

    [Tooltip("Fallback head-to-floor height (only used if device won't report FloorLevel and we cannot infer).")]
    [SerializeField] private float fallbackHeadToFloor = 1.5f;

    // -------------- Public API --------------

    /// <summary>
    /// Teleport a Fusion NetworkObject to a target Transform after an optional delay.
    /// If this object has input authority locally and alignLocalRigAfterTeleport is true,
    /// the local XR rig will be aligned to floor at the destination.
    /// </summary>
    public void RequestTeleport(NetworkObject netObj, float delaySeconds, Transform target)
    {
        if (netObj == null || target == null) return;
        StartCoroutine(TeleportRoutine(netObj, delaySeconds, target));
    }

    /// <summary>
    /// Teleport a plain GameObject (non-networked) to a target Transform after a delay.
    /// No network authority checks; will still align the local rig if this is the local player root.
    /// </summary>
    public void RequestTeleport(GameObject go, float delaySeconds, Transform target)
    {
        if (go == null || target == null) return;
        StartCoroutine(TeleportRoutine(go.transform, delaySeconds, target));
    }

    public void RequestTeleport(NetworkObject netObj, float delaySeconds, Vector3 pos, Quaternion rot)
    {
        if (netObj == null) return;
        StartCoroutine(TeleportRoutinePose(netObj, delaySeconds, pos, rot));
    }

    private IEnumerator TeleportRoutinePose(NetworkObject netObj, float delay, Vector3 pos, Quaternion rot)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        netObj.transform.SetPositionAndRotation(pos, rot);

        if (netObj.HasInputAuthority && VRRigMarker.Local != null)
        {
            var rig = VRRigMarker.Local;
            var root = rig.RigRoot ? rig.RigRoot : rig.transform;
            root.position = new Vector3(pos.x, root.position.y, pos.z);
            root.rotation = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
            DelayedTeleporter.AlignLocalRigHeadAboveFloorAt(pos, 0f, 0, 0, 1.5f, true);
        }
    }

    // -------------- Routines --------------

    private IEnumerator TeleportRoutine(NetworkObject netObj, float delay, Transform dst)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // Move the networked object
        var tr = netObj.transform;
        tr.SetPositionAndRotation(dst.position, dst.rotation);

        // If this is OUR player, also place the local XR rig at the same X/Z & yaw, then align height.
        if (alignLocalRigAfterTeleport && netObj.HasInputAuthority && VRRigMarker.Local != null)
        {
            var rig = VRRigMarker.Local;
            var root = rig.RigRoot != null ? rig.RigRoot : rig.transform;

            // Match X/Z and yaw exactly to the spawn
            root.position = new Vector3(dst.position.x, root.position.y, dst.position.z);
            root.rotation = Quaternion.Euler(0f, dst.eulerAngles.y, 0f);

            // Now fix height using FloorLevel tracking origin
            AlignLocalRigHeadAboveFloorAt(
                dst.position,
                extraFloorYOffset,
                0, 0,
                fallbackHeadToFloor,
                preferQuestFloorLevel
            );
        }
    }


    private IEnumerator TeleportRoutine(Transform subject, float delay, Transform dst)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        subject.SetPositionAndRotation(dst.position, dst.rotation);

        if (alignLocalRigAfterTeleport && VRRigMarker.Local != null)
        {
            // We can't know authority from a plain Transform, so only align if this subject IS our rig root.
            var rig = VRRigMarker.Local;
            var root = rig.RigRoot != null ? rig.RigRoot : rig.transform;
            if (subject == root)
            {
                AlignLocalRigHeadAboveFloorAt(
                    dst.position,
                    extraFloorYOffset,
                    0, 0, fallbackHeadToFloor, preferQuestFloorLevel
                );
            }
        }
    }

    // -------------- Collider-free floor alignment --------------

    /// <summary>
    /// Align the LOCAL XR rig so the head sits at:
    /// desiredHeadY = targetFloorY + headHeight + extraOffset.
    /// If the device is in "FloorLevel" origin, headHeight is already correct,
    /// so we simply put rig root Y at targetFloorY + extraOffset.
    /// </summary>
    public static void AlignLocalRigHeadAboveFloorAt(
        Vector3 targetFloorPosition,
        float extraFloorYOffset,
        LayerMask _ignoredFloorMask,   // kept for signature compatibility; not used
        float _ignoredMaxDist,         // kept for signature compatibility; not used
        float fallbackHeadToFloor,
        bool preferQuestFloorLevel)
    {
        var rig = VRRigMarker.Local;
        if (rig == null) return;

        var head = rig.Head != null ? rig.Head : rig.transform;
        var root = rig.RigRoot != null ? rig.RigRoot : rig.transform;

        bool isFloorOrigin = IsFloorOrigin(preferQuestFloorLevel);

        // Head height relative to rig root (works for both Floor/Eye origins)
        float headLocalY = rig.RigRoot
            ? rig.RigRoot.InverseTransformPoint(head.position).y
            : head.localPosition.y;

        float desiredRootY;
        if (isFloorOrigin)
        {
            // In floor origin, the rig's world origin represents the floor.
            // Place root so floor at target + offset.
            desiredRootY = targetFloorPosition.y + extraFloorYOffset;
        }
        else
        {
            // Eye-level or unknown: approximate head-to-floor from current local head height,
            // falling back to inspector value if it's implausible.
            float headToFloor = headLocalY > 0.2f ? headLocalY : Mathf.Max(0.2f, fallbackHeadToFloor);
            float desiredHeadY = targetFloorPosition.y + headToFloor + extraFloorYOffset;
            float deltaY = desiredHeadY - head.position.y;
            desiredRootY = root.position.y + deltaY;
        }

        var p = root.position;
        root.position = new Vector3(p.x, desiredRootY, p.z);
    }

    private static bool IsFloorOrigin(bool preferQuestFloorLevel)
    {
        bool isFloor = false;

        // Oculus / Quest path
#if UNITY_XR_OCULUS
        if (preferQuestFloorLevel && OVRManager.instance != null)
        {
            if (OVRManager.instance.trackingOriginType != OVRManager.TrackingOrigin.FloorLevel)
                OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;

            isFloor = (OVRManager.instance.trackingOriginType == OVRManager.TrackingOrigin.FloorLevel);
        }
#endif

        // Generic XR path (OpenXR, etc.)
#if UNITY_2019_4_OR_NEWER
        try
        {
            var subs = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances(subs);
            if (subs.Count > 0)
            {
                var xr = subs[0];
#if UNITY_2020_2_OR_NEWER
                xr.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                var mode = xr.GetTrackingOriginMode();
                if ((mode & TrackingOriginModeFlags.Floor) == TrackingOriginModeFlags.Floor)
                    isFloor = true;
#else
                // Older API uses enum (not flags)
                xr.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                isFloor = xr.GetTrackingOriginMode() == TrackingOriginModeFlags.Floor;
#endif
            }
        }
        catch { /* best-effort */ }
#endif
        return isFloor;
    }
}
