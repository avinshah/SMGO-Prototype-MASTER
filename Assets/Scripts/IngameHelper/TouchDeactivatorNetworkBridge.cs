using Fusion;
using UnityEngine;

public class TouchDeactivatorNetworkBridge : NetworkBehaviour
{
    [SerializeField] private TouchDeactivator target;

    void Awake() { if (!target) target = GetComponent<TouchDeactivator>(); }

    // Hook this up in the inspector: TouchDeactivator.OnTouch -> OnLocalTouch()
    public void OnLocalTouch()
    {
        if (!target) return;

        if (Object && Object.HasStateAuthority)
            RPC_BroadcastRun();
        else
            RPC_RequestRun();
    }

    // Non-authority asks the LM owner (or any SA holder) to fan-out
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRun()
    {
        RPC_BroadcastRun();
    }

    // Authority tells ALL peers to run the same actions LOCALLY
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastRun()
    {
        if (target) target.RunAsIfTouched();
    }
}
