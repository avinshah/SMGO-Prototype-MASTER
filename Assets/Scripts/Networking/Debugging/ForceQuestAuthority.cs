using Fusion;
using UnityEngine;
using System.Threading.Tasks;

public class ForceQuestAuthority : MonoBehaviour
{
    [SerializeField] string sessionPrefix = "quest-room";
    [SerializeField] float fallbackDelaySeconds = 1.0f;

    async void Start()
    {
        // Find or create a runner
        var runner = FindObjectOfType<NetworkRunner>() ?? new GameObject("Runner").AddComponent<NetworkRunner>();
        var sceneMgr = runner.GetComponent<INetworkSceneManager>() as NetworkSceneManagerDefault
                       ?? runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

#if UNITY_ANDROID && !UNITY_EDITOR
        // Use a unique session name so you never join a stale room in the cloud
        string sessionName = $"{sessionPrefix}-{System.Guid.NewGuid().ToString("N").Substring(0,6)}";

        // Try Shared first (so real multi-Quest will still work when others join)
        var shared = await runner.StartGame(new StartGameArgs {
            GameMode     = GameMode.Shared,
            SessionName  = sessionName,
            SceneManager = sceneMgr
            // Scene left null => keep current scene
        });
        Debug.Log(shared.Ok ? "[ForceQuestAuthority] Started Shared." :
                              $"[ForceQuestAuthority] Shared failed: {shared.ShutdownReason}");

        // Give Fusion a moment to settle, then ensure we actually became the server
        await Task.Delay((int)(fallbackDelaySeconds * 1000));

        if (!runner.IsServer) {
            Debug.LogWarning("[ForceQuestAuthority] Not server after Shared start. Restarting as Single (offline authority).");
            await runner.Shutdown();
            var single = await runner.StartGame(new StartGameArgs {
                GameMode     = GameMode.Single,
                SceneManager = sceneMgr
            });
            Debug.Log(single.Ok ? "[ForceQuestAuthority] Started Single (offline)." :
                                  $"[ForceQuestAuthority] Single failed: {single.ShutdownReason}");
        }
#else
        // Non-Android paths unaffected (do whatever you already do on PC/Editor)
#endif
    }
}
