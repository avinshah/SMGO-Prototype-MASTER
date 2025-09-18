using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpotlightAligner : MonoBehaviour
{
    public GameObject spotlightGameObject; // Spotlight GameObject
    public Transform skewingHandle; // The skewing handle of the VLB
    public Transform vlbOrigin; // The VLB origin (base of the cone)
    public float planeHeight = 0f; // Y-coordinate of the floor/target plane

    void LateUpdate()
    {
        if (spotlightGameObject == null || skewingHandle == null || vlbOrigin == null) return;

        // Calculate the skew offset in world space
        Vector3 skewOffset = skewingHandle.position - vlbOrigin.position;

        // Project the beam's target point onto the plane
        Vector3 beamDirection = vlbOrigin.forward + new Vector3(skewOffset.x, skewOffset.y, skewOffset.z).normalized;
        float distanceToPlane = (planeHeight - vlbOrigin.position.y) / beamDirection.y;
        Vector3 targetPoint = vlbOrigin.position + beamDirection * distanceToPlane;

        // Update spotlight position and rotation
        Transform spotlightTransform = spotlightGameObject.transform;
        spotlightTransform.rotation = Quaternion.LookRotation(targetPoint - spotlightTransform.position, Vector3.up);
    }
}
