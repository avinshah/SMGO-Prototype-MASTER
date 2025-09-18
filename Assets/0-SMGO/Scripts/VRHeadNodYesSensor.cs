using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Events;

public class VRHeadNodYesSensor : MonoBehaviour
{
    [SerializeField] private AudioSource audioYes;
    [SerializeField] private int nodCountRequired = 6;
    [SerializeField] private float nodAngularRequirement = 2.0f;
    [SerializeField] private float nodTimingRequirement = 0.75f;
    [SerializeField] private UnityEvent onNodYes;

    private float nodInProgress;
    private float lastSignificantNodAngle;
    private int lastDigitalNod;
    private int nodCount;

    void Update()
    {
        UpdateNodYes();
    }

    void UpdateNodYes()
    {
        if (nodInProgress > 0)
        {
            nodInProgress -= Time.deltaTime;
            if (nodInProgress <= 0)
            {
                nodCount = 0;
                lastDigitalNod = 0;
            }
        }

        InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRotation))
            return;

        Vector3 forward = headRotation * Vector3.forward;
        float angle = Mathf.Asin(forward.y) * Mathf.Rad2Deg;

        int nod = 0;
        if (angle < lastSignificantNodAngle - nodAngularRequirement)
        {
            nod = -1;
            lastSignificantNodAngle = angle;
        }
        else if (angle > lastSignificantNodAngle + nodAngularRequirement)
        {
            nod = 1;
            lastSignificantNodAngle = angle;
        }

        if (nod != 0 && nod != lastDigitalNod)
        {
            lastDigitalNod = nod;
            nodCount++;
            nodInProgress = nodTimingRequirement;

            if (nodCount >= nodCountRequired)
            {
                nodCount = 0;
                Debug.Log("Yes!");
                if (audioYes) audioYes.Play();
                onNodYes?.Invoke(); // Trigger UnityEvent
            }
        }
    }
}
