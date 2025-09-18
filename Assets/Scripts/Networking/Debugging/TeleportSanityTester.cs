using Fusion;
using UnityEngine;

public class TeleportSanityTester : SimulationBehaviour
{
    public DelayedTeleporter teleporter;
    public Transform destination;
    public float delay = 0f;

    public override void FixedUpdateNetwork()
    {
        if (Object && Object.HasStateAuthority) // run once on host
        {
            if (teleporter && destination)
            {
                foreach (var p in Runner.ActivePlayers)
                {
                    if (Runner.TryGetPlayerObject(p, out var po))
                    {
                        Debug.Log($"[Tester] Teleport {p} -> {destination.name}");
                        teleporter.RequestTeleport(po, delay, destination);
                    }
                }
                enabled = false; // run once
            }
        }
    }
}
