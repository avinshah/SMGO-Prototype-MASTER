using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class ForceHostClientRunner : MonoBehaviour
{
    [SerializeField] string sessionName = "dev-room";
    [SerializeField] bool autoStart = true;
    [SerializeField] bool provideInput = true;

    private NetworkRunner _runner;

    private async void Start()
    {
        if (!autoStart) return;

        _runner = FindObjectOfType<NetworkRunner>();
        if (_runner == null) _runner = gameObject.AddComponent<NetworkRunner>();

        _runner.ProvideInput = provideInput;

        // Optional scene manager; safe default
        var sceneMgr = gameObject.GetComponent<INetworkSceneManager>();
        if (sceneMgr == null) sceneMgr = gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Host,          // <-- host yourself
            SessionName = sessionName,
            SceneManager = sceneMgr              // no explicit Scene value -> stay in current scene
        });

        Debug.Log($"[ForceHostClientRunner] Started Host. ShutdownReason={result.ShutdownReason}");
    }
}
