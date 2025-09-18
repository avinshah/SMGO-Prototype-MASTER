using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// On enable (authority only), teleports selected roles/spectators to the given destinations.
/// Useful for quick staging or scene entry without a full SetManager.
/// </summary>
public class TeleportOnEnableByRole : NetworkBehaviour
{
    [Header("Control")]
    [SerializeField] private bool runOnStateAuthorityOnly = true;
    [SerializeField] private bool runOnce = true;

    [Header("Teleport")]
    [SerializeField] private DelayedTeleporter teleporter;
    [SerializeField] private float delaySeconds = 0f;
    [SerializeField] private bool moveChar1 = true;
    [SerializeField] private bool moveChar2 = true;
    [SerializeField] private bool moveSpectators = true;
    [SerializeField] private List<int> spectatorOrdinals = new List<int>(); // empty = all

    [Header("Destinations")]
    [SerializeField] private Transform destChar1;
    [SerializeField] private Transform destChar2;
    [SerializeField] private Transform[] destChar3Ordered;

    private bool _done;

    private void OnEnable()
    {
        if (runOnStateAuthorityOnly && (!Object || !Object.HasStateAuthority)) return;
        if (_done && runOnce) return;
        _done = true;

        if (!teleporter || !LobbyManager.Instance || LobbyManager.Instance.Runner == null) return;

        var runner = LobbyManager.Instance.Runner;

        foreach (var p in runner.ActivePlayers)
        {
            var role = LobbyManager.Instance.GetPlayerRole(p);

            if (role == CharacterRole.Character1 && moveChar1 && destChar1)
                Tele(p, destChar1);
            else if (role == CharacterRole.Character2 && moveChar2 && destChar2)
                Tele(p, destChar2);
        }

        if (moveSpectators)
        {
            var spectators = LobbyManager.Instance.GetSpectatorsOrdered();
            for (int i = 0; i < spectators.Count; i++)
            {
                if (spectatorOrdinals != null && spectatorOrdinals.Count > 0 && !spectatorOrdinals.Contains(i))
                    continue;

                if (i >= (destChar3Ordered?.Length ?? 0)) continue;
                var dst = destChar3Ordered[i];
                if (dst) Tele(spectators[i], dst);
            }
        }

        void Tele(PlayerRef who, Transform dst)
        {
            if (runner.TryGetPlayerObject(who, out var po))
                teleporter.RequestTeleport(po, delaySeconds, dst);
        }
    }
}
