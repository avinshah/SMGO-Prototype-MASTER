using Oculus.Interaction;
using Oculus.Interaction.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixes hand initialization issues and ensures proper tagging.
/// Attach to InteractionRigOVR-Basic root.
/// </summary>
public class HandSetupFix : MonoBehaviour
{
    [Header("Auto Setup")]
    [SerializeField] private bool autoTagHandColliders = true;
    [SerializeField] private bool disableProblematicComponents = true;
    [SerializeField] private float initDelay = 1f;

    [Header("Hand References (Auto-detected if empty)")]
    [SerializeField] private GameObject leftHandRoot;
    [SerializeField] private GameObject rightHandRoot;

    private void Awake()
    {
        // Find hand roots if not assigned
        if (leftHandRoot == null)
        {
            leftHandRoot = GameObject.Find("LeftControllerAnchor");
            if (leftHandRoot == null)
                leftHandRoot = transform.Find("OVRCameraRig/TrackingSpace/LeftHandAnchor")?.gameObject;
        }

        if (rightHandRoot == null)
        {
            rightHandRoot = GameObject.Find("RightControllerAnchor");
            if (rightHandRoot == null)
                rightHandRoot = transform.Find("OVRCameraRig/TrackingSpace/RightHandAnchor")?.gameObject;
        }

        StartCoroutine(FixHandSetup());
    }

    private IEnumerator FixHandSetup()
    {
        Debug.Log("[HandSetupFix] Starting hand setup fix...");

        // Step 1: Disable problematic components immediately
        if (disableProblematicComponents)
        {
            DisableProblematicComponents();
        }

        // Step 2: Tag hand colliders
        if (autoTagHandColliders)
        {
            TagHandColliders();
        }

        // Step 3: Wait for initialization
        yield return new WaitForSeconds(initDelay);

        // Step 4: Re-enable and fix references
        FixHandReferences();

        // Step 5: Re-enable components
        if (disableProblematicComponents)
        {
            yield return new WaitForSeconds(0.5f);
            EnableComponents();
        }

        Debug.Log("[HandSetupFix] Hand setup complete!");
    }

    private void DisableProblematicComponents()
    {
        // Disable TouchHandGrabInteractorVisual components
        var visualComponents = GetComponentsInChildren<TouchHandGrabInteractorVisual>(true);
        foreach (var comp in visualComponents)
        {
            comp.enabled = false;
            Debug.Log($"[HandSetupFix] Disabled {comp.name} TouchHandGrabInteractorVisual");
        }

        // Disable TouchHandGrabInteractor components temporarily
        var interactors = GetComponentsInChildren<TouchHandGrabInteractor>(true);
        foreach (var comp in interactors)
        {
            comp.enabled = false;
        }

        // Find and disable TransferOwnershipOnSelect in scene
        var transferComps = FindObjectsOfType<Meta.XR.MultiplayerBlocks.Shared.TransferOwnershipOnSelect>(true);
        foreach (var comp in transferComps)
        {
            comp.enabled = false;
        }
    }

    private void TagHandColliders()
    {
        // Create Hand tag if it doesn't exist
        if (!TagExists("Hand"))
        {
            Debug.LogWarning("[HandSetupFix] 'Hand' tag doesn't exist. Please create it in Project Settings > Tags");
        }

        // Tag left hand colliders
        if (leftHandRoot != null)
        {
            TagHandCollidersRecursive(leftHandRoot, "Hand");

            // Specifically look for HandPhysicsCapsules
            var leftHandVisual = leftHandRoot.GetComponentInChildren<OVRHand>();
            if (leftHandVisual != null)
            {
                var capsules = leftHandVisual.GetComponentsInChildren<CapsuleCollider>(true);
                foreach (var capsule in capsules)
                {
                    capsule.gameObject.tag = "Hand";
                    capsule.isTrigger = true; // Ensure they're triggers
                    Debug.Log($"[HandSetupFix] Tagged left hand capsule: {capsule.name}");
                }
            }
        }

        // Tag right hand colliders
        if (rightHandRoot != null)
        {
            TagHandCollidersRecursive(rightHandRoot, "Hand");

            var rightHandVisual = rightHandRoot.GetComponentInChildren<OVRHand>();
            if (rightHandVisual != null)
            {
                var capsules = rightHandVisual.GetComponentsInChildren<CapsuleCollider>(true);
                foreach (var capsule in capsules)
                {
                    capsule.gameObject.tag = "Hand";
                    capsule.isTrigger = true;
                    Debug.Log($"[HandSetupFix] Tagged right hand capsule: {capsule.name}");
                }
            }
        }
    }

    private void TagHandCollidersRecursive(GameObject root, string tag)
    {
        // Find all colliders in children
        var colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            // Skip non-trigger colliders (those are for physics)
            if (!col.isTrigger) continue;

            // Tag anything that looks like a hand collider
            if (col.name.ToLower().Contains("hand") ||
                col.name.ToLower().Contains("capsule") ||
                col.name.ToLower().Contains("finger") ||
                col.name.ToLower().Contains("palm"))
            {
                col.gameObject.tag = tag;
                Debug.Log($"[HandSetupFix] Tagged {col.name} as {tag}");
            }
        }
    }

    private void FixHandReferences()
    {
        // Fix TouchHandGrabInteractor references
        var interactors = GetComponentsInChildren<TouchHandGrabInteractor>(true);
        foreach (var interactor in interactors)
        {
            // Find the Hand component it should reference
            var hand = interactor.GetComponentInParent<HandRef>();
            if (hand != null)
            {
                // Use reflection to set private fields if needed
                var handField = interactor.GetType().GetField("_hand",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (handField != null && handField.GetValue(interactor) == null)
                {
                    handField.SetValue(interactor, hand);
                    Debug.Log($"[HandSetupFix] Fixed hand reference for {interactor.name}");
                }
            }
        }
    }

    private void EnableComponents()
    {
        // Re-enable TouchHandGrabInteractor
        var interactors = GetComponentsInChildren<TouchHandGrabInteractor>(true);
        foreach (var comp in interactors)
        {
            comp.enabled = true;
        }

        // Re-enable TouchHandGrabInteractorVisual
        var visualComponents = GetComponentsInChildren<TouchHandGrabInteractorVisual>(true);
        foreach (var comp in visualComponents)
        {
            comp.enabled = true;
        }

        Debug.Log("[HandSetupFix] Re-enabled hand components");
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

    [ContextMenu("Force Retag Hands")]
    public void ForceRetagHands()
    {
        TagHandColliders();
    }
}