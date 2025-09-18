using Fusion;
using UnityEngine;
using System.Threading.Tasks;

public class ForceSingleRunnerQuest : MonoBehaviour
{
    async void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    // Clean up any existing runners just in case
#if UNITY_2023_1_OR_NEWER
    var existing = Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
#else
    var existing = Object.FindObjectsOfType<NetworkRunner>();
#endif
    foreach (var r in existing) {
      if (r.IsRunning) await r.Shutdown();
      Destroy(r.gameObject);
    }

    // Fresh runner
    var go = new GameObject("Runner(Single)");
    var runner = go.AddComponent<NetworkRunner>();
    var sceneMgr = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

    var res = await runner.StartGame(new StartGameArgs {
      GameMode     = GameMode.Single,   // offline authority
      SceneManager = sceneMgr           // Scene left null => keep current
    });
    Debug.Log(res.Ok ? "[ForceSingleRunnerQuest] Started Single (offline)." :
                       $"[ForceSingleRunnerQuest] Single failed: {res.ShutdownReason}");
#endif
    }
}
