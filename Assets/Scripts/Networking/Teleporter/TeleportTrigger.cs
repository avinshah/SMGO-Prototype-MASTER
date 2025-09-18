using Fusion;
using UnityEngine;

public class TeleportTrigger : MonoBehaviour
{
    [SerializeField] private DelayedTeleporter teleporter;
    [SerializeField] private float delaySeconds = 2f;
    [SerializeField] private Transform destinationOverride; // optional

    private void OnTriggerEnter(Collider other)
    {
        if (!teleporter) return;

        // Find the NetworkObject belonging to the PlayerPrefab (not the local XR rig)
        NetworkObject playerNO = other.GetComponentInParent<NetworkObject>();
        if (!playerNO && other.attachedRigidbody)
            playerNO = other.attachedRigidbody.GetComponentInParent<NetworkObject>();
        if (!playerNO)
            playerNO = other.transform.root.GetComponent<NetworkObject>();

        if (playerNO != null)
            teleporter.RequestTeleport(playerNO, delaySeconds, destinationOverride);
    }
}
