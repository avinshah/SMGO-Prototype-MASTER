using Fusion;
using UnityEngine;


public sealed class PlayerObjectBinder : NetworkBehaviour
{
    public override void Spawned()
    {
        Debug.Log($"[Binder] Spawned on {Runner.name} for {Object.InputAuthority}  NO={Object}");
        if (Object && Runner != null)
            Runner.SetPlayerObject(Object.InputAuthority, Object);
    }
}