using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Events;

public class VRHeadShakeNoSensor : MonoBehaviour
{
    [SerializeField] private AudioSource audioNo;
    [SerializeField] private int shakeCountRequired = 6;
    [SerializeField] private float shakeAngularRequirement = 2.0f;
    [SerializeField] private float shakeTimingRequirement = 0.5f;
    [SerializeField] private UnityEvent onNodNo;

    private float shakeInProgress;
    private float lastSignificantShakeAngle;
    private int lastDigitalShake;
    private int shakeCount;

    void Update()
    {
        UpdateShakeNo();
    }

    void UpdateShakeNo()
    {
        if (shakeInProgress > 0)
        {
            shakeInProgress -= Time.deltaTime;
            if (shakeInProgress <= 0)
            {
                shakeCount = 0;
                lastDigitalShake = 0;
            }
        }

        InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRotation))
            return;

        Vector3 forward = headRotation * Vector3.forward;
        float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        int shake = 0;
        if (angle < lastSignificantShakeAngle - shakeAngularRequirement)
        {
            shake = -1;
            lastSignificantShakeAngle = angle;
        }
        else if (angle > lastSignificantShakeAngle + shakeAngularRequirement)
        {
            shake = 1;
            lastSignificantShakeAngle = angle;
        }

        if (shake != 0 && shake != lastDigitalShake)
        {
            lastDigitalShake = shake;
            shakeCount++;
            shakeInProgress = shakeTimingRequirement;

            if (shakeCount >= shakeCountRequired)
            {
                shakeCount = 0;
                Debug.Log("No!");
                if (audioNo) audioNo.Play();
                onNodNo?.Invoke(); // Trigger UnityEvent

            }
        }
    }
}
