using Fusion;
using Fusion.Sockets;                  // <- types used by INetworkRunnerCallbacks
using System;
using System.Collections;
using System.Collections.Generic;       // <- for the callbacks interface
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    [Header("Networked player prefab (must have NetworkObject)")]
    public GameObject PlayerPrefab;

    [Header("Local XR rig in scene (optional)")]
    public GameObject LocalXrRigRoot;

    [Header("Lobby spawn point")]
    public Transform LobbySpawnPoint;

    private NetworkObject _localPlayerNO;
    private NetworkRunner _boundRunner;
    private Coroutine _bindCo;
    private INetworkRunnerCallbacks _bridge;



    public void PlayerJoined(PlayerRef player)
    {
        // --- DEBUG cube: mark that PlayerJoined fired ---
        var cube = GameObject.Find("BuildingBlock");
        if (cube) { var r = cube.GetComponent<Renderer>(); if (r) r.material.color = Color.black; }

        if (_boundRunner == null) { Debug.LogWarning("[PlayerSpawner] PlayerJoined but _boundRunner is null."); return; }
        Debug.Log($"[PlayerSpawner] PlayerJoined for {player}. IsServer={_boundRunner.IsServer} Local={_boundRunner.LocalPlayer} Mode={_boundRunner.GameMode}");

        if (PlayerPrefab == null) { Debug.LogError("[PlayerSpawner] PlayerPrefab is NOT assigned."); return; }
        if (!PlayerPrefab.GetComponent<NetworkObject>()) { Debug.LogError("[PlayerSpawner] PlayerPrefab is missing NetworkObject on root."); return; }

        // --------- decide spawn pose (SAFE read of GameStarted) ----------
        var lm = LobbyManager.Instance;
        bool gameStarted = false;
        try { if (lm && lm.Object && lm.Object.IsValid) gameStarted = lm.GameStarted; } catch { gameStarted = false; }

        Vector3 spawnPos; Quaternion spawnRot;
        if (gameStarted)
        {
            var role = lm.GetPlayerRole(player);
            spawnPos = GetSpawnPositionForRole(role);
            spawnRot = GetSpawnRotationForRole(role);
        }
        else if (LobbySpawnPoint)
        {
            spawnPos = LobbySpawnPoint.position;
            spawnRot = LobbySpawnPoint.rotation;
        }
        else
        {
            spawnPos = new Vector3(0, 1, 0);
            spawnRot = Quaternion.identity;
        }

        NetworkObject no = null; // declared once

        // =================== BRANCH BY MODE ===================
        if (_boundRunner.GameMode == GameMode.Shared)
        {
            // In Shared, spawn ONLY the local player; non-local players will map via binder when they replicate
            if (player != _boundRunner.LocalPlayer)
            {
                Debug.Log("[PlayerSpawner] Shared: skipping NON-LOCAL player (binder will map when it replicates).");
            }
            else
            {
                no = _boundRunner.Spawn(PlayerPrefab, spawnPos, spawnRot, player);
                if (no == null)
                {
                    Debug.LogError("[PlayerSpawner] Spawn returned NULL (check Fusion prefab table).");
                }
                else
                {
                    Debug.Log($"[PlayerSpawner] (Shared) Spawned Player NO id={no.Id} for {player} at {spawnPos}");
                    _boundRunner.SetPlayerObject(player, no); // local mapping
                    if (!_boundRunner.TryGetPlayerObject(player, out _))
                        Debug.LogWarning("[PlayerSpawner] SetPlayerObject called but TryGetPlayerObject failed (same frame).");
                }
            }
        }
        else // Host/Client or Server
        {
            if (_boundRunner.IsServer)
            {
                no = _boundRunner.Spawn(PlayerPrefab, spawnPos, spawnRot, player);
                if (no == null)
                {
                    Debug.LogError("[PlayerSpawner] Spawn returned NULL (check Fusion prefab table).");
                }
                else
                {
                    Debug.Log($"[PlayerSpawner] (Host) Spawned Player NO id={no.Id} for {player} at {spawnPos}");
                    _boundRunner.SetPlayerObject(player, no); // host mapping
                    if (!_boundRunner.TryGetPlayerObject(player, out _))
                        Debug.LogWarning("[PlayerSpawner] SetPlayerObject called but TryGetPlayerObject failed (same frame).");
                }
            }
            else
            {
                Debug.Log("[PlayerSpawner] Not server; waiting for host to spawn/map us.");
            }
        }

        // --------- initial visibility (only if we actually spawned here) ----------
        if (no)
        {
            var vis = no.GetComponent<PlayerVisibilityManager>();
            if (vis)
            {
                if (gameStarted)
                    vis.RPC_SetVisibilityState(PlayerVisibilityManager.VisibilityState.Spectator, CharacterRole.Character3);
                else
                    vis.RPC_SetVisibilityState(PlayerVisibilityManager.VisibilityState.Lobby, CharacterRole.None);
            }
        }

        // Local rig link coroutine (as before)
        if (player == _boundRunner.LocalPlayer)
            StartCoroutine(Co_LinkLocalRigWhenReady(player, cube));
    }

    static bool IsSpawned(NetworkBehaviour nb) => nb && nb.Object && nb.Object.IsValid;



    // Link local XR rig to our Player NO when it exists locally
    private IEnumerator Co_LinkLocalRigWhenReady(PlayerRef localPlayer, GameObject debugCube)
    {
        NetworkObject localNO = null;
        float wait = 0f;

        while ((_boundRunner == null || !_boundRunner.TryGetPlayerObject(localPlayer, out localNO)) && wait < 5f)
        {
            yield return null;
            wait += Time.deltaTime;
        }

        if (localNO == null)
        {
            Debug.LogWarning("[PlayerSpawner] Timed out waiting for local PlayerObject.");
            yield break;
        }

        // Debug: white = spawn seen locally
        if (debugCube != null)
        {
            var r = debugCube.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.white;
        }

        // Link XR rig (kept from your version)
        GameObject rig = LocalXrRigRoot;
        if (!rig)
        {
#if UNITY_2023_1_OR_NEWER
        var marker = UnityEngine.Object.FindFirstObjectByType<VRRigMarker>();
#else
            var marker = UnityEngine.Object.FindObjectOfType<VRRigMarker>();
#endif
            if (marker) rig = marker.gameObject;
        }

        if (rig)
        {
            var link = rig.GetComponent<PlayerLink>() ?? rig.AddComponent<PlayerLink>();
            link.PlayerNO = localNO;

            var vrm = rig.GetComponent<VRRigMarker>();
            if (vrm) vrm.IsLocalRig = true;
        }
        else
        {
            Debug.Log("[PlayerSpawner] No local XR rig found/assigned (link step skipped).");
        }
    }

    // Bind/unbind to the active runner
    private void OnEnable()
    {
        _bindCo = StartCoroutine(Co_BindWhenRunnerReady());
    }
    private void OnDisable()
    {
        if (_boundRunner != null && _bridge != null)
            _boundRunner.RemoveCallbacks(_bridge);

        if (_bindCo != null) StopCoroutine(_bindCo);
        _bindCo = null;
        _bridge = null;
        _boundRunner = null;
    }

    // ADD inside PlayerSpawner (anywhere in the class)
    private NetworkRunner PickRunner()
    {
#if UNITY_2023_1_OR_NEWER
    var all = UnityEngine.Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
#else
        var all = UnityEngine.Object.FindObjectsOfType<NetworkRunner>();
#endif
        // 1) prefer enabled & already running
        foreach (var r in all)
            if (r && r.enabled && r.isActiveAndEnabled && r.IsRunning)
                return r;
        // 2) then enabled but not running yet
        foreach (var r in all)
            if (r && r.enabled && r.isActiveAndEnabled)
                return r;
        // 3) fallback to any (avoids null if something is creating one next frame)
        return all.Length > 0 ? all[0] : null;
    }

    // REPLACE your current coroutine with this
    private IEnumerator Co_BindWhenRunnerReady()
    {
        while (_boundRunner == null)
        {
            var r = PickRunner(); // <-- ignore the disabled Meta BB runner
            if (r != null)
            {
                _bridge = new RunnerBridge(this);
                r.AddCallbacks(_bridge);
                _boundRunner = r;
                Debug.Log($"[PlayerSpawner] Bound to runner '{r.name}'; IsRunning={r.IsRunning} IsServer={r.IsServer}");
                yield break;
            }
            yield return null;
        }
    }



    // Adapts INetworkRunnerCallbacks into our existing PlayerJoined/PlayerLeft methods.
    private sealed class RunnerBridge : INetworkRunnerCallbacks
    {
        private readonly PlayerSpawner _owner;
        public RunnerBridge(PlayerSpawner owner) { _owner = owner; }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) => _owner.PlayerJoined(player);
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) => _owner.PlayerLeft(player);

        // Unused callbacks (required by the interface)
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Runner.TryGetPlayerObject(player, out var po) && po.HasStateAuthority)
            Runner.Despawn(po);
    }

    private Vector3 GetSpawnPositionForRole(CharacterRole role)
    {
        var lobbyManager = LobbyManager.Instance;
        if (lobbyManager == null) return new Vector3(0, 1, 0);

        Transform spawnPoint = role switch
        {
            CharacterRole.Character1 => lobbyManager.GetLocationA(),
            CharacterRole.Character2 => lobbyManager.GetLocationB(),
            CharacterRole.Character3 => UnityEngine.Random.Range(0, 2) == 0
                ? lobbyManager.GetLocationC()
                : lobbyManager.GetLocationD(),
            _ => LobbySpawnPoint
        };

        return spawnPoint != null ? spawnPoint.position : new Vector3(0, 1, 0);
    }

    private Quaternion GetSpawnRotationForRole(CharacterRole role)
    {
        var lobbyManager = LobbyManager.Instance;
        if (lobbyManager == null) return Quaternion.identity;

        Transform spawnPoint = role switch
        {
            CharacterRole.Character1 => lobbyManager.GetLocationA(),
            CharacterRole.Character2 => lobbyManager.GetLocationB(),
            CharacterRole.Character3 => UnityEngine.Random.Range(0, 2) == 0
                ? lobbyManager.GetLocationC()
                : lobbyManager.GetLocationD(),
            _ => LobbySpawnPoint
        };

        return spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
    }
}