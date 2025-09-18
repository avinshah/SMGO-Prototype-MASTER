using UnityEngine;
using Fusion;
using System.Collections;

/// <summary>
/// Manages the local VR rig positioning with floor-aware alignment.
/// Handles initial lobby placement and (optionally) Quest floor calibration.
/// </summary>
public class VRRigSpawnManager : MonoBehaviour
{
    [Header("VR Rig References")]
    [SerializeField] private GameObject vrRigToManage;

    [Header("Initial Lobby Placement")]
    [SerializeField] private Transform lobbySpawnPoint;
    [SerializeField] private bool useFloorAlignOnStart = true;
    [SerializeField] private float extraFloorYOffset = -0.40f; // For seated testing
    [SerializeField] private LayerMask floorMask = ~0;
    [SerializeField] private float headRayMaxDistance = 3f;
    [SerializeField] private float fallbackHeadToFloor = 1.65f;
    [SerializeField] private bool preferQuestFloorLevel = true;
    [SerializeField] private bool matchSpawnYaw = true;

    [Header("Startup Behaviour")]
    [SerializeField] private bool teleportOnStart = true;

    [Tooltip("If true, wait for a Fusion NetworkRunner to exist (Fusion Bootstrap). If false, just wait a short delay.")]
    [SerializeField] private bool waitForRunner = true;

    [Tooltip("Maximum time to wait for the Runner before proceeding anyway.")]
    [SerializeField] private float maxWaitForRunnerSeconds = 5f;

    [Tooltip("Extra delay before placing the rig (gives XR/scene time to initialize).")]
    [SerializeField] private float prePlaceDelay = 0.25f;

    private VRRigMarker _vrRigMarker;

    private void Awake()
    {
        // If no VR rig specified, try to find it
        if (vrRigToManage == null)
        {
            vrRigToManage = GameObject.Find("InteractionRigOVR-Basic");
            if (vrRigToManage == null)
                vrRigToManage = FindObjectOfType<VRRigMarker>()?.gameObject;
        }

        if (vrRigToManage != null)
        {
            _vrRigMarker = vrRigToManage.GetComponent<VRRigMarker>();
            if (_vrRigMarker == null)
            {
                _vrRigMarker = vrRigToManage.AddComponent<VRRigMarker>();
                _vrRigMarker.RigRoot = vrRigToManage.transform;

                // Try to find the head/camera
                var camera = vrRigToManage.GetComponentInChildren<Camera>();
                if (camera != null) _vrRigMarker.Head = camera.transform;
            }

            // Mark this as the local rig (your VRRigMarker should set the static Local accordingly)
            _vrRigMarker.IsLocalRig = true;
        }

#if UNITY_XR_OCULUS
        // Set Quest to floor-level tracking for accurate floor detection
        if (preferQuestFloorLevel && OVRManager.instance)
        {
            OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            Debug.Log("[VRRigSpawnManager] Set Quest tracking origin to FloorLevel");
        }
#endif
    }

    private void Start()
    {
        if (teleportOnStart)
            StartCoroutine(InitialPlacement());
    }

    private IEnumerator InitialPlacement()
    {
        // wait one frame so XR rigs & cameras spawn
        yield return null;

        // Wait until VRRigMarker.Local & Head exist
        float t = 0f;
        while ((VRRigMarker.Local == null || VRRigMarker.Local.Head == null) && t < 5f)
        {
            t += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        // Optionally wait for a Fusion Runner (Bootstrap) — but never forever
        if (waitForRunner)
        {
            float waited = 0f;
            while (FindAnyRunner() == null && waited < maxWaitForRunnerSeconds)
            {
                yield return new WaitForSeconds(0.1f);
                waited += 0.1f;
            }
        }

        // Small extra delay to let everything settle
        if (prePlaceDelay > 0f)
            yield return new WaitForSeconds(prePlaceDelay);

        PlaceRigAtLobbySpawn();
    }

    private NetworkRunner FindAnyRunner()
    {
        // Finds an active runner created by Fusion Bootstrap (or any other)
        return FindObjectOfType<NetworkRunner>();
    }

    private void PlaceRigAtLobbySpawn()
    {
        // Find spawn point
        Transform spawnPoint = GetLobbySpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogWarning("[VRRigSpawnManager] No lobby spawn point found");
            return;
        }

        if (VRRigMarker.Local == null)
        {
            Debug.LogWarning("[VRRigSpawnManager] VRRigMarker.Local not set");
            return;
        }

        var rig = VRRigMarker.Local;
        var root = rig.RigRoot != null ? rig.RigRoot : rig.transform;

        // 1) Move horizontally & yaw to the spawn point
        root.position = new Vector3(spawnPoint.position.x, root.position.y, spawnPoint.position.z);
        if (matchSpawnYaw)
            root.rotation = Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f);

        DelayedTeleporter.AlignLocalRigHeadAboveFloorAt(
        spawnPoint.position,
         extraFloorYOffset,   // -0.40 for seated testing, 0 for standing
         0,                   // floorMask – ignored
         0,                   // headRayMaxDistance – ignored
         fallbackHeadToFloor, // only used if device can't do Floor origin
         preferQuestFloorLevel
        );

        // 2) Floor-aware Y alignment using the DelayedTeleporter static helper
        if (useFloorAlignOnStart)
        {
            DelayedTeleporter.AlignLocalRigHeadAboveFloorAt(
                spawnPoint.position,
                extraFloorYOffset,
                floorMask,
                headRayMaxDistance,
                fallbackHeadToFloor,
                preferQuestFloorLevel
            );

            Debug.Log($"[VRRigSpawnManager] Placed VR rig at lobby spawn (floor align). Root: {root.position}, extraY: {extraFloorYOffset}");
        }
        else
        {
            root.position = spawnPoint.position;
            root.rotation = spawnPoint.rotation;
            Debug.Log($"[VRRigSpawnManager] Placed VR rig at lobby spawn (simple). Root: {root.position}");
        }
    }

    private Transform GetLobbySpawnPoint()
    {
        // Priority 1: Inspector assignment
        if (lobbySpawnPoint != null)
            return lobbySpawnPoint;

        // Priority 2: PlayerSpawner field
        var playerSpawner = FindObjectOfType<PlayerSpawner>();
        if (playerSpawner != null && playerSpawner.LobbySpawnPoint != null)
            return playerSpawner.LobbySpawnPoint;

        // Priority 3: Named object
        var spawnPointObj = GameObject.Find("LobbySpawnPoint");
        if (spawnPointObj != null)
            return spawnPointObj.transform;

        // Priority 4: Default at origin
        Debug.LogWarning("[VRRigSpawnManager] No lobby spawn point found, creating default at origin");
        var defaultSpawn = new GameObject("DefaultLobbySpawn");
        defaultSpawn.transform.position = Vector3.zero;
        return defaultSpawn.transform;
    }

    /// <summary>Manually place the VR rig at lobby spawn with floor alignment.</summary>
    [ContextMenu("Reset to Lobby Spawn")]
    public void ResetToLobbySpawn() => PlaceRigAtLobbySpawn();

    /// <summary>Adjust the floor offset (useful for seated/standing toggle).</summary>
    public void SetFloorOffset(float offset)
    {
        extraFloorYOffset = offset;
        PlaceRigAtLobbySpawn();
    }

    [ContextMenu("Apply Seated Offset")]
    public void ApplySeatedOffset() => SetFloorOffset(-0.4f);

    [ContextMenu("Apply Standing Offset")]
    public void ApplyStandingOffset() => SetFloorOffset(0f);
}
