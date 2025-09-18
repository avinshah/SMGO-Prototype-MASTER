using Fusion;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Relay Meta Build Blocks grab/selection events to your InteractableReporter,
/// resolving which player interacted by climbing to a PlayerLink (on the local XR rig).
/// </summary>
public class MetaGrabRelay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reporter on the ROOT/parent (same object as your NetworkObject).")]
    [SerializeField] private InteractableReporter reporter;

    [Tooltip("If true, we count/record immediately on selection by calling reporter.ActivateByPlayer().")]
    [SerializeField] private bool countOnSelect = true;

    [Header("Meta Build Blocks Events")]
    [Tooltip("Wire TouchHandGrabInteractable's 'On Select' (or equivalent) event to this in the Inspector.")]
    public UnityEvent<GameObject> OnSelectedBy;
    [Tooltip("Wire 'On Unselect' here if you need it (optional).")]
    public UnityEvent<GameObject> OnUnselectedBy;

    private void Reset()
    {
        if (!reporter) reporter = GetComponentInParent<InteractableReporter>();
    }

    // Call this from the TouchHandGrabInteractable “On Select” event (passes the interactor/hand GO)
    public void OnSelectedInteractor(GameObject interactorGO)
    {
        if (!reporter) return;

        var playerNO = ResolvePlayerFromInteractor(interactorGO);
        if (!playerNO) return;

        // This is all you need: reporter will use playerNO.InputAuthority → RPC to StateAuthority.
        if (countOnSelect)
            reporter.ActivateByPlayer(playerNO);
    }

    // Optional: call from “On Unselect” if you want to react on release
    public void OnUnselectedInteractor(GameObject interactorGO)
    {
        // No-op by default
    }

    private NetworkObject ResolvePlayerFromInteractor(GameObject interactorGO)
    {
        if (!interactorGO) return null;

        // Preferred: find PlayerLink on/above the hand/controller, then use its PlayerNO
        var link = PlayerLink.FindOn(interactorGO.transform);
        if (link && link.PlayerNO) return link.PlayerNO;

        // Fallback: try to find a NetworkObject on/above the interactor
        return interactorGO.GetComponentInParent<NetworkObject>();
    }
}
