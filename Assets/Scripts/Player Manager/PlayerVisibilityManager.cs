using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Manages player visibility and interaction capabilities based on game phase and character role.
/// Attach this to the player prefab (NetworkRig).
/// </summary>
public class PlayerVisibilityManager : NetworkBehaviour
{
    public enum VisibilityState
    {
        Lobby,          // Invisible to others, can only interact with lobby objects
        InGame,         // Visible, full interactions (Character 1 & 2)
        Spectator,      // Invisible, no interactions (Character 3)
        PartialSpectator // Invisible, limited interactions (Character 3 after trigger)
    }
    
    [Header("Configuration")]
    [SerializeField] private bool hideLocalPlayer = false; // Usually false for VR
    [SerializeField] private LayerMask lobbyInteractionLayers;
    [SerializeField] private LayerMask gameInteractionLayers;
    
    [Header("Components to Control")]
    [SerializeField] private List<Renderer> playerRenderers = new List<Renderer>();
    [SerializeField] private List<Collider> interactionColliders = new List<Collider>();
    [SerializeField] private List<Collider> physicsColliders = new List<Collider>();
    
    [Header("Hand Components")]
    [SerializeField] private GameObject leftHandVisual;
    [SerializeField] private GameObject rightHandVisual;
    [SerializeField] private Collider leftHandCollider;
    [SerializeField] private Collider rightHandCollider;
    
    // Networked state
    [Networked] public VisibilityState CurrentState { get; set; } = VisibilityState.Lobby;
    [Networked] public CharacterRole AssignedRole { get; set; } = CharacterRole.None;
    
    // Local state
    private bool _componentsCollected = false;
    private readonly Dictionary<Renderer, bool> _originalRendererStates = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Collider, bool> _originalColliderStates = new Dictionary<Collider, bool>();
    
    public override void Spawned()
    {
        base.Spawned();
        
        // Collect components if not manually assigned
        if (!_componentsCollected)
        {
            CollectComponents();
        }
        
        // Set initial state - everyone starts in lobby
        if (Object.HasStateAuthority)
        {
            CurrentState = VisibilityState.Lobby;
        }
        
        // Apply initial visibility
        ApplyVisibilityState();
        
        // Register with lobby manager
        if (LobbyManager.Instance)
        {
            LobbyManager.Instance.OnPlayerRoleChanged.AddListener(OnRoleChanged);
        }
    }
    
    private void CollectComponents()
    {
        _componentsCollected = true;
        
        // Auto-collect renderers if list is empty
        if (playerRenderers.Count == 0)
        {
            playerRenderers.AddRange(GetComponentsInChildren<Renderer>());
            
            // Remove UI renderers
            playerRenderers.RemoveAll(r => r.GetComponent<Canvas>() != null);
        }
        
        // Auto-collect colliders if lists are empty
        if (interactionColliders.Count == 0 && physicsColliders.Count == 0)
        {
            var allColliders = GetComponentsInChildren<Collider>();
            foreach (var col in allColliders)
            {
                if (col.isTrigger)
                {
                    interactionColliders.Add(col);
                }
                else
                {
                    physicsColliders.Add(col);
                }
            }
        }
        
        // Store original states
        foreach (var r in playerRenderers)
        {
            _originalRendererStates[r] = r.enabled;
        }
        
        foreach (var c in interactionColliders)
        {
            _originalColliderStates[c] = c.enabled;
        }
        
        foreach (var c in physicsColliders)
        {
            _originalColliderStates[c] = c.enabled;
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SetVisibilityState(VisibilityState newState, CharacterRole role)
    {
        CurrentState = newState;
        AssignedRole = role;
        ApplyVisibilityState();
    }
    
    private void ApplyVisibilityState()
    {
        bool isLocalPlayer = Object.HasInputAuthority;
        
        switch (CurrentState)
        {
            case VisibilityState.Lobby:
                ApplyLobbyState(isLocalPlayer);
                break;
                
            case VisibilityState.InGame:
                ApplyInGameState(isLocalPlayer);
                break;
                
            case VisibilityState.Spectator:
                ApplySpectatorState(isLocalPlayer);
                break;
                
            case VisibilityState.PartialSpectator:
                ApplyPartialSpectatorState(isLocalPlayer);
                break;
        }
        
        Debug.Log($"[PlayerVisibility] {(isLocalPlayer ? "Local" : "Remote")} player state: {CurrentState}, Role: {AssignedRole}");
    }
    
    private void ApplyLobbyState(bool isLocal)
    {
        // In lobby: Everyone is invisible to each other
        // But hands can interact with lobby objects only
        
        // Hide all player visuals (except for local player if in VR)
        foreach (var r in playerRenderers)
        {
            if (r != null)
            {
                bool shouldShow = isLocal && !hideLocalPlayer;
                
                // Special handling for hands - always show local hands in VR
                if (isLocal && (r.gameObject == leftHandVisual || r.gameObject == rightHandVisual))
                {
                    r.enabled = true;
                }
                else
                {
                    r.enabled = shouldShow;
                }
            }
        }
        
        // Enable hand colliders for lobby interactions only
        if (leftHandCollider) 
        {
            leftHandCollider.enabled = true;
            SetColliderLayers(leftHandCollider, "LobbyInteraction");
        }
        
        if (rightHandCollider) 
        {
            rightHandCollider.enabled = true;
            SetColliderLayers(rightHandCollider, "LobbyInteraction");
        }
        
        // Disable other interaction colliders
        foreach (var c in interactionColliders)
        {
            if (c != leftHandCollider && c != rightHandCollider)
            {
                c.enabled = false;
            }
        }
        
        // Keep physics colliders disabled to prevent player collision
        foreach (var c in physicsColliders)
        {
            if (c != null) c.enabled = false;
        }
    }
    
    private void ApplyInGameState(bool isLocal)
    {
        // Characters 1 & 2: Fully visible and interactive
        
        // Show all renderers
        foreach (var r in playerRenderers)
        {
            if (r != null)
            {
                bool shouldShow = !isLocal || !hideLocalPlayer;
                r.enabled = shouldShow && _originalRendererStates.GetValueOrDefault(r, true);
            }
        }
        
        // Enable all interaction colliders with game layers
        foreach (var c in interactionColliders)
        {
            if (c != null)
            {
                c.enabled = _originalColliderStates.GetValueOrDefault(c, true);
                SetColliderLayers(c, "Default");
            }
        }
        
        // Enable physics colliders
        foreach (var c in physicsColliders)
        {
            if (c != null)
            {
                c.enabled = _originalColliderStates.GetValueOrDefault(c, true);
            }
        }
    }
    
    private void ApplySpectatorState(bool isLocal)
    {
        // Character 3: Invisible ghosts with no interaction
        
        // Hide from everyone (including themselves if desired)
        foreach (var r in playerRenderers)
        {
            if (r != null)
            {
                // Optionally show hands to local spectator for navigation
                if (isLocal && (r.gameObject == leftHandVisual || r.gameObject == rightHandVisual))
                {
                    r.enabled = true;
                    // Make hands semi-transparent for local spectator
                    SetRendererAlpha(r, 0.3f);
                }
                else
                {
                    r.enabled = false;
                }
            }
        }
        
        // Disable ALL colliders - no interaction allowed
        foreach (var c in interactionColliders)
        {
            if (c != null) c.enabled = false;
        }
        
        foreach (var c in physicsColliders)
        {
            if (c != null) c.enabled = false;
        }
    }
    
    private void ApplyPartialSpectatorState(bool isLocal)
    {
        // Character 3 after trigger: Still invisible but can interact with specific objects
        
        // Keep invisible
        foreach (var r in playerRenderers)
        {
            if (r != null)
            {
                if (isLocal && (r.gameObject == leftHandVisual || r.gameObject == rightHandVisual))
                {
                    r.enabled = true;
                    SetRendererAlpha(r, 0.5f); // Slightly more visible
                }
                else
                {
                    r.enabled = false;
                }
            }
        }
        
        // Enable hand colliders for limited interactions
        if (leftHandCollider)
        {
            leftHandCollider.enabled = true;
            SetColliderLayers(leftHandCollider, "SpectatorInteraction");
        }
        
        if (rightHandCollider)
        {
            rightHandCollider.enabled = true;
            SetColliderLayers(rightHandCollider, "SpectatorInteraction");
        }
        
        // Keep physics disabled
        foreach (var c in physicsColliders)
        {
            if (c != null) c.enabled = false;
        }
    }
    
    private void SetColliderLayers(Collider col, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer != -1)
        {
            col.gameObject.layer = layer;
        }
    }
    
    private void SetRendererAlpha(Renderer r, float alpha)
    {
        if (r.material.HasProperty("_Color"))
        {
            Color c = r.material.color;
            c.a = alpha;
            r.material.color = c;
        }
    }
    
    private void OnRoleChanged(PlayerRef player, CharacterRole role)
    {
        // Only process if this is our player
        if (!Object.HasInputAuthority || !Object.InputAuthority.Equals(player))
            return;
        
        // Update our assigned role
        if (Object.HasStateAuthority)
        {
            AssignedRole = role;
        }
    }
    
    /// <summary>
    /// Call this when transitioning from lobby to game
    /// </summary>
    public void TransitionToGame()
    {
        if (!Object.HasStateAuthority) return;
        
        VisibilityState targetState = AssignedRole switch
        {
            CharacterRole.Character1 => VisibilityState.InGame,
            CharacterRole.Character2 => VisibilityState.InGame,
            CharacterRole.Character3 => VisibilityState.Spectator,
            _ => VisibilityState.Spectator
        };
        
        RPC_SetVisibilityState(targetState, AssignedRole);
    }
    
    /// <summary>
    /// Call this to enable partial interactions for spectators
    /// </summary>
    public void EnableSpectatorInteractions()
    {
        if (!Object.HasStateAuthority) return;
        
        if (CurrentState == VisibilityState.Spectator)
        {
            RPC_SetVisibilityState(VisibilityState.PartialSpectator, AssignedRole);
        }
    }
    
    /// <summary>
    /// Reset to lobby state
    /// </summary>
    public void ResetToLobby()
    {
        if (!Object.HasStateAuthority) return;
        
        RPC_SetVisibilityState(VisibilityState.Lobby, CharacterRole.None);
    }
    
    public bool CanInteractWith(GameObject target)
    {
        // Check if current state allows interaction with target
        switch (CurrentState)
        {
            case VisibilityState.Lobby:
                // Can only interact with lobby objects
                return target.layer == LayerMask.NameToLayer("LobbyInteraction") ||
                       target.GetComponent<CharacterCubeController>() != null ||
                       target.GetComponent<ReadyCubeController>() != null ||
                       target.GetComponent<GameStartCubeController>() != null;
                
            case VisibilityState.InGame:
                // Full interactions
                return true;
                
            case VisibilityState.Spectator:
                // No interactions
                return false;
                
            case VisibilityState.PartialSpectator:
                // Limited interactions
                return target.layer == LayerMask.NameToLayer("SpectatorInteraction");
                
            default:
                return false;
        }
    }
    
    private void OnDestroy()
    {
        if (LobbyManager.Instance)
        {
            LobbyManager.Instance.OnPlayerRoleChanged.RemoveListener(OnRoleChanged);
        }
    }
}