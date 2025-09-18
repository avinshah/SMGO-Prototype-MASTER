using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using URandom = UnityEngine.Random;

public enum CharacterRole
{
    None = 0,
    Character1 = 1,
    Character2 = 2,
    Character3 = 3
}

[System.Serializable]
public struct PlayerLobbyState : INetworkStruct
{
    public PlayerRef Player;
    public CharacterRole Role;
    public NetworkBool IsReady;
    public int StatusCubeIndex;

    public readonly bool IsValid => Player != PlayerRef.None;
}

public class LobbyManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static LobbyManager Instance { get; private set; }

    // -----------------------------
    // Inspector
    // -----------------------------

    [Header("Spawn Points")]
    [SerializeField] private Transform statusCubeSpawnPoint;
    [SerializeField] private Transform gameStartCubePosition; // ensure assigned in Inspector

    [Header("Parents / Prefabs")]
    [SerializeField] private Transform statusCubeParent;
    [SerializeField] private GameObject statusCubePrefab;
    [SerializeField] private GameObject readyCubePrefab;
    [SerializeField] private GameObject gameStartCubePrefab;

    [Header("Character Cubes (pre-placed)")]
    [SerializeField] private GameObject characterCube1;
    [SerializeField] private GameObject characterCube2;
    [SerializeField] private GameObject characterCube3;

 
    public enum StackAxis { X, Y, Z }   
    [Header("Status Column Layout")]
    [SerializeField] private StackAxis statusStackAxis = StackAxis.Y;
    [SerializeField] private bool statusGrowNegative = false;
    [Tooltip("If > 0, overrides auto spacing (center-to-center). Otherwise measured from cube bounds.")]
    [SerializeField] private float statusCubeDistanceOverride = 0f;

    [Header("Ready Cube (local)")]
    [SerializeField] private bool useFixedReadyPositions = false;
    [SerializeField] private Transform[] readyCubePositions;

    [Header("Game Start Destinations")]
    [SerializeField] private Transform locationA; // Char1
    [SerializeField] private Transform locationB; // Char2
    [SerializeField] private Transform locationC; // Char3 near A
    [SerializeField] private Transform locationD; // Char3 near B

    [Header("Colours (fallbacks if controller missing)")]
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.red;
    [SerializeField] private Color selectedColor = new Color(1f, 0.64f, 0f); // amber
    [SerializeField] private Color occupiedColor = Color.yellow;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color character3SelectColor = Color.cyan;

    // ----- Local-only colors for Character 3 -----
    [SerializeField] private Color character3IdleColor = Color.white;                 // "not selected"
    [SerializeField] private Color character3SelectedColor = new Color(0.20f, 0.60f, 1f); // "you selected Char3"

    // LobbyManager.cs  (inside your NetworkBehaviour)
    [SerializeField] private SetManager[] setSequence;  // assign Set 1 at index 0 in Inspector
    [SerializeField] private bool startSet1OnGameStart = true;




    [Header("Events")]
    public UnityEvent OnGameStart;
    public UnityEvent<PlayerRef, CharacterRole> OnPlayerRoleChanged;

    // -------------- Test Mode --------------
    [System.Serializable]
    public class SimPlayerConfig
    {
        public bool present = false;
        public CharacterRole role = CharacterRole.None; // None=RED, Char1/2=AMBER (flash), Char3=AMBER
        public bool ready = false;                      // true=GREEN
        public string label = "Sim P";
        public bool isPresent => present;  // alias for old code
        public bool isReady => ready;    // alias for old code
    }

    [Header("Test Mode (In-Editor Debug)")]
    [SerializeField] private bool enableTestMode = false;
    [Tooltip("Create visual status cubes for simulated players so you can see the full column.")]
    [SerializeField] private bool testModeSpawnSimStatusCubes = true;
    [Tooltip("Use ONLY simulated players to decide Start Game gating (ignores real players).")]
    [SerializeField] private bool testModeUseSimForGating = true;
    [Tooltip("Let you start the game alone if you're ready (handy for teleport tests).")]
    [SerializeField] private bool testModeAllowSoloStart = true;
    [Tooltip("Auto-toggle your local Ready on play (quick solo tests).")]
    [SerializeField] private bool testModeAutoReadyLocal = true;

    [SerializeField] bool debugAllowSoloStart = true;

    [SerializeField]
    private SimPlayerConfig[] simPlayers = new SimPlayerConfig[3]
    {
        new SimPlayerConfig{ present=false, role=CharacterRole.Character1, ready=false, label="Sim P1" },
        new SimPlayerConfig{ present=false, role=CharacterRole.Character2, ready=false, label="Sim P2" },
        new SimPlayerConfig{ present=false, role=CharacterRole.Character3, ready=false, label="Sim P3" },
    };

    // -----------------------------
    // Networked state
    // -----------------------------
    [Networked, Capacity(16)] private NetworkArray<PlayerLobbyState> _playerStates => default;
    [Networked] public NetworkBool GameStarted { get; private set; }
    [Networked] private int _nextStatusCubeIndex { get; set; }

    // -----------------------------
    // Locals
    // -----------------------------
    private readonly Dictionary<PlayerRef, GameObject> _statusCubes = new();
    private readonly Dictionary<PlayerRef, GameObject> _readyCubes = new();
    private readonly List<GameObject> _simStatusCubes = new(); // purely visual, local-only
    private GameObject _gameStartCube;

    private CharacterCubeController _char1Controller, _char2Controller, _char3Controller;

    // Tracks players we auto-assigned to Spectator (Char3) by pressing Ready with no role.
    // When they toggle Ready OFF again, we revert them back to Role=None.
    private readonly System.Collections.Generic.Dictionary<PlayerRef, bool> _autoSpectatorByReady
        = new System.Collections.Generic.Dictionary<PlayerRef, bool>();


    // -----------------------------
    // Unity / Fusion
    // -----------------------------

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SetupCharacterCubes();
    }

    public override void Spawned()
    {
        base.Spawned();

        // local ready cube (visual + input)
        if (Runner.LocalPlayer != PlayerRef.None) SpawnLocalReadyCube();

        // test mode visuals (sim status cubes)
        if (enableTestMode && testModeSpawnSimStatusCubes)
            RefreshSimulatedPlayersLocal();

        // auto-ready yourself if requested (runs on Authority so it propagates)
        if (Object.HasStateAuthority && enableTestMode && testModeAutoReadyLocal && Runner.LocalPlayer != PlayerRef.None)
            StartCoroutine(AutoReadyLocalNextFrame());
    }

    [ContextMenu("DEBUG: Force Start On Host")]
    public void Debug_ForceStartOnHost()
    {
        if (!debugAllowSoloStart) return;
        if (!Object || !Object.HasStateAuthority || Runner == null) return;

        Debug.Log("[Lobby][DEBUG] Force start on host (bypassing ready checks).");
        foreach (var p in Runner.ActivePlayers)
        {
            var role = GetPlayerRole(p);
            RPC_TeleportToGameLocation(p, role);
        }
    }



    private IEnumerator AutoReadyLocalNextFrame()
    {
        yield return null;
        RPC_RequestReadyToggle(Runner.LocalPlayer);
    }

    private void SetupCharacterCubes()
    {
        if (characterCube1)
        {
            _char1Controller = characterCube1.GetComponent<CharacterCubeController>() ?? characterCube1.AddComponent<CharacterCubeController>();
            _char1Controller.Initialize(CharacterRole.Character1, this);
        }
        if (characterCube2)
        {
            _char2Controller = characterCube2.GetComponent<CharacterCubeController>() ?? characterCube2.AddComponent<CharacterCubeController>();
            _char2Controller.Initialize(CharacterRole.Character2, this);
        }
        if (characterCube3)
        {
            _char3Controller = characterCube3.GetComponent<CharacterCubeController>() ?? characterCube3.AddComponent<CharacterCubeController>();
            _char3Controller.Initialize(CharacterRole.Character3, this);
        }
    }

    private bool SimRoleOccupied(CharacterRole role)
    {
        if (!enableTestMode || simPlayers == null) return false;
        if (role != CharacterRole.Character1 && role != CharacterRole.Character2) return false;
        foreach (var sp in simPlayers)
            if (sp.present && sp.role == role) return true;
        return false;
    }


    // -----------------------------
    // Fusion Callbacks
    // -----------------------------
    public void PlayerJoined(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        // Find first free state index
        for (int i = 0; i < _playerStates.Length; i++)
        {
            if (_playerStates[i].IsValid) continue;

            // New player state: no role, not ready
            var state = new PlayerLobbyState
            {
                Player = player,
                Role = CharacterRole.None,
                IsReady = false,
                StatusCubeIndex = i   // use the free index as initial stack order
            };

            _playerStates.Set(i, state);

            // Status cube + initial RED paint + restack
            RPC_SpawnStatusCube(player, state.StatusCubeIndex);
            RPC_UpdateStatusCubeVisual(player, CharacterRole.None, false);
            RPC_ReorganizeStatusCubes();

            CheckGameStartConditions();
            return;
        }

        Debug.LogWarning($"[LobbyManager] No free player state slot for {player}");
    }


    // Call this from your existing GameStart point (e.g., after you flip GameStarted=true)
    public void OnGameStartTriggered()
    {
        if (Object && Object.HasStateAuthority && startSet1OnGameStart)
            RPC_BeginSetIndex(0);
    }

    // Authority -> All : everyone enables & begins the set LOCALLY
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BeginSetIndex(int index)
    {
        if (setSequence == null || index < 0 || index >= setSequence.Length) return;
        var set = setSequence[index];
        if (!set) return;

        if (!set.gameObject.activeSelf) set.gameObject.SetActive(true);
        set.TryBegin(); // ✅ public; internally calls the private BeginSet()
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;

        for (int i = 0; i < _playerStates.Length; i++)
        {
            if (_playerStates[i].Player != player) continue;

            // free role cube visuals
            var old = _playerStates[i];
            if (old.Role != CharacterRole.None)
                RPC_UpdateCharacterCubeVisual(old.Role, false, PlayerRef.None);

            _playerStates.Set(i, default);
            RPC_RemoveStatusCube(player);
            RPC_ReorganizeStatusCubes();
            break;
        }

        if (!GameStarted) CheckGameStartConditions();
    }

    private void PlaceStateAndSpawnCube(in PlayerLobbyState state)
    {
        // write state
        for (int i = 0; i < _playerStates.Length; i++)
        {
            if (!_playerStates[i].IsValid)
            {
                _playerStates.Set(i, state);
                break;
            }
        }
        // spawn cube
        RPC_SpawnStatusCube(state.Player, state.StatusCubeIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TeleportToPose(PlayerRef who, Vector3 pos, Quaternion rot)
    {
        if (!Runner.TryGetPlayerObject(who, out var po) || !po) return;
        if (!po.HasStateAuthority) return; // only the owner applies

        var tele = FindObjectOfType<DelayedTeleporter>();
        if (tele)
        {
            // reuse a hidden scratch Transform to call your existing Transform overload
            if (_scratch == null)
            {
                var go = new GameObject("TeleportScratch"); go.hideFlags = HideFlags.HideAndDontSave;
                _scratch = go.transform;
            }
            _scratch.SetPositionAndRotation(pos, rot);
            tele.RequestTeleport(po, 0f, _scratch);
        }
        else
        {
            po.transform.SetPositionAndRotation(pos, rot);
        }
    }
    private Transform _scratch;

    // -----------------------------
    // Client Requests
    // -----------------------------
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestCharacterSelection(PlayerRef player, CharacterRole role)
    {
        if (GameStarted) return;

        var psOpt = GetPlayerState(player);
        if (!psOpt.HasValue) return;
        var ps = psOpt.Value;

        // Tapping same role -> toggle OFF
        if (ps.Role == role)
        {
            if (ps.IsReady) UpdatePlayerReady(player, false);

            if (ps.Role == CharacterRole.Character3)
                RPC_LocalPaintCharacter3For(player, false); // back to white locally

            UpdatePlayerRole(player, CharacterRole.None);

            if (role == CharacterRole.Character1 || role == CharacterRole.Character2)
                RPC_UpdateCharacterCubeVisual(role, false, PlayerRef.None);

            RPC_UpdateStatusCubeVisual(player, CharacterRole.None, false);
            RPC_UpdateReadyCubeVisual(player, false);

            CheckGameStartConditions();
            return;
        }

        // Switching roles
        if (ps.Role == CharacterRole.Character3 && role != CharacterRole.Character3)
            RPC_LocalPaintCharacter3For(player, false); // leaving Char3 -> white

        if ((role == CharacterRole.Character1 || role == CharacterRole.Character2)
            && (IsRoleOccupied(role) || SimRoleOccupied(role)))
        {
            return;
        }

        if (ps.Role == CharacterRole.Character1 || ps.Role == CharacterRole.Character2)
            RPC_UpdateCharacterCubeVisual(ps.Role, false, PlayerRef.None);

        UpdatePlayerRole(player, role);

        if (role == CharacterRole.Character1 || role == CharacterRole.Character2)
            RPC_UpdateCharacterCubeVisual(role, true, player);

        if (role == CharacterRole.Character3)
            RPC_LocalPaintCharacter3For(player, true);   // blue locally

        bool isReady = GetPlayerState(player)?.IsReady ?? false;
        RPC_UpdateStatusCubeVisual(player, role, isReady);
        RPC_UpdateReadyCubeVisual(player, isReady);

        CheckGameStartConditions();
    }

    public List<PlayerRef> GetSpectatorsOrdered()
    {
        var list = new List<(PlayerRef p, int order)>();
        // Use the NetworkArray order (StatusCubeIndex) to keep it stable across sets
        for (int i = 0; i < _playerStates.Length; i++)
        {
            var st = _playerStates[i];
            if (!st.IsValid) continue;
            if (st.Role == CharacterRole.Character3)
                list.Add((st.Player, st.StatusCubeIndex));
        }
        return list.OrderBy(t => t.order).Select(t => t.p).ToList();
    }

    public int GetSpectatorOrdinal(PlayerRef player)
    {
        var ordered = GetSpectatorsOrdered();
        for (int i = 0; i < ordered.Count; i++)
            if (ordered[i] == player) return i; // 0-based
        return -1;
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestReadyToggle(PlayerRef player)
    {
        if (GameStarted) return;

        EnsurePlayerStateExists(player);

        var psOpt = GetPlayerState(player);
        if (!psOpt.HasValue) return;
        var ps = psOpt.Value;

        bool newReady;
        var newRole = ps.Role;

        if (!ps.IsReady)
        {
            // READY ON
            if (ps.Role == CharacterRole.None)
            {
                newRole = CharacterRole.Character3;          // auto-spectator
                UpdatePlayerRole(player, newRole);
                _autoSpectatorByReady[player] = true;

                RPC_LocalPaintCharacter3For(player, true);   // blue locally
            }
            else
            {
                _autoSpectatorByReady[player] = false;
            }

            newReady = true;
        }
        else
        {
            // READY OFF
            newReady = false;

            bool wasAuto = _autoSpectatorByReady.TryGetValue(player, out var v) && v;
            if (ps.Role == CharacterRole.Character3 && wasAuto)
            {
                newRole = CharacterRole.None;
                UpdatePlayerRole(player, newRole);
                _autoSpectatorByReady[player] = false;

                RPC_LocalPaintCharacter3For(player, false);  // back to white
            }
        }

        UpdatePlayerReady(player, newReady);
        RPC_UpdateStatusCubeVisual(player, newRole, newReady);
        RPC_UpdateReadyCubeVisual(player, newReady);

        CheckGameStartConditions();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RebuildLobbyVisuals()
    {
        // Clear existing
        foreach (var go in _statusCubes.Values) if (go) Destroy(go);
        _statusCubes.Clear();

        foreach (var go in _readyCubes.Values) if (go) Destroy(go);
        _readyCubes.Clear();

        // Respawn status cubes for all known players
        foreach (var st in GetAllPlayerStates())
            RPC_SpawnStatusCube(st.Player, st.StatusCubeIndex);

        // Re-apply layout & colors
        RPC_ReorganizeStatusCubes();
        ApplyCharacterCubeIdleVisuals();

        // If start button should be visible, update it too
        CheckGameStartConditions();
    }

    private int CountPlayersFast() { int c = 0; if (Runner != null) foreach (var _ in Runner.ActivePlayers) c++; return c; }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestGameStart()
    {
        DebugFeed.Log($"[Lobby] Start requested | host={(Object && Object.HasStateAuthority)} | players={CountPlayersFast()}");

        Debug.Log($"[Lobby] Start requested by {Runner?.LocalPlayer} | host={(Object && Object.HasStateAuthority)} | players={CountPlayers()}");

        if (!Object || !Object.HasStateAuthority) return;
        if (GameStarted) return;
        if (!CanStartGame()) return;

        GameStarted = true;

        foreach (var p in Runner.ActivePlayers)
        {
            var role = GetPlayerRole(p);
            RPC_TeleportToGameLocation(p, role);
        }

        // teleport real players only (sim players are visuals)
        foreach (var state in GetAllPlayerStates())
            RPC_TeleportToGameLocation(state.Player, state.Role);

        // optionally hide lobby stuff
        RPC_HideLobbyElements();

        OnGameStart?.Invoke();
        Debug.Log("[LobbyManager] Game Started");

        // ✅ add this (Authority → All): enable + TryBegin Set 1 on every peer
        if (startSet1OnGameStart) RPC_BeginSetIndex(0);
    }

    // -----------------------------
    // RPCs: Authority -> All
    // -----------------------------

    // Reset default visuals so nothing looks pre-selected, then show everything
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowLobbyElements()
    {
        ApplyCharacterCubeIdleVisuals(); // reset to non-selected look

        if (characterCube1) characterCube1.SetActive(true);
        if (characterCube2) characterCube2.SetActive(true);
        if (characterCube3) characterCube3.SetActive(true);

        foreach (var kv in _statusCubes) if (kv.Value) kv.Value.SetActive(true);
        foreach (var kv in _readyCubes) if (kv.Value) kv.Value.SetActive(true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnStatusCube(PlayerRef player, int index)
    {
        if (!statusCubePrefab || !statusCubeSpawnPoint) return;

        var cube = Instantiate(statusCubePrefab, statusCubeSpawnPoint.position, statusCubeSpawnPoint.rotation, statusCubeParent);
        _statusCubes[player] = cube;

        var ctrl = cube.GetComponent<StatusCubeController>();
        if (ctrl) ctrl.Initialize(player, GetDisplayPlayerNumber(player));
    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RemoveStatusCube(PlayerRef player)
    {
        if (_statusCubes.TryGetValue(player, out var go) && go) Destroy(go);
        _statusCubes.Remove(player);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReorganizeStatusCubes()
    {
        if (!statusCubeSpawnPoint) return;

        // axis
        Vector3 axis = statusStackAxis switch
        {
            StackAxis.X => (statusGrowNegative ? -statusCubeSpawnPoint.right : statusCubeSpawnPoint.right),
            StackAxis.Y => (statusGrowNegative ? -statusCubeSpawnPoint.up : statusCubeSpawnPoint.up),
            _ => (statusGrowNegative ? -statusCubeSpawnPoint.forward : statusCubeSpawnPoint.forward)
        };

        // step
        float step = statusCubeDistanceOverride > 0f ? statusCubeDistanceOverride : ComputeAutoStatusCubeStep(axis);

        // combined list: simulated first (if any), then real players by StatusCubeIndex
        int i = 0;

        // sim cubes
        foreach (var sim in _simStatusCubes)
        {
            if (!sim) continue;
            sim.transform.position = statusCubeSpawnPoint.position + axis * (step * i);
            i++;
        }

        // real cubes
        var sorted = GetAllPlayerStates().OrderBy(s => s.StatusCubeIndex).ToList();
        foreach (var st in sorted)
        {
            if (_statusCubes.TryGetValue(st.Player, out var real) && real)
            {
                real.transform.position = statusCubeSpawnPoint.position + axis * (step * i);
                i++;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateStatusCubeVisual(PlayerRef player, CharacterRole role, bool isReady)
    {
        if (_statusCubes.TryGetValue(player, out var cube) && cube)
        {
            var ctrl = cube.GetComponent<StatusCubeController>();
            if (ctrl != null)
            {
                bool flashAmber = !isReady && (role == CharacterRole.Character1 || role == CharacterRole.Character2);
                ctrl.ApplyVisual(role, isReady, flashAmber);
                return;
            }

            // fallback
            var r = cube.GetComponent<Renderer>();
            if (r) r.material.color = isReady ? readyColor : (role == CharacterRole.None ? notReadyColor : selectedColor);
        }
    }

    // Paints Character 1/2 selector cubes for everyone. Character 3 is LOCAL-ONLY (handled by RPC_FlashCharacter3For).
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateCharacterCubeVisual(CharacterRole role, bool occupied, PlayerRef occupant)

    {
        GameObject cube = null;
        switch (role)
        {
            case CharacterRole.Character1: cube = characterCube1; break;
            case CharacterRole.Character2: cube = characterCube2; break;
            case CharacterRole.Character3: return; // Char3 is local-only
            default: return;
        }
        if (!cube) return;

        var r = cube.GetComponent<Renderer>();
        if (r) r.material.color = occupied ? occupiedColor : availableColor;

        var tm = cube.GetComponentInChildren<TextMesh>();
        if (tm) tm.text = occupied ? "Occupied"
                                   : (role == CharacterRole.Character1 ? "Character 1" : "Character 2");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FlashCharacterCube(CharacterRole role)
    {
        if (role != CharacterRole.Character3 || !characterCube3) return;
        StartCoroutine(FlashCubeImpl(characterCube3, character3SelectColor, 0.9f));
    }

   /* private IEnumerator FlashCube(Color c, float duration)
    {
        var r = characterCube3 ? characterCube3.GetComponent<Renderer>() : null;
        if (!r) yield break;

        var original = r.material.color;
        r.material.color = c;
        yield return new WaitForSeconds(duration);
        r.material.color = original;
    } */

    private System.Collections.IEnumerator FlashCubeImpl(GameObject cube, Color flashColor, float duration)
    {
        if (!cube) yield break;

        var rend = cube.GetComponent<Renderer>();
        if (!rend) yield break;

        // Important: access .material to get an instance, then restore it
        var mat = rend.material;
        var original = mat.color;

        mat.color = flashColor;
        yield return new WaitForSeconds(duration);
        mat.color = original;
    }

    // Local-only flash for Character 3 on the client who triggered it
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FlashCharacter3For(PlayerRef target)
    {
        if (Runner == null || Runner.LocalPlayer != target) return;
        if (!characterCube3) return;

        StartCoroutine(FlashCubeImpl(characterCube3, character3SelectColor, 1f));
    }

    // Real player occupying 'role'? return ready state.
    private bool LM_TryGetRealRoleReady(CharacterRole role, out bool ready)
    {
        ready = false;
        for (int i = 0; i < _playerStates.Length; i++)
        {
            var st = _playerStates[i];
            if (st.Player != PlayerRef.None && st.Role == role)
            {
                ready = st.IsReady;
                return true;
            }
        }
        return false;
    }

    // Sim player ready for 'role'? (uses your SimPlayerConfig fields: present, ready, role)
    private bool LM_TryGetSimRoleReady(CharacterRole role)
    {
        if (!enableTestMode || !testModeUseSimForGating || simPlayers == null) return false;

        foreach (var sp in simPlayers)
        {
            // NOTE: use the exact field names you have: present / ready / role
            if (sp.present && sp.ready && sp.role == role)
                return true;
        }
        return false;
    }

    // Is role ready (real or, if allowed, simulated)?
    private bool LM_RoleIsReady(CharacterRole role)
    {
        if (LM_TryGetRealRoleReady(role, out var ready)) return ready;
        return LM_TryGetSimRoleReady(role);
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateReadyCubeVisual(PlayerRef player, bool isReady)
    {
        if (_readyCubes.TryGetValue(player, out var go) && go)
        {
            // Preferred path: drive via controller (handles Red/Yellow/Green and pulse)
            var ctrl = go.GetComponent<ReadyCubeController>();
            if (ctrl)
            {
                var role = GetPlayerRole(player); // networked state is replicated; safe to read on clients
                ctrl.SetDebugColor(role, isReady);
                ctrl.SetReadyState(isReady);
                return;
            }

            // Fallback: plain renderer (green when ready, red otherwise)
            var r = go.GetComponent<Renderer>();
            if (r) r.material.color = isReady ? readyColor : notReadyColor;
        }
    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowGameStartCube()
    {
        if (!gameStartCubePrefab || !gameStartCubePosition) return;

        if (_gameStartCube == null)
        {
            _gameStartCube = Instantiate(gameStartCubePrefab, gameStartCubePosition.position, gameStartCubePosition.rotation);
            _gameStartCube.name = "GameStartCube";

            // Touch reliability: trigger + kinematic rigidbody
            var col = _gameStartCube.GetComponent<Collider>() ?? _gameStartCube.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var rb = _gameStartCube.GetComponent<Rigidbody>() ?? _gameStartCube.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var reporter = _gameStartCube.GetComponent<InteractableReporter>() ?? _gameStartCube.AddComponent<InteractableReporter>();
            reporter.TriggerThreshold = 1;
            reporter.PerPlayerCooldown = 0.5f;
            reporter.FireOnce = false;
            reporter.ObjectLabel = "StartGame";

            reporter.OnThresholdReached.RemoveAllListeners();
            reporter.OnThresholdReached.AddListener(() =>
            {
                if (!Instance) return;
                Instance.RPC_RequestGameStart(); // gating + teleports happen inside the manager
            });
        }

        _gameStartCube.transform.SetPositionAndRotation(gameStartCubePosition.position, gameStartCubePosition.rotation);

        // Visual cue: showing means gating passed
        if (_gameStartCube.TryGetComponent<Renderer>(out var sr))
            sr.material.color = readyColor;

        _gameStartCube.SetActive(true);
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideGameStartCube()
    {
        if (_gameStartCube)
        {
            var r = _gameStartCube.GetComponent<Renderer>();
            if (r) r.material.color = notReadyColor; // red
            _gameStartCube.SetActive(false);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TeleportToGameLocation(PlayerRef player, CharacterRole role)
    {
        Transform dst = role switch
        {
            CharacterRole.Character1 => locationA,
            CharacterRole.Character2 => locationB,
            CharacterRole.Character3 => (UnityEngine.Random.Range(0, 2) == 0 ? locationC : locationD),
            _ => null
        };
        if (!dst) return;

        if (!Runner.TryGetPlayerObject(player, out var po)) return;

        // Only the owner should actually move it (prevents proxy flicker)
        if (!po.HasStateAuthority) return;

        var tele = FindObjectOfType<DelayedTeleporter>();
        if (tele) tele.RequestTeleport(po, 0f, dst);
        else po.transform.SetPositionAndRotation(dst.position, dst.rotation);

        var vis = po.GetComponent<PlayerVisibilityManager>();
        if (vis) vis.TransitionToGame(); // this already guards on HasStateAuthority internally
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideLobbyElements()
    {
        if (characterCube1) characterCube1.SetActive(false);
        if (characterCube2) characterCube2.SetActive(false);
        if (characterCube3) characterCube3.SetActive(false);

        foreach (var c in _statusCubes.Values) if (c) c.SetActive(false);
        foreach (var c in _readyCubes.Values) if (c) c.SetActive(false);
        foreach (var c in _simStatusCubes) if (c) c.SetActive(false);

        if (_gameStartCube) _gameStartCube.SetActive(false);
    }

    // RPC: Authority -> All
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateStatusCubeColor(PlayerRef player, bool isReady)
    {
        var role = GetPlayerRole(player); // read the replicated role
        RPC_UpdateStatusCubeVisual(player, role, isReady);
    }


    // -----------------------------
    // Helpers
    // -----------------------------

    // Returns a stable, 1-based display number for a given player (P1, P2, ...).
    // If the player isn't found yet (or Runner is null), returns 0.
    private int GetDisplayPlayerNumber(PlayerRef player)
    {
        if (Runner == null || player == PlayerRef.None) return 0;

        int idx = 0;
        foreach (var p in Runner.ActivePlayers)
        {
            if (p == player) return idx + 1; // 1-based
            idx++;
        }
        return 0;
    }
    /// <summary>
    /// Spawns the local player's Ready cube, makes it touchable, and wires it to toggle ready via RPC.
    /// Also paints initial debug color: Red (no character), Yellow (has character, not ready), Green (ready).
    /// </summary>
    private void SpawnLocalReadyCube()
    {
        if (!readyCubePrefab) return;

        // Resolve local PlayerRef once and keep it for wiring
        var me = (Runner != null) ? Runner.LocalPlayer : PlayerRef.None;

        // --- decide spawn pose ---
        Vector3 spawnPos;
        Quaternion spawnRot = Quaternion.identity;

        // Try fixed slots first (if enabled)
        if (useFixedReadyPositions && readyCubePositions != null && readyCubePositions.Length > 0 && me != PlayerRef.None)
        {
            int slot = GetDisplayPlayerNumber(me) - 1; // 0-based
            if (slot >= 0 && slot < readyCubePositions.Length && readyCubePositions[slot] != null)
            {
                spawnPos = readyCubePositions[slot].position;
                spawnRot = readyCubePositions[slot].rotation;
            }
            else
            {
                var rig = VRRigMarker.Local;
                if (rig && rig.Head)
                {
                    spawnPos = rig.Head.position + rig.Head.forward * 1.0f;
                    spawnPos.y = rig.Head.position.y - 0.30f;
                }
                else spawnPos = new Vector3(0f, 1f, 0.5f);
            }
        }
        else
        {
            var rig = VRRigMarker.Local;
            if (rig && rig.Head)
            {
                spawnPos = rig.Head.position + rig.Head.forward * 1.0f;
                spawnPos.y = rig.Head.position.y - 0.30f;
            }
            else spawnPos = new Vector3(0f, 1f, 0.5f);
        }

        // --- instantiate (this is the actual "create from prefab") ---
        var readyCube = Instantiate(readyCubePrefab, spawnPos, spawnRot);
        readyCube.name = (me != PlayerRef.None) ? $"ReadyCube_P{GetDisplayPlayerNumber(me)}" : "ReadyCube";

        // optional: put on your lobby interaction layer
        int lobbyLayer = LayerMask.NameToLayer("LobbyInteraction");
        if (lobbyLayer != -1) readyCube.layer = lobbyLayer;

        // track locally so we can update visuals later
        if (me != PlayerRef.None)
            _readyCubes[me] = readyCube;

        // --- ensure physics prerequisites for hand triggers ---
        var col = readyCube.GetComponent<Collider>() ?? readyCube.AddComponent<BoxCollider>();
        col.isTrigger = true;

        var rb = readyCube.GetComponent<Rigidbody>() ?? readyCube.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // --- wire InteractableReporter (touch -> toggle ready for *me*) ---
        var reporter = readyCube.GetComponent<InteractableReporter>() ?? readyCube.AddComponent<InteractableReporter>();
        reporter.TriggerThreshold = 1;
        reporter.PerPlayerCooldown = 0.5f;
        reporter.FireOnce = false;
        reporter.ObjectLabel = "ReadyCube";

        reporter.OnThresholdReached.RemoveAllListeners();
        reporter.OnThresholdReached.AddListener(() =>
        {
            if (!Instance) return;
            // Use the captured PlayerRef 'me' so we always target our own state
            Instance.RPC_RequestReadyToggle(me);
        });

        // --- set initial debug color (Red/Yellow/Green) ---
        if (readyCube.TryGetComponent<ReadyCubeController>(out var rc))
        {
            var role = GetPlayerRole(me);
            var isReady = IsPlayerReady(me);
            rc.SetDebugColor(role, isReady);   // Red if no role, Yellow if role + !ready, Green if ready
            rc.SetReadyState(isReady);         // keep pulsing behaviour in sync
        }
    }


    // In LobbyManager.cs — replace the whole method body + signature with this:
    private float ComputeAutoStatusCubeStep(Vector3 axisWorld)
{
    // 1) Prefer a live, real status cube to measure; else fall back to prefab by spawning a temp sample
    GameObject sample = _statusCubes.Values.FirstOrDefault(go => go);
    bool spawnedTemp = false;

    if (!sample && statusCubePrefab)
    {
        // Spawn a temporary (disabled) sample at the spawn point to measure bounds correctly
        sample = Instantiate(statusCubePrefab, statusCubeSpawnPoint.position, statusCubeSpawnPoint.rotation, statusCubeParent);
        sample.SetActive(true); // needs to be active so renderers report bounds
        spawnedTemp = true;
    }

    if (!sample) return 0.4f; // safety

    // 2) Get world-aligned AABB of all renderers (UI Canvas is not a Renderer, so it’s ignored)
    var rs = sample.GetComponentsInChildren<Renderer>(true);
    if (rs.Length == 0)
    {
        if (spawnedTemp) Destroy(sample);
        return 0.4f;
    }

    Bounds b = new Bounds(sample.transform.position, Vector3.zero);
    bool any = false;
    foreach (var r in rs)
    {
        if (!r || !r.enabled) continue;
        if (!any) { b = r.bounds; any = true; }
        else b.Encapsulate(r.bounds);
    }
    if (!any)
    {
        if (spawnedTemp) Destroy(sample);
        return 0.4f;
    }

    // 3) Project the world AABB size onto the chosen axis (WORLD basis, not sample.transform axes)
    axisWorld = axisWorld.normalized;
    Vector3 size = b.size;
    float projected = Mathf.Abs(axisWorld.x) * size.x
                    + Mathf.Abs(axisWorld.y) * size.y
                    + Mathf.Abs(axisWorld.z) * size.z;

    // 4) Cleanup temp
    if (spawnedTemp) Destroy(sample);

    // 5) Clamp to sane range
    return Mathf.Clamp(projected, 0.05f, 5f);
}

    private void CheckGameStartConditions()
    {
        if (CanStartGame()) RPC_ShowGameStartCube();
        else RPC_HideGameStartCube();
    }

    private bool CanStartGame()
    {
        // Optional: solo start in Test Mode
        if (enableTestMode && testModeAllowSoloStart)
        {
            var me = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
            if (me != PlayerRef.None)
            {
                var st = GetPlayerState(me);
                if (st.HasValue && st.Value.IsReady) return true;
            }
        }

        // Only Char1 & Char2 gate the start
        bool c1 = LM_RoleIsReady(CharacterRole.Character1);
        bool c2 = LM_RoleIsReady(CharacterRole.Character2);
        return c1 && c2;
    }

    private bool IsRoleOccupied(CharacterRole role)
    {
        foreach (var s in GetAllPlayerStates())
            if (s.Role == role) return true;
        return false;
    }

    private PlayerLobbyState? GetPlayerState(PlayerRef player)
    {
        for (int i = 0; i < _playerStates.Length; i++)
            if (_playerStates[i].Player == player) return _playerStates[i];
        return null;
    }

    private void ApplyCharacterCubeIdleVisuals()
    {
        var me = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
        bool iAmChar3 = false;
        if (me != PlayerRef.None)
        {
            var st = GetPlayerState(me);
            iAmChar3 = st.HasValue && st.Value.Role == CharacterRole.Character3;
        }
        LM_LocalPaintCharacter3Selected(iAmChar3); // white if not selected, blue if you are Char3

        void Paint(GameObject go, Color c, string label)
        {
            if (!go) return;
            var r = go.GetComponent<Renderer>(); if (r) r.material.color = c;
            var tm = go.GetComponentInChildren<TextMesh>(); if (tm) tm.text = label;
        }

        // Char1/2 start available; Char3 is a local “Spectator” hint color
        Paint(characterCube1, availableColor, "Character 1");
        Paint(characterCube2, availableColor, "Character 2");
        Paint(characterCube3, character3SelectColor, "Spectator");
    }




    private List<PlayerLobbyState> GetAllPlayerStates()
    {
        var list = new List<PlayerLobbyState>();
        for (int i = 0; i < _playerStates.Length; i++)
            if (_playerStates[i].IsValid) list.Add(_playerStates[i]);
        return list;
    }

    private void UpdatePlayerRole(PlayerRef player, CharacterRole role)
    {
        for (int i = 0; i < _playerStates.Length; i++)
        {
            if (_playerStates[i].Player != player) continue;
            var s = _playerStates[i]; s.Role = role; _playerStates.Set(i, s);
            break;
        }
    }

    private void UpdatePlayerReady(PlayerRef player, bool ready)
    {
        for (int i = 0; i < _playerStates.Length; i++)
        {
            if (_playerStates[i].Player != player) continue;
            var s = _playerStates[i]; s.IsReady = ready; _playerStates.Set(i, s);
            break;
        }
    }

    // --- helpers: find indices in the NetworkArray ---
    private int FindPlayerIndex(PlayerRef player)
    {
        for (int i = 0; i < _playerStates.Length; i++)
            if (_playerStates[i].Player == player) return i;
        return -1;
    }

    private int FindFreeIndex()
    {
        for (int i = 0; i < _playerStates.Length; i++)
            if (!_playerStates[i].IsValid) return i;   // IsValid: Player != PlayerRef.None
        return -1;
    }

    private int PutNewStateAt(int index, PlayerRef player)
    {
        if (index < 0 || index >= _playerStates.Length) return -1;

        var state = new PlayerLobbyState
        {
            Player = player,
            Role = CharacterRole.None,
            IsReady = false,
            StatusCubeIndex = index
        };

        _playerStates.Set(index, state);

        // visuals: spawn + paint RED, then restack
        RPC_SpawnStatusCube(player, state.StatusCubeIndex);
        RPC_UpdateStatusCubeVisual(player, CharacterRole.None, false);
        RPC_ReorganizeStatusCubes();

        return index;
    }

    // ----- Public query helpers (for other scripts) -----


    public CharacterRole GetPlayerRole(PlayerRef player)
    {
        var st = GetPlayerState(player);
        return st.HasValue ? st.Value.Role : CharacterRole.None;
    }

    public bool IsPlayerReady(PlayerRef player)
    {
        var st = GetPlayerState(player);
        return st.HasValue && st.Value.IsReady;
    }

    public bool LocalHasLockedCharacter
    {
        get
        {
            if (Runner == null || Runner.LocalPlayer == PlayerRef.None) return false;
            return GetPlayerRole(Runner.LocalPlayer) != CharacterRole.None;
        }
    }

    // Find if a role is occupied by a real player, and whether that occupant is ready.
    private bool TryGetRealRoleState(CharacterRole role, out PlayerRef who, out bool isReady)
    {
        who = PlayerRef.None;
        isReady = false;

        for (int i = 0; i < _playerStates.Length; i++)
        {
            var st = _playerStates[i];
            if (st.Player != PlayerRef.None && st.Role == role)
            {
                who = st.Player;
                isReady = st.IsReady;
                return true;
            }
        }
        return false;
    }

    // If Test Mode is ON and "use sim for gating" is ticked, allow sims to satisfy the role+ready requirement.
    private bool TryGetSimRoleReady(CharacterRole role)
    {
        if (!enableTestMode || !testModeUseSimForGating || simPlayers == null) return false;

        foreach (var sp in simPlayers)
        {
            // tolerate either 'ready' or 'isReady' field names; and 'present' when available
            bool present = false, ready = false;
            try { present = sp.present; } catch { }
            try { ready = sp.ready; } catch { }
            try { if (!ready) ready = sp.isReady; } catch { }
            try { if (!present) present = sp.isPresent; } catch { }

            CharacterRole simRole = CharacterRole.None;
            try { simRole = sp.role; } catch { }

            if (present && ready && simRole == role)
                return true;
        }
        return false;
    }

    // Is a role "ready" (either a real occupant who is ready, or (if allowed) a sim marked ready)?
    private bool RoleIsReady(CharacterRole role)
    {
        if (TryGetRealRoleState(role, out _, out var ready)) return ready;
        return TryGetSimRoleReady(role);
    }

    // ----- Sim Players (local visuals only) -----

    [ContextMenu("Test Mode: Refresh Simulated Players")]
    private void RefreshSimulatedPlayersLocal()
    {
        // clear existing
        foreach (var go in _simStatusCubes) if (go) Destroy(go);
        _simStatusCubes.Clear();

        if (!testModeSpawnSimStatusCubes || !statusCubePrefab || !statusCubeSpawnPoint) return;

        // spawn new based on config
        int simIndex = 0;
        foreach (var sim in simPlayers)
        {
            if (!sim.present) continue;

            var go = Instantiate(statusCubePrefab, statusCubeSpawnPoint.position, statusCubeSpawnPoint.rotation, statusCubeParent);
            go.name = $"SimStatusCube_{sim.label}";
            var ctrl = go.GetComponent<StatusCubeController>();
            if (ctrl)
            {
                ctrl.Initialize(PlayerRef.None, 0);
                ctrl.SetCustomPlayerLabel(sim.label);
                bool flashAmber = !sim.ready && (sim.role == CharacterRole.Character1 || sim.role == CharacterRole.Character2);
                ctrl.ApplyVisual(sim.role, sim.ready, flashAmber);
            }
            else
            {
                var r = go.GetComponent<Renderer>();
                if (r) r.material.color = sim.ready ? readyColor : (sim.role == CharacterRole.None ? notReadyColor : selectedColor);
            }

            _simStatusCubes.Add(go);
            simIndex++;
        }

        // Mark Character1/2 cubes as occupied if any sim uses that role
        bool simHasC1 = simPlayers.Any(sp => sp.present && sp.role == CharacterRole.Character1);
        bool simHasC2 = simPlayers.Any(sp => sp.present && sp.role == CharacterRole.Character2);

        // Update visuals locally (no network state change)
        if (characterCube1)
        {
            var r = characterCube1.GetComponent<Renderer>();
            if (r) r.material.color = simHasC1 ? occupiedColor : availableColor;
            var tm = characterCube1.GetComponentInChildren<TextMesh>();
            if (tm) tm.text = simHasC1 ? "Occupied" : "Character 1";
        }
        if (characterCube2)
        {
            var r = characterCube2.GetComponent<Renderer>();
            if (r) r.material.color = simHasC2 ? occupiedColor : availableColor;
            var tm = characterCube2.GetComponentInChildren<TextMesh>();
            if (tm) tm.text = simHasC2 ? "Occupied" : "Character 2";
        }

        // stack together with real cubes
        RPC_ReorganizeStatusCubes();
        CheckGameStartConditions();
    }

    /// <summary>Ensure a PlayerLobbyState exists. Returns the index, or -1 on failure. (StateAuthority only)</summary>
    private int EnsurePlayerStateExists(PlayerRef player)
    {
        if (!Object || !Object.HasStateAuthority) return FindPlayerIndex(player);

        int idx = FindPlayerIndex(player);
        if (idx != -1) return idx;

        int free = FindFreeIndex();
        if (free == -1)
        {
            Debug.LogWarning($"[LobbyManager] No free player state slot for {player}");
            return -1;
        }

        return PutNewStateAt(free, player);
    }

    /** Paint YOUR Character 3 cube locally (white ↔ blue) */
    private void LM_LocalPaintCharacter3Selected(bool selected)
    {
        if (!characterCube3) return;

        var r = characterCube3.GetComponent<Renderer>();
        if (r) r.material.color = selected ? character3SelectedColor : character3IdleColor;

        var tm = characterCube3.GetComponentInChildren<TextMesh>();
        if (tm) tm.text = selected ? "Spectating (You)" : "Spectator";
    }

    /** RPC: paint Character 3 only on the client who triggered it */
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_LocalPaintCharacter3For(PlayerRef target, bool selected)
    {
        if (Runner == null || Runner.LocalPlayer != target) return;
        LM_LocalPaintCharacter3Selected(selected);
    }

    private int CountPlayers()
    {
        int c = 0;
        if (Runner != null) { foreach (var _ in Runner.ActivePlayers) c++; }
        return c;
    }


    /// <summary>
    /// Make sure this player has a PlayerLobbyState. Creates a new state if missing.
    /// StateAuthority only.
    /// </summary>
   /* private void EnsurePlayerStateExists(PlayerRef player)
    {
        if (!Object || !Object.HasStateAuthority) return;

        // Already has a state?
        for (int i = 0; i < _playerStates.Length; i++)
        {
            if (_playerStates[i].Player == player)
                return;
        }

        // Find a free slot (free == PlayerRef.None)
        int slot = -1;
        for (int i = 0; i < _playerStates.Length; i++)
        {
            if (_playerStates[i].Player == PlayerRef.None)
            {
                slot = i;
                break;
            }
        }

        if (slot == -1)
        {
            Debug.LogWarning($"[LobbyManager] No free player state slot for {player}");
            return;
        }

        // Create default state: no role, not ready
        var state = new PlayerLobbyState
        {
            Player = player,
            Role = CharacterRole.None,
            IsReady = false,
            // Use the slot index for initial status-cube order (safe and deterministic)
            StatusCubeIndex = slot
        };

        _playerStates.Set(slot, state);

        // Spawn + paint status cube RED, then re-stack
        RPC_SpawnStatusCube(player, state.StatusCubeIndex);
        RPC_UpdateStatusCubeVisual(player, CharacterRole.None, false);
        RPC_ReorganizeStatusCubes();
    }
   */

    // -----------------------------
    // Public API
    // -----------------------------

    public Transform GetLocationA() => locationA;
    public Transform GetLocationB() => locationB;
    public Transform GetLocationC() => locationC;
    public Transform GetLocationD() => locationD;

    public void RestartLobby()
    {
        if (!Object.HasStateAuthority) return;

        GameStarted = false;
        for (int i = 0; i < _playerStates.Length; i++)
        {
            if (!_playerStates[i].IsValid) continue;
            var s = _playerStates[i];
            s.Role = CharacterRole.None;
            s.IsReady = false;
            _playerStates.Set(i, s);
        }

        // show lobby visuals again
        if (characterCube1) characterCube1.SetActive(true);
        if (characterCube2) characterCube2.SetActive(true);
        if (characterCube3) characterCube3.SetActive(true);
        foreach (var c in _statusCubes.Values) if (c) c.SetActive(true);
        foreach (var c in _readyCubes.Values) if (c) c.SetActive(true);
        foreach (var c in _simStatusCubes) if (c) c.SetActive(true);

        // recompute
        RPC_ReorganizeStatusCubes();
        CheckGameStartConditions();
    }
}
