using UnityEngine;

public class DebugForceStartBridge : MonoBehaviour
{
    // Call this from InteractableReporter.OnThresholdReached to skip gating completely
    public void ForceStart()
    {
        if (!LobbyManager.Instance) { Debug.LogWarning("[DebugForceStartBridge] No LobbyManager"); return; }
        LobbyManager.Instance.Debug_ForceStartOnHost(); // calls StartGame() directly on StateAuthority
    }
}
