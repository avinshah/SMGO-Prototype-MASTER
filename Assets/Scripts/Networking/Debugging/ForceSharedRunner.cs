using Fusion;
using UnityEngine;
using System.Threading.Tasks;

public class ForceSharedRunner : MonoBehaviour
{
    [SerializeField] string sessionName = "dev-room";

    async void Start()
    {
        var runner = FindObjectOfType<NetworkRunner>() ?? new GameObject("Runner").AddComponent<NetworkRunner>();
        var sceneMgr = runner.GetComponent<INetworkSceneManager>() as NetworkSceneManagerDefault
                       ?? runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

#if UNITY_EDITOR
        var args = new StartGameArgs
        {
            GameMode = GameMode.Single,   // <- offline authority for Editor
            SceneManager = sceneMgr
        };
        var res = await runner.StartGame(args);
        Debug.Log(res.Ok ? "[ForceSharedRunner] Started Single (Editor)." : $"[ForceSharedRunner] Single failed: {res.ShutdownReason}");
#else
        var args = new StartGameArgs {
            GameMode     = GameMode.Shared,   // <- builds use Shared
            SessionName  = sessionName,
            SceneManager = sceneMgr,
            PlayerCount  = 16
        };
        var res = await runner.StartGame(args);
        Debug.Log(res.Ok ? "[ForceSharedRunner] Started Shared (Build)." : $"[ForceSharedRunner] Shared failed: {res.ShutdownReason}");
#endif
    }
}
