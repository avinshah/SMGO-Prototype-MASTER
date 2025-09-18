using UnityEngine;
using System;
using System.Collections;
using System.Linq;

/// <summary>
/// Fixes Meta Building Blocks hand errors and tags hand colliders.
/// Movement/teleportation is now handled by VRRigSpawnManager.
/// </summary>
public class SimpleVRLobbyFix : MonoBehaviour
{
    [Header("General")]
    [Tooltip("Run the hand fixes shortly after play to allow other systems to initialize.")]
    [SerializeField] private float initDelay = 0.5f;

    [Header("Hand Error Workarounds")]
    [SerializeField] private bool fixHandErrors = true;

    [Tooltip("Full type names that will be disabled if found (by reflection).")]
    [SerializeField]
    private string[] problematicTypesToDisable =
    {
        "Oculus.Interaction.TouchHandGrabInteractorVisual",
        "Meta.XR.MultiplayerBlocks.Shared.TransferOwnershipOnSelect"
    };

    [Header("Hand Colliders Tag/Layer")]
    [SerializeField] private bool tagHandColliders = true;

    [Tooltip("Will warn if Tag does not exist (cannot create tags at runtime).")]
    [SerializeField] private string handTag = "Hand";

    [Tooltip("Optional: also set a dedicated layer for hand colliders (set to -1 to skip).")]
    [SerializeField] private int handLayer = -1; // e.g., LayerMask.NameToLayer("Hand")

    [Header("Deprecated (Owned by VRRigSpawnManager)")]
    [SerializeField] private bool performInitialTeleport = false; // kept for backward compatibility

    private bool _applied;

    private void Start()
    {
        StartCoroutine(ApplyFixesOnce());
    }

    private IEnumerator ApplyFixesOnce()
    {
        if (_applied) yield break;
        _applied = true;

        if (performInitialTeleport)
            Debug.LogWarning("[SimpleVRLobbyFix] Teleportation is disabled here. Use VRRigSpawnManager for positioning.");

        // Let other systems (rig spawn / hand prefabs) appear
        if (initDelay > 0f)
            yield return new WaitForSeconds(initDelay);

        Debug.Log("[SimpleVRLobbyFix] Starting hand fixes...");

        if (fixHandErrors)
            DisableProblematicComponents();

        if (tagHandColliders)
            TagAllHandTriggers();

        Debug.Log("[SimpleVRLobbyFix] Hand fixes applied!");
    }

    // ---------------------------
    // Disable specific components
    // ---------------------------
    private void DisableProblematicComponents()
    {
        // We avoid hard references to Meta/Oculus types to keep this script compilable without those packages.
        var allBehaviours = FindObjectsOfType<MonoBehaviour>(true);

        foreach (var fullName in problematicTypesToDisable)
        {
            int disabledCount = 0;

            foreach (var mb in allBehaviours)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (!StringEquals(t.FullName, fullName)) continue;

                var compBehaviour = mb as Behaviour;
                if (compBehaviour && compBehaviour.enabled)
                {
                    compBehaviour.enabled = false;
                    disabledCount++;
                }
            }

            if (disabledCount > 0)
                Debug.Log($"[SimpleVRLobbyFix] Disabled '{fullName}' on {disabledCount} object(s).");
        }
    }

    // ---------------------------
    // Tag (and optionally layer) hand trigger colliders
    // ---------------------------
    private void TagAllHandTriggers()
    {
        // Warn if tag does not exist (cannot create at runtime)
        if (!TagExists(handTag))
        {
            Debug.LogWarning($"[SimpleVRLobbyFix] Tag '{handTag}' not found. Create it in Project Settings > Tags & Layers. Tagging will be skipped.");
            return;
        }

        // Known hand root names commonly used by Meta rigs
        string[] likelyHandRoots =
        {
            "OVRLeftHand", "OVRRightHand",
            "OVRLeftHandVisual", "OVRRightHandVisual",
            "LeftHand", "RightHand",
            "LeftHandAnchor", "RightHandAnchor"
        };

        int tagged = 0;

        // 1) Targeted pass: search under likely hand roots
        foreach (var rootName in likelyHandRoots)
        {
            var go = GameObject.Find(rootName);
            if (!go) continue;

            tagged += TagTriggersUnder(go.transform);
        }

        // 2) If that was too few, do a broader pass over scene colliders that look like hand parts
        if (tagged < 2)
        {
            var allCols = FindObjectsOfType<Collider>(true);
            foreach (var col in allCols)
            {
                if (!col || !col.isTrigger) continue;

                if (LooksLikeHand(col.transform))
                {
                    ApplyTagAndLayer(col.gameObject);
                    tagged++;
                }
            }
        }

        Debug.Log($"[SimpleVRLobbyFix] Tagged {tagged} hand trigger collider(s) with '{handTag}'{(handLayer >= 0 ? $" and set layer {handLayer}" : "")}.");
    }

    private int TagTriggersUnder(Transform root)
    {
        int count = 0;
        var colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (!col || !col.isTrigger) continue;
            ApplyTagAndLayer(col.gameObject);
            count++;
        }
        return count;
    }

    private void ApplyTagAndLayer(GameObject go)
    {
        if (!go) return;
        go.tag = handTag;
        if (handLayer >= 0) go.layer = handLayer;
    }

    // ---------------------------
    // Helpers
    // ---------------------------
    private static bool StringEquals(string a, string b)
    {
        return string.Equals(a, b, StringComparison.Ordinal);
    }

    private static bool TagExists(string tag)
    {
        // FindWithTag throws if the tag does not exist; returns null if it exists but no object has it.
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

    private static bool LooksLikeHand(Transform t)
    {
        if (!t) return false;
        // Heuristics: common substrings in Meta hand capsule/bone colliders
        string n = t.name.ToLowerInvariant();
        return n.Contains("hand") || n.Contains("finger") || n.Contains("palm") || n.Contains("bone") || n.Contains("capsule");
    }

    [ContextMenu("Manual Fix Hand Errors")]
    public void ManualFixHandErrors()
    {
        DisableProblematicComponents();
        TagAllHandTriggers();
    }
}
