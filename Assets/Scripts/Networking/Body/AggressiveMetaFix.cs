using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Aggressively fixes all Meta/Oculus Interaction errors by delaying their initialization.
/// Attach to InteractionRigOVR-Basic root.
/// </summary>
public class AggressiveMetaFix : MonoBehaviour
{
    [Header("Fix Settings")]
    [SerializeField] private float initializationDelay = 2f;
    [SerializeField] private bool disableProblematicComponents = true;
    [SerializeField] private bool tagHandColliders = true;

    [Header("Debug")]
    [SerializeField] private List<string> disabledComponentNames = new List<string>();

    private List<MonoBehaviour> _disabledComponents = new List<MonoBehaviour>();

    private void Awake()
    {
        Debug.Log("[AggressiveMetaFix] Starting aggressive fix for Meta components...");

        if (disableProblematicComponents)
        {
            DisableAllProblematicComponents();
        }

        if (tagHandColliders)
        {
            TagHandColliders();
        }

        StartCoroutine(DelayedReinitialization());
    }

    private void DisableAllProblematicComponents()
    {
        // List of all component types that are causing errors
        var problematicTypes = new[]
        {
            "Oculus.Interaction.HandRootOffset",
            "Oculus.Interaction.HandTransformScaler",
            "Oculus.Interaction.Input.FromOVRHmdDataSource",
            "Oculus.Interaction.HandVisual",
            "Oculus.Interaction.Input.FromOVRHandDataSource",
            "Oculus.Interaction.GrabAPI.HandGrabAPI",
            "Oculus.Interaction.HandJoint",
            "Oculus.Interaction.HandTrackingConfidenceProvider",
            "Oculus.Interaction.Input.HandPhysicsCapsules",
            "Oculus.Interaction.HandPinchOffset",
            "Oculus.Interaction.TouchHandGrabInteractor",
            "Oculus.Interaction.TouchHandGrabInteractorVisual",
            "Meta.XR.MultiplayerBlocks.Shared.TransferOwnershipOnSelect"
        };

        // Find and disable all problematic components
        foreach (var typeName in problematicTypes)
        {
            var type = System.Type.GetType(typeName + ", Assembly-CSharp")
                    ?? System.Type.GetType(typeName + ", Oculus.Interaction")
                    ?? System.Type.GetType(typeName + ", Meta.XR.SDK.Interaction")
                    ?? System.Type.GetType(typeName + ", Meta.XR.SDK.Core");

            if (type != null)
            {
                var components = GetComponentsInChildren(type, true);
                foreach (var comp in components)
                {
                    if (comp is MonoBehaviour mb && mb.enabled)
                    {
                        mb.enabled = false;
                        _disabledComponents.Add(mb);
                        disabledComponentNames.Add(mb.GetType().Name + " (" + mb.name + ")");
                        Debug.Log($"[AggressiveMetaFix] Disabled: {mb.GetType().Name} on {mb.name}");
                    }
                }
            }
        }

        // Also disable by finding components that have these errors
        DisableComponentsByName("HandRootOffset");
        DisableComponentsByName("HandTransformScaler");
        DisableComponentsByName("FromOVRHmdDataSource");
        DisableComponentsByName("HandVisual");
        DisableComponentsByName("FromOVRHandDataSource");
        DisableComponentsByName("HandGrabAPI");
        DisableComponentsByName("HandJoint");
        DisableComponentsByName("HandTrackingConfidenceProvider");
        DisableComponentsByName("HandPhysicsCapsules");
        DisableComponentsByName("HandPinchOffset");
        DisableComponentsByName("TouchHandGrabInteractor");
        DisableComponentsByName("TransferOwnershipOnSelect");

        Debug.Log($"[AggressiveMetaFix] Disabled {_disabledComponents.Count} components total");
    }

    private void DisableComponentsByName(string componentName)
    {
        var allComponents = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var comp in allComponents)
        {
            if (comp != null && comp.GetType().Name.Contains(componentName))
            {
                if (comp.enabled && !_disabledComponents.Contains(comp))
                {
                    comp.enabled = false;
                    _disabledComponents.Add(comp);
                    disabledComponentNames.Add(comp.GetType().Name + " (" + comp.name + ")");
                }
            }
        }
    }

    private void TagHandColliders()
    {
        // Make sure "Hand" tag exists
        if (!TagExists("Hand"))
        {
            Debug.LogWarning("[AggressiveMetaFix] 'Hand' tag not found. Please create it in Project Settings > Tags");
            return;
        }

        // Find and tag all hand colliders
        var handObjects = new[]
        {
            "OVRLeftHandVisual",
            "OVRRightHandVisual",
            "LeftHandAnchor",
            "RightHandAnchor",
            "LeftControllerAnchor",
            "RightControllerAnchor"
        };

        foreach (var handName in handObjects)
        {
            var handObj = transform.Find(handName) ?? GameObject.Find(handName)?.transform;
            if (handObj != null)
            {
                var colliders = handObj.GetComponentsInChildren<Collider>(true);
                foreach (var col in colliders)
                {
                    if (col.isTrigger)
                    {
                        col.gameObject.tag = "Hand";
                        Debug.Log($"[AggressiveMetaFix] Tagged {col.name} as Hand");
                    }
                }
            }
        }

        // Also tag anything with HandPhysicsCapsules
        var capsules = GetComponentsInChildren<Collider>(true)
            .Where(c => c.name.Contains("Capsule") || c.name.Contains("Hand"))
            .Where(c => c.isTrigger);

        foreach (var capsule in capsules)
        {
            capsule.gameObject.tag = "Hand";
        }
    }

    private IEnumerator DelayedReinitialization()
    {
        // Wait for everything to initialize
        yield return new WaitForSeconds(initializationDelay);

        // Wait for network to be ready
        while (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("[AggressiveMetaFix] Network ready, attempting to re-enable components...");

        // Try to re-enable components in small batches
        int batchSize = 5;
        int enabledCount = 0;

        for (int i = 0; i < _disabledComponents.Count; i += batchSize)
        {
            var batch = _disabledComponents.Skip(i).Take(batchSize);

            foreach (var comp in batch)
            {
                if (comp != null)
                {
                    try
                    {
                        // Skip components that are known to be very problematic
                        if (comp.GetType().Name.Contains("TouchHandGrabInteractorVisual") ||
                            comp.GetType().Name.Contains("TransferOwnershipOnSelect"))
                        {
                            Debug.Log($"[AggressiveMetaFix] Keeping {comp.GetType().Name} disabled permanently");
                            continue;
                        }

                        comp.enabled = true;
                        enabledCount++;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[AggressiveMetaFix] Failed to re-enable {comp.GetType().Name}: {e.Message}");
                        comp.enabled = false;
                    }
                }
            }

            // Wait between batches to let them initialize
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log($"[AggressiveMetaFix] Re-enabled {enabledCount} components successfully");

        // Final check - disable any that are still throwing errors
        yield return new WaitForSeconds(0.5f);
        DisableStillBrokenComponents();
    }

    private void DisableStillBrokenComponents()
    {
        // These are known to be consistently problematic
        var stillBrokenTypes = new[]
        {
            "TouchHandGrabInteractorVisual",
            "TransferOwnershipOnSelect",
            "HandRootOffset",
            "HandTransformScaler"
        };

        foreach (var typeName in stillBrokenTypes)
        {
            DisableComponentsByName(typeName);
        }
    }

    private bool TagExists(string tag)
    {
        try
        {
            GameObject.FindWithTag(tag);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [ContextMenu("Force Disable All Problematic")]
    public void ForceDisableAll()
    {
        DisableAllProblematicComponents();
    }

    [ContextMenu("List All Disabled Components")]
    public void ListDisabledComponents()
    {
        Debug.Log($"[AggressiveMetaFix] Total disabled: {disabledComponentNames.Count}");
        foreach (var name in disabledComponentNames)
        {
            Debug.Log($"  - {name}");
        }
    }
}