using Fusion;
using UnityEngine;

/// <summary>
/// Hook this into InteractableReporter.OnThresholdReached (UnityEvent).
/// Decides WHO to teleport; WHERE is either DelayedTeleporter.defaultTarget (when override is null)
/// or a per-player override you provide below.
/// </summary>
public class ThresholdTeleportAction : NetworkBehaviour
{
    public enum TeleportTargetMode
    {
        LastInteractor,
        OtherPlayer,
        BothPlayersDifferentDestinations
    }

    [Header("References")]
    [SerializeField] private InteractableReporter reporter;   // required
    [SerializeField] private DelayedTeleporter teleporter;    // required

    [Header("Mode")]
    [SerializeField] private TeleportTargetMode mode = TeleportTargetMode.LastInteractor;

    [Header("Delay")]
    [SerializeField] private float delaySeconds = 2f;

    [Header("Optional destination overrides")]
    [Tooltip("If left empty, DelayedTeleporter will use its own defaultTarget for the LAST interactor.")]
    [SerializeField] private Transform destinationOverrideForLast;
    [Tooltip("If left empty, DelayedTeleporter will use its own defaultTarget for the OTHER player.")]
    [SerializeField] private Transform destinationOverrideForOther;

    /// <summary>
    /// Call this (no args) from InteractableReporter.OnThresholdReached.
    /// Runs on StateAuthority (the reporter fires UnityEvent on authority).
    /// </summary>
    public void TeleportByMode()
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[ThresholdTeleportAction] Must be invoked on StateAuthority.");
            return;
        }

        if (!reporter || !teleporter)
        {
            Debug.LogWarning("[ThresholdTeleportAction] Missing reporter or teleporter.");
            return;
        }

        if (!reporter.TryGetLastInteractor(out var lastWho, out var lastNO))
        {
            Debug.LogWarning("[ThresholdTeleportAction] No last interactor cached.");
            return;
        }
        var lm = LobbyManager.Instance;
        if (!lm || !lm.Object || !lm.Object.HasStateAuthority)
        {
            Debug.LogWarning("[ThresholdTeleportAction] Needs LM StateAuthority to broadcast.");
            return;
        }

        switch (mode)
        {
            case TeleportTargetMode.LastInteractor:
                if (lastNO && destinationOverrideForLast)
                    lm.RPC_TeleportToPose(lastWho, destinationOverrideForLast.position, destinationOverrideForLast.rotation);
                break;

            case TeleportTargetMode.OtherPlayer:
                var otherNO = FindOtherPlayerNO(lastWho);
                if (otherNO && destinationOverrideForOther)
                    lm.RPC_TeleportToPose(otherNO.InputAuthority, destinationOverrideForOther.position, destinationOverrideForOther.rotation);
                break;

            case TeleportTargetMode.BothPlayersDifferentDestinations:
                var other2NO = FindOtherPlayerNO(lastWho);
                if (lastNO && destinationOverrideForLast)
                    lm.RPC_TeleportToPose(lastWho, destinationOverrideForLast.position, destinationOverrideForLast.rotation);
                if (other2NO && destinationOverrideForOther)
                    lm.RPC_TeleportToPose(other2NO.InputAuthority, destinationOverrideForOther.position, destinationOverrideForOther.rotation);
                break;
        }
    }

    /// <summary>Find the other player's NetworkObject (authority view).</summary>
    private NetworkObject FindOtherPlayerNO(PlayerRef lastWho)
    {
        if (Runner == null) return null;

        foreach (var p in Runner.ActivePlayers)
        {
            if (p == lastWho) continue;
            if (Runner.TryGetPlayerObject(p, out var po)) return po;
        }

        return null;
    }
}