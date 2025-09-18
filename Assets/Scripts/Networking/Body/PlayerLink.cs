using Fusion;
using UnityEngine;

public class PlayerLink : MonoBehaviour
{
    public NetworkObject PlayerNO; // assign at player spawn time

    // Helper to find the parent PlayerLink from any child (e.g., a hand collider)
    public static PlayerLink FindOn(Transform t)
    {
        return t ? t.GetComponentInParent<PlayerLink>() : null;
    }
}
