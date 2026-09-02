using UnityEngine;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>
/// Persistent application entry point. Starts a local FishNet host and presents the main menu.
/// </summary>
public class BootstrapManager : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private GameObject gameplaySessionPrefab;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string campSceneName = "Camp";
    [SerializeField] private bool verboseLogging = true;

    private static BootstrapManager instance;
    private GameObject gameplaySessionInstance;
    private bool loadingCamp;
    private bool networkingRequested;

    public static BootstrapManager Instance => instance;
    public NetworkManager NetworkManager => networkManager;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        networkManager ??= FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("[BootstrapManager] No NetworkManager found. Add a NetworkManager to the Bootstrap scene before running the game.");
            return;
        }

        networkManager.ServerManager.OnServerConnectionState += LogServerConnectionState;
        networkManager.ClientManager.OnClientConnectionState += LogClientConnectionState;

        StopUnexpectedNetwork();
        LoadMainMenu();
    }

    private void OnDestroy()
    {
        if (instance != this || networkManager == null)
            return;

        networkManager.ServerManager.OnServerConnectionState -= LogServerConnectionState;
        networkManager.ClientManager.OnClientConnectionState -= LogClientConnectionState;
    }

    private void LogServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started && !networkingRequested)
        {
            Debug.LogWarning("[BootstrapManager] Server started before Camp was requested. Stopping the unexpected network session.");
            StopUnexpectedNetwork();
        }

        if (!verboseLogging)
            return;

        Debug.Log($"[BootstrapManager] SERVER {args.ConnectionState} | activeScene={UnitySceneManager.GetActiveScene().name}\nStartedBy:\n{StackTraceUtility.ExtractStackTrace()}");
    }

    private void LogClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started && !networkingRequested)
        {
            Debug.LogWarning("[BootstrapManager] Client started before Camp was requested. Stopping the unexpected network session.");
            StopUnexpectedNetwork();
        }

        if (!verboseLogging)
            return;

        Debug.Log($"[BootstrapManager] CLIENT {args.ConnectionState} | activeScene={UnitySceneManager.GetActiveScene().name}\nStartedBy:\n{StackTraceUtility.ExtractStackTrace()}");
    }

    public void LoadMainMenu()
    {
        if (UnitySceneManager.GetActiveScene().name == mainMenuSceneName)
            return;

        EndGameplaySession();
        UnitySceneManager.LoadScene(mainMenuSceneName);
    }

    public void LoadCamp()
    {
        if (loadingCamp)
            return;

        Log($"LoadCamp requested. activeScene={UnitySceneManager.GetActiveScene().name}, serverStarted={networkManager.IsServerStarted}, clientStarted={networkManager.IsClientStarted}");
        loadingCamp = true;
        networkingRequested = true;
        if (networkManager.IsServerStarted)
        {
            LoadCampAsGlobalScene();
            return;
        }

        networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        networkManager.ServerManager.StartConnection();
    }

    private void OnServerConnectionState(ServerConnectionStateArgs connectionState)
    {
        Log($"Server connection state changed to {connectionState.ConnectionState}. activeScene={UnitySceneManager.GetActiveScene().name}");
        if (connectionState.ConnectionState != LocalConnectionState.Started)
            return;

        networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        LoadCampAsGlobalScene();
    }

    private void LoadCampAsGlobalScene()
    {
        Log($"Loading '{campSceneName}' as a global scene. activeScene={UnitySceneManager.GetActiveScene().name}");
        networkManager.SceneManager.OnLoadEnd += OnCampLoaded;

        bool returningFromGame = UnitySceneManager.GetActiveScene().name == "GameScene";

        foreach (PlayerController activePlayer in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            activePlayer.DepositBagIntoHoard();
            if (returningFromGame)
                activePlayer.Revive();
        }

        SceneLoadData sceneLoadData = new SceneLoadData(campSceneName)
        {
            ReplaceScenes = ReplaceOption.All,
            MovedNetworkObjects = GetSpawnedPlayerObjects()
        };
        networkManager.SceneManager.LoadGlobalScenes(sceneLoadData);
    }

    private NetworkObject[] GetSpawnedPlayerObjects()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        NetworkObject[] playerObjects = new NetworkObject[players.Length];
        int count = 0;

        foreach (PlayerController player in players)
        {
            if (player.NetworkObject == null || !player.NetworkObject.IsSpawned)
                continue;

            playerObjects[count++] = player.NetworkObject;
        }

        if (count != playerObjects.Length)
            System.Array.Resize(ref playerObjects, count);

        Log($"Moving {playerObjects.Length} already spawned player object(s) into '{campSceneName}'.");
        return playerObjects;
    }

    private void OnCampLoaded(SceneLoadEndEventArgs sceneLoadEnd)
    {
        Log($"Scene load finished. activeScene={UnitySceneManager.GetActiveScene().name}, loaded=[{string.Join(", ", System.Array.ConvertAll(sceneLoadEnd.LoadedScenes, scene => scene.name))}]");
        foreach (UnityEngine.SceneManagement.Scene scene in sceneLoadEnd.LoadedScenes)
        {
            if (scene.name != campSceneName)
                continue;

            networkManager.SceneManager.OnLoadEnd -= OnCampLoaded;
            loadingCamp = false;
            EnsureGameplaySession();

            if (!networkManager.IsClientStarted)
            {
                Log($"Camp is loaded. Starting local client. activeScene={UnitySceneManager.GetActiveScene().name}");
                networkManager.ClientManager.StartConnection();
            }

            return;
        }
    }

    private void Log(string message)
    {
        if (verboseLogging)
            Debug.Log($"[BootstrapManager] {message}");
    }

    private void EnsureGameplaySession()
    {
        if (GameplaySessionRoot.Instance != null)
            return;

        if (gameplaySessionPrefab == null)
        {
            Debug.LogError("[BootstrapManager] Gameplay Session Prefab is not assigned. Create one shared HUD/pause/trait UI prefab with GameplaySessionRoot and assign it here.");
            return;
        }

        gameplaySessionInstance = Instantiate(gameplaySessionPrefab);
        GameplaySessionRoot sessionRoot = gameplaySessionInstance.GetComponent<GameplaySessionRoot>();
        if (sessionRoot == null || !sessionRoot.BeginSession())
        {
            Debug.LogError("[BootstrapManager] Gameplay Session Prefab requires GameplaySessionRoot on its root GameObject.");
            Destroy(gameplaySessionInstance);
            gameplaySessionInstance = null;
        }
    }

    private void EndGameplaySession()
    {
        if (gameplaySessionInstance != null)
            Destroy(gameplaySessionInstance);
    }

    private void StopUnexpectedNetwork()
    {
        if (networkManager.IsClientStarted)
            networkManager.ClientManager.StopConnection();

        if (networkManager.IsServerStarted)
            networkManager.ServerManager.StopConnection(true);
    }
}
