using UnityEngine;

/// <summary>
/// Attach this to your local XR rig (e.g., XR Origin / OVRCameraRig).
/// There should be exactly ONE enabled per client (the local player's rig).
/// Remote players won't have a local rig, so their clients won't have a Local marker.
/// </summary>
public class VRRigMarker : MonoBehaviour
{
    /// <summary>The local player's rig marker on THIS client (null on remote-only clients).</summary>
    public static VRRigMarker Local { get; private set; }

    [Tooltip("Root you normally move (XR Origin / OVRCameraRig root).")]
    public Transform RigRoot;

    [Tooltip("Your VR camera (CenterEyeAnchor / Main Camera under the rig).")]
    public Transform Head;

    [Tooltip("Tick this on the local player's rig prefab. There should only be one per client.")]
    public bool IsLocalRig = true;

    private void Awake()
    {
        // We only care about the local player's rig on each client.
        if (IsLocalRig)
        {
            if (Local != null && Local != this)
            {
                // Keep the first one; destroy duplicates to avoid ambiguity
                Destroy(gameObject);
                return;
            }
            Local = this;
        }
    }
}
