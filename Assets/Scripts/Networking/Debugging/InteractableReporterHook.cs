using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableReporterHook : MonoBehaviour
{
    [Tooltip("Optional label to show in the HUD feed")]
    public string label = "DebugCube";

    // Hook up from the InteractableReporter inspector:
    // OnThresholdReached -> InteractableReporterHook.OnThresholdReached
    public void OnThresholdReached()
    {
        DebugFeed.Log($"[Touch] {label}: threshold reached");
    }

    // (Optional) if you also wire OnActivated / OnBegin / OnEnd, add these:
    public void OnActivated() { DebugFeed.Log($"[Touch] {label}: activated"); }
    public void OnBegin() { DebugFeed.Log($"[Touch] {label}: begin"); }
    public void OnEnd() { DebugFeed.Log($"[Touch] {label}: end"); }
}
