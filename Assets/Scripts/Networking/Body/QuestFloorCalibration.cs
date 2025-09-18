using UnityEngine;

/// <summary>
/// Calibrates floor height for Quest users, handling sitting/standing positions.
/// Attach to your VR rig (InteractionRigOVR-Basic).
/// </summary>
public class QuestFloorCalibration : MonoBehaviour
{
    [Header("Floor Calibration")]
    [SerializeField] private bool autoCalibrate = true;
    [SerializeField] private float floorOffsetY = 0f;
    [SerializeField] private float sittingModeOffset = -0.4f; // Adjust for sitting

    [Header("Detection")]
    [SerializeField] private bool assumeSitting = false; // Set true if you're usually sitting
    [SerializeField] private float standingHeightThreshold = 1.3f; // Below this = sitting

    private OVRCameraRig cameraRig;
    private float initialHeight;

    private void Start()
    {
        cameraRig = GetComponentInChildren<OVRCameraRig>();
        if (cameraRig == null)
        {
            cameraRig = FindObjectOfType<OVRCameraRig>();
        }

        if (autoCalibrate)
        {
            CalibrateFloor();
        }
    }

    private void CalibrateFloor()
    {
        if (cameraRig == null) return;

        // Get current head height
        float currentHeadHeight = cameraRig.centerEyeAnchor.position.y;

        // Detect if sitting or standing
        bool isSitting = assumeSitting || (currentHeadHeight < standingHeightThreshold);

        if (isSitting)
        {
            Debug.Log($"[FloorCalibration] Sitting detected. Head at {currentHeadHeight}m, applying offset {sittingModeOffset}");
            ApplyFloorOffset(sittingModeOffset);
        }
        else
        {
            Debug.Log($"[FloorCalibration] Standing detected. Head at {currentHeadHeight}m");
            // For standing, Quest usually calibrates correctly
            ApplyFloorOffset(floorOffsetY);
        }

        // Store initial height for reference
        initialHeight = currentHeadHeight;
    }

    private void ApplyFloorOffset(float offset)
    {
        Vector3 currentPos = transform.position;
        currentPos.y += offset;
        transform.position = currentPos;
    }

    [ContextMenu("Recalibrate Floor")]
    public void RecalibrateFloor()
    {
        // Reset to original position first
        Vector3 pos = transform.position;
        pos.y = 0;
        transform.position = pos;

        // Then calibrate again
        CalibrateFloor();
    }

    [ContextMenu("Force Sitting Mode")]
    public void ForceSittingMode()
    {
        assumeSitting = true;
        RecalibrateFloor();
    }

    [ContextMenu("Force Standing Mode")]
    public void ForceStandingMode()
    {
        assumeSitting = false;
        RecalibrateFloor();
    }

    // Call this when teleporting to maintain floor calibration
    public void TeleportWithFloorCalibration(Vector3 destination)
    {
        // Calculate the floor-relative position
        float currentFloorOffset = transform.position.y - (cameraRig ? cameraRig.centerEyeAnchor.position.y : 0);

        // Apply to destination
        destination.y += currentFloorOffset;
        transform.position = destination;
    }
}