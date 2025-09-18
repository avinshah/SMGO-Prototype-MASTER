using UnityEngine;
using Oculus.Interaction;
using Meta.XR.MultiplayerBlocks.Shared;
using System.Collections;

/// <summary>
/// Fixes initialization order issues with Meta Building Blocks and Fusion networking.
/// Attach this to your InteractionRigOVR-Basic GameObject.
/// </summary>
public class MetaBlocksInitializationFix : MonoBehaviour
{
    [Header("Delay Settings")]
    [SerializeField] private float initializationDelay = 0.5f;
    [SerializeField] private bool disableUntilNetworkReady = true;

    [Header("Auto-detected Components")]
    [SerializeField] private ActiveStateTracker[] activeStateTrackers;
    [SerializeField] private TransferOwnershipOnSelect[] transferOwnershipComponents;
    [SerializeField] private TouchHandGrabInteractor[] touchGrabInteractors;
    [SerializeField] private InteractorGroup[] interactorGroups;

    private void Awake()
    {
        // Find all Meta interaction components
        CollectComponents();

        if (disableUntilNetworkReady)
        {
            // Temporarily disable Meta components to prevent null refs
            SetMetaComponentsEnabled(false);

            // Re-enable after a delay
            StartCoroutine(DelayedInitialization());
        }
    }

    private void CollectComponents()
    {
        // Find all problematic components in children
        activeStateTrackers = GetComponentsInChildren<ActiveStateTracker>(true);
        transferOwnershipComponents = GetComponentsInChildren<TransferOwnershipOnSelect>(true);
        touchGrabInteractors = GetComponentsInChildren<TouchHandGrabInteractor>(true);
        interactorGroups = GetComponentsInChildren<InteractorGroup>(true);

        Debug.Log($"[MetaBlocksFix] Found {activeStateTrackers.Length} ActiveStateTrackers, " +
                  $"{transferOwnershipComponents.Length} TransferOwnership, " +
                  $"{touchGrabInteractors.Length} TouchGrabInteractors, " +
                  $"{interactorGroups.Length} InteractorGroups");
    }

    private IEnumerator DelayedInitialization()
    {
        // Wait for network to initialize
        yield return new WaitForSeconds(initializationDelay);

        // Wait for NetworkManager to be ready
        while (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("[MetaBlocksFix] Network ready, enabling Meta components");

        // Re-enable Meta components
        SetMetaComponentsEnabled(true);

        // Force refresh on components that need it
        RefreshComponents();
    }

    private void SetMetaComponentsEnabled(bool enabled)
    {
        // Disable/enable components that are causing issues
        foreach (var tracker in activeStateTrackers)
        {
            if (tracker != null) tracker.enabled = enabled;
        }

        foreach (var transfer in transferOwnershipComponents)
        {
            if (transfer != null) transfer.enabled = enabled;
        }

        foreach (var grabber in touchGrabInteractors)
        {
            if (grabber != null) grabber.enabled = enabled;
        }

        foreach (var group in interactorGroups)
        {
            if (group != null) group.enabled = enabled;
        }
    }

    private void RefreshComponents()
    {
        // Force components to re-initialize their references
        foreach (var group in interactorGroups)
        {
            if (group != null)
            {
                // Force a refresh by disabling and re-enabling
                group.enabled = false;
                group.enabled = true;
            }
        }
    }

    /// <summary>
    /// Call this to manually reinitialize if errors persist
    /// </summary>
    [ContextMenu("Force Reinitialize")]
    public void ForceReinitialize()
    {
        SetMetaComponentsEnabled(false);
        StartCoroutine(DelayedInitialization());
    }
}