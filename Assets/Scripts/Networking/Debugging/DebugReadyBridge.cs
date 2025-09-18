using Fusion;
using UnityEngine;

public class DebugReadyBridge : MonoBehaviour
{
    public void ToggleLocalReady()
    {
        var lm = LobbyManager.Instance;
        if (!lm || lm.Runner == null || lm.Runner.LocalPlayer == PlayerRef.None)
        {
            Debug.LogWarning("[DebugReadyBridge] No local player yet."); return;
        }
        lm.RPC_RequestReadyToggle(lm.Runner.LocalPlayer);
    }
}
