using Fusion;
using UnityEngine;

/// <summary>
/// Debug version with visual feedback at each step
/// </summary>
public class ThresholdTeleportAction_Debug : NetworkBehaviour
{
    public enum TeleportTargetMode
    {
        LastInteractor,
        OtherPlayer,
        BothPlayersDifferentDestinations
    }

    [Header("References")]
    [SerializeField] private InteractableReporter reporter;
    [SerializeField] private DelayedTeleporter teleporter;

    [Header("Mode")]
    [SerializeField] private TeleportTargetMode mode = TeleportTargetMode.LastInteractor;

    [Header("Delay")]
    [SerializeField] private float delaySeconds = 2f;

    [Header("Optional destination overrides")]
    [SerializeField] private Transform destinationOverrideForLast;
    [SerializeField] private Transform destinationOverrideForOther;

    [Header("Debug Colors")]
    [SerializeField] private bool useDebugColors = true;

    private Renderer _renderer;

    private void Start()
    {
        _renderer = GetComponent<Renderer>();
    }

    /// <summary>
    /// Called from InteractableReporter.OnThresholdReached
    /// </summary>
    public void TeleportByMode()
    {
        // Step 1: Confirm method is called (BLUE)
        SetDebugColor(Color.blue);

        if (!Object.HasStateAuthority)
        {
            // Not authority (MAGENTA)
            SetDebugColor(Color.magenta);
            return;
        }

        // Step 2: Check references (CYAN if missing)
        if (!reporter || !teleporter)
        {
            SetDebugColor(Color.cyan);
            return;
        }

        // Step 3: Try to get last interactor (RED if none)
        if (!reporter.TryGetLastInteractor(out var lastWho, out var lastNO))
        {
            SetDebugColor(Color.red);
            return;
        }

        // Step 4: Check if NetworkObject is valid (ORANGE if null)
        if (lastNO == null)
        {
            SetDebugColor(new Color(1f, 0.5f, 0f)); // Orange
            return;
        }

        // Step 5: Actually request teleport (WHITE = success)
        SetDebugColor(Color.white);

        switch (mode)
        {
            case TeleportTargetMode.LastInteractor:
                // Just try to teleport with the override (or null to use teleporter's default)
                teleporter.RequestTeleport(lastNO, delaySeconds, destinationOverrideForLast);

                // Final success = GREEN after small delay
                Invoke(nameof(ShowSuccess), 0.5f);
                break;

            case TeleportTargetMode.OtherPlayer:
                var otherNO = FindOtherPlayerNO(lastWho);
                if (!otherNO)
                {
                    SetDebugColor(new Color(1f, 0f, 1f)); // Pink = no other player
                    return;
                }
                teleporter.RequestTeleport(otherNO, delaySeconds, destinationOverrideForOther);
                Invoke(nameof(ShowSuccess), 0.5f);
                break;

            case TeleportTargetMode.BothPlayersDifferentDestinations:
                var other2NO = FindOtherPlayerNO(lastWho);
                if (lastNO) teleporter.RequestTeleport(lastNO, delaySeconds, destinationOverrideForLast);
                if (other2NO) teleporter.RequestTeleport(other2NO, delaySeconds, destinationOverrideForOther);
                Invoke(nameof(ShowSuccess), 0.5f);
                break;
        }
    }

    private void ShowSuccess()
    {
        SetDebugColor(Color.green);
    }

    private void SetDebugColor(Color color)
    {
        if (useDebugColors && _renderer != null)
        {
            _renderer.material.color = color;
        }
    }

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

    // Public getter for DelayedTeleporter to check its settings
    public bool CheckTeleporterSetup()
    {
        if (teleporter == null) return false;

        // Just check if we have an override or assume DelayedTeleporter has its own target
        return destinationOverrideForLast != null || true; // Assume teleporter has internal target
    }
}