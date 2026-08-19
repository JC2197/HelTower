// using UnityEngine;
// using UnityEngine.Rendering;
// using System;
// using System.Collections;
// using FishNet;
// using FishNet.Object;
// using FishNet.Managing.Scened;
// using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
// using System.Collections.Generic;

// /// <summary>
// /// Manages floor loading and progression in multiplayer.
// /// SERVER-AUTHORITATIVE: Only server spawns floors, clients receive synchronized copies.
// /// Persists across scenes via DontDestroyOnLoad (set by NetworkSpawner before Spawn).
// /// Now a NetworkBehaviour so it can send ObserversRpc directly instead of relaying
// /// through PlayerController.
// /// </summary>
// public class FloorManager : NetworkBehaviour
// {
//     [Header("Configuration")]
//     [SerializeField] private FloorListConfig floorListConfig;

//     [Header("Auto Load Settings")]
//     [SerializeField] private bool autoLoadfloorOnStart = false;
//     [SerializeField]
//     [Tooltip("Only used if autoLoadfloorOnStart is true")]
//     private bool loadRandomOnStart = true;
//     [SerializeField]
//     [Tooltip("Only used if autoLoadfloorOnStart is true and loadRandomOnStart is false")]
//     private Floor specificStartfloor;

//     [Header("Environment Parent")]
//     [SerializeField] private Transform environmentParent;
//     [Tooltip("Permanent scene object at (0,0) that carries Teleporter, PlayerSpawner, and Tileset child. " +
//              "Used as the world root for infinite-world floors.")]
//     [SerializeField] private Transform floorRoot;

//     [Header("Lighting")]
//     [SerializeField] private UnityEngine.Rendering.Universal.Light2D globalLight;
//     [Header("Timer")]
//     [SerializeField] private int floorTimer; 
//     private Floor currentfloor;
//     private GameObject currentEnvironment;

//     public static FloorManager Instance { get; private set; }
//     public Floor Currentfloor => currentfloor;

//     /// <summary>
//     /// Fired when an floor is fully loaded and ready to play (loading screen hidden, players repositioned).
//     /// </summary>
//     public static event Action<Floor> OnfloorLoaded;

//     private bool isLoadingfloor = false;

//     // Set to true once FinalizefloorTransition has run for the current transition.
//     // Prevents the client fallback coroutine from double-finalizing.
//     private bool _floorTransitionFinalized = false;

//     // Prevents HandleGameSceneLoaded from running twice per scene transition
//     // (both FishNet OnLoadEnd and Unity sceneLoaded fire for the same load).
//     private bool _gameSceneHandled = false;

//     // Flag to indicate we're transitioning from CommandScene
//     private static bool comingFromCommandScene = false;

//     // Pre-selected floor (set by floorTeleporter before transition)
//     private static Floor preSelectedfloor = null;
//     private static int preSelectedEnemyLevel = 1;
//     private static bool hasPreSelectedEnemyLevel = false;

//     [Header("Enemy Scaling")]
//     [SerializeField] private MapLevelScalingConfig mapLevelScalingConfig;
//     private int currentEnemyLevel = 1;
//     private const float STAT_INCREASE_PER_LEVEL = 0.5f; // 50% increase per level

//     public int CurrentEnemyLevel => currentEnemyLevel;
//     public float GetEnemyStatMultiplier()
//     {
//         if (mapLevelScalingConfig != null)
//             return mapLevelScalingConfig.GetHealthMultiplier(currentEnemyLevel);

//         return 1f + (STAT_INCREASE_PER_LEVEL * (currentEnemyLevel - 1));
//     }

//     public float GetEnemyDamageMultiplier()
//     {
//         if (mapLevelScalingConfig != null)
//             return mapLevelScalingConfig.GetDamageMultiplier(currentEnemyLevel);

//         return 1f;
//     }

//     /// <summary>
//     /// Called when component is enabled - log for debugging
//     /// </summary>
//     void OnEnable()
//     {
//         Debug.Log($"[FloorManager] OnEnable() called at {Time.realtimeSinceStartup:F3}s");
//         Debug.Log($"[FloorManager] GameObject: {gameObject.name}, Scene: {gameObject.scene.name}");
//     }

//     /// <summary>
//     /// Called when component is disabled - log for debugging
//     /// </summary>
//     void OnDisable()
//     {
//         Debug.Log($"[FloorManager] OnDisable() called at {Time.realtimeSinceStartup:F3}s");
//         Debug.Log($"[FloorManager] GameObject: {gameObject.name}, Scene: {gameObject.scene.name}");
//         Debug.LogWarning("[FloorManager] Component is being disabled! Stack trace:");
//         Debug.LogWarning(UnityEngine.StackTraceUtility.ExtractStackTrace());
//     }

//     void Awake()
//     {
//         Debug.Log($"[FloorManager] Awake() called at {Time.realtimeSinceStartup:F3}s");

//         // floorManager is now a NetworkBehaviour. It is spawned from a prefab by
//         // NetworkSpawner (which calls DontDestroyOnLoad + ServerManager.Spawn).
//         // No need to strip NetworkObject or call DontDestroyOnLoad here.

//         if (Instance == null)
//         {
//             Instance = this;
//             Debug.Log($"[FloorManager] Set as singleton Instance");

//             // Subscribe to Unity's scene callback (works reliably even during FishNet transitions)
//             UnitySceneManager.sceneLoaded += OnUnitySceneLoaded;
//             Debug.Log("[FloorManager] Subscribed to Unity sceneLoaded event");

//             // Auto-load config from Resources if not assigned
//             if (floorListConfig == null)
//             {
//                 floorListConfig = Resources.Load<FloorListConfig>("FloorListConfig");
//                 if (floorListConfig == null)
//                 {
//                     Debug.LogError("[FloorManager] FloorListConfig not found! Create it at Assets/Resources/FloorListConfig.asset");
//                 }
//             }

//             if (mapLevelScalingConfig == null)
//             {
//                 mapLevelScalingConfig = Resources.Load<MapLevelScalingConfig>("MapLevelScalingConfig");
//             }
//         }
//         else
//         {
//             Debug.LogWarning($"[FloorManager] Duplicate instance found, destroying");
//             Destroy(gameObject);
//             return;
//         }
//     }

//     void Start()
//     {
//         Debug.Log($"[FloorManager] Start() called at {Time.realtimeSinceStartup:F3}s");
//         Debug.Log($"[FloorManager] Component enabled: {enabled}, GameObject active: {gameObject.activeInHierarchy}");

//         // Subscribe to FishNet scene load events (supplements Unity sceneLoaded callback)
//         var networkManager = InstanceFinder.NetworkManager;
//         if (networkManager != null && networkManager.SceneManager != null)
//         {
//             networkManager.SceneManager.OnLoadEnd += OnFishNetSceneLoaded;
//             Debug.Log("[FloorManager] Subscribed to FishNet OnLoadEnd event");
//         }
//         else
//         {
//             Debug.LogWarning("[FloorManager] Could not subscribe to FishNet OnLoadEnd — will rely on Unity sceneLoaded callback");
//         }

//         // Only auto-load floor if explicitly enabled (for testing/debugging)
//         if (autoLoadfloorOnStart)
//         {
//             Debug.Log($"[FloorManager] Auto-load is ENABLED (debug mode)");
//             if (loadRandomOnStart)
//             {
//                 LoadRandomfloor();
//             }
//             else if (specificStartFloor != null)
//             {
//                 Loadfloor(specificStartFloor);
//             }
//             else
//             {
//                 Debug.LogWarning("[FloorManager] autoLoadfloorOnStart is true but no floor specified. Loading random floor.");
//                 LoadRandomfloor();
//             }
//         }
//         else
//         {
//             Debug.Log("[FloorManager] Auto-load DISABLED. floors load via scene transition events.");
//         }
//     }

//     void OnDestroy()
//     {
//         if (Instance == this)
//         {
//             Instance = null;

//             // Unsubscribe from FishNet events
//             var networkManager = InstanceFinder.NetworkManager;
//             if (networkManager != null && networkManager.SceneManager != null)
//             {
//                 networkManager.SceneManager.OnLoadEnd -= OnFishNetSceneLoaded;
//             }

//             // Unsubscribe from Unity events (fallback)
//             UnitySceneManager.sceneLoaded -= OnUnitySceneLoaded;
//         }
//     }

//     /// <summary>
//     /// FishNet scene load callback - fires during networked scene transitions
//     /// </summary>
//     private void OnFishNetSceneLoaded(SceneLoadEndEventArgs args)
//     {
//         if (args.QueueData.SceneLoadData.SceneLookupDatas.Length == 0) return;

//         string loadedScene = args.QueueData.SceneLoadData.SceneLookupDatas[0].Name;
    
//         if (loadedScene == "GameScene")
//         {
//             HandleGameSceneLoaded();
//         }
//     }

//     /// <summary>
//     /// Unity scene load callback - fallback for single-player mode
//     /// </summary>
//     private void OnUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
//     {
//         if (!enabled)
//         {
//             Debug.LogWarning("[floorManager] Component was disabled during scene transition - re-enabling");
//             enabled = true;
//         }

//         if (scene.name == "GameScene")
//         {
//             HandleGameSceneLoaded();
//         }
//         else if (scene.name == "CommandScene")
//         {

//             // Ensure we stay enabled in CommandScene too
//             if (!enabled)
//             {
//                 Debug.LogWarning("[floorManager] Component was disabled in CommandScene - re-enabling");
//                 enabled = true;
//             }
//         }
//     }

//     /// <summary>
//     /// Common handler for GameScene loading (called by both FishNet and Unity callbacks)
//     /// </summary>
//     private void HandleGameSceneLoaded()
//     {
//         // Guard: both FishNet OnLoadEnd and Unity sceneLoaded fire for the same
//         // scene transition. Only process once per transition.
//         if (_gameSceneHandled)
//         {
//             Debug.Log("[floorManager] HandleGameSceneLoaded: already handled this scene load — skipping duplicate");
//             return;
//         }
//         _gameSceneHandled = true;

//         Debug.Log($"[floorManager] GameScene detected, finding references...");

//         // Find environment parent specifically in GameScene (not in other scenes)
//         UnityEngine.SceneManagement.Scene gameScene = UnitySceneManager.GetSceneByName("GameScene");
//         if (gameScene.isLoaded)
//         {
//             GameObject[] rootObjects = gameScene.GetRootGameObjects();
//             foreach (GameObject obj in rootObjects)
//             {
//                 if (obj.name == "Environment" || obj.name == "floorRoot")
//                 {
//                     environmentParent = obj.transform;
//                     // Both names serve as the world root for infinite-world floors.
//                     floorRoot = obj.transform;
//                     Debug.Log($"[floorManager] Found Environment/floorRoot in GameScene: {environmentParent.name} (scene: {obj.scene.name})");
//                     break;
//                 }

//                 // Also check children in case Environment/floorRoot is not at root
//                 Transform found = obj.transform.Find("Environment") ?? obj.transform.Find("floorRoot");
//                 if (found != null)
//                 {
//                     environmentParent = found;
//                     floorRoot = found;
//                     Debug.Log($"[floorManager] Found Environment/floorRoot as child in GameScene: {environmentParent.name} (scene: {found.gameObject.scene.name})");
//                     break;
//                 }
//             }

//             if (environmentParent == null)
//             {
//                 Debug.LogError("[floorManager] Could not find Environment GameObject in GameScene!");
//             }

//             // Find global light in GameScene
//             UnityEngine.Rendering.Universal.Light2D[] lights = FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None);
//             Debug.Log($"[floorManager] Found {lights.Length} lights in scene");
//             foreach (var light in lights)
//             {
//                 if (light.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global)
//                 {
//                     globalLight = light;
//                     Debug.Log($"[floorManager] Found global light: {light.gameObject.name}");
//                     break;
//                 }
//             }

//             // If we're coming from CommandScene, load floor immediately
//             if (comingFromCommandScene)
//             {
//                 // In multiplayer, add a small delay to ensure all clients have loaded the scene
//                 var networkManager = InstanceFinder.NetworkManager;
//                 if (networkManager != null && networkManager.IsServerStarted)
//                 {
//                     Debug.Log($"[floorManager] Multiplayer: Waiting 0.5s for all clients to load GameScene...");
//                     StartCoroutine(DelayedfloorLoad());
//                 }
//                 else if (networkManager != null && networkManager.IsClientStarted && !networkManager.IsServerStarted)
//                 {
//                     // Client (non-host): The server handles floor loading and network-spawns the floor.
//                     // We just need to wait for the floor to appear, then hide the loading screen
//                     // and re-enable player components.
//                     Debug.Log($"[floorManager] Client: Waiting for server to spawn floor...");
//                     StartCoroutine(ClientWaitForfloorAndFinish());
//                 }
//                 else
//                 {
//                     // Single-player: Load immediately
//                     Debug.Log($"[floorManager] Calling LoadRandomfloor() at {Time.realtimeSinceStartup:F3}s");
//                     LoadRandomfloor();
//                 }
//             }
//             else
//             {
//                 Debug.LogWarning($"[floorManager] WARNING: GameScene loaded but comingFromCommandScene=FALSE!");
//             }
//         }
//         else
//         {
//             Debug.LogError("[floorManager] GameScene not loaded!");
//         }
//     }

//     void Update()
//     {
//         // Don't check anything while loading an floor
//         // floor clear detection is now handled by floorClearWatcher components in the floor prefabs
//         if (isLoadingfloor) return;
//     }

//     /// <summary>
//     /// Coroutine to delay floor loading in multiplayer to ensure all clients have loaded the scene.
//     /// This delay is covered by the loading screen for a smooth transition experience.
//     /// </summary>
//     private IEnumerator DelayedfloorLoad()
//     {
//         Debug.Log($"[floorManager] Waiting for clients to load GameScene (covered by loading screen)...");
//         yield return new WaitForSeconds(0.5f); // Give clients time to load the scene
//         Debug.Log($"[floorManager] Client sync delay complete, calling LoadRandomfloor() at {Time.realtimeSinceStartup:F3}s");
//         LoadRandomfloor();
//     }

//     /// <summary>
//     /// Client-side coroutine: fallback that waits for the server RPC signal.
//     /// The primary path is floorTransitionCompleteRpc triggered by the server's ObserversRpc.
//     /// This coroutine exists only as a safety net in case the RPC is lost.
//     /// </summary>
//     private IEnumerator ClientWaitForfloorAndFinish()
//     {
//         float timeout = 15f;
//         float elapsed = 0f;

//         // Wait until either:
//         //   (a) floorTransitionCompleteRpc has already finalized the transition, or
//         //   (b) a PlayerSpawner appears (spawned inside the floor prefab by the server).
//         //       NOTE: We detect PlayerSpawner instead of environmentParent.childCount
//         //       because FishNet doesn't replicate Unity hierarchy — the server parents
//         //       the floor under environmentParent, but the client's copy appears at root.
//         while (elapsed < timeout)
//         {
//             // If the server RPC already triggered finalization, we're done
//             if (_floorTransitionFinalized)
//             {
//                 Debug.Log($"[floorManager] Client: RPC already finalized transition after {elapsed:F2}s — ClientWaitForfloorAndFinish exiting.");
//                 yield break;
//             }

//             // PlayerSpawner lives inside the floor prefab — if we can find one,
//             // the floor has been network-spawned and replicated to this client.
//             if (FindFirstObjectByType<PlayerSpawner>() != null)
//             {
//                 Debug.Log($"[floorManager] Client: floor detected (PlayerSpawner found) after {elapsed:F2}s");
//                 break;
//             }
//             elapsed += Time.deltaTime;
//             yield return null;
//         }

//         // If the RPC already handled it, bail out
//         if (_floorTransitionFinalized)
//         {
//             Debug.Log($"[floorManager] Client: RPC finalized during wait — exiting fallback.");
//             yield break;
//         }

//         if (elapsed >= timeout)
//         {
//             Debug.LogWarning($"[floorManager] Client: Timed out waiting for floor ({timeout}s). Finalizing locally as fallback.");
//         }

//         // Reset the flag now that we've processed it
//         comingFromCommandScene = false;

//         // Wait for the loading screen typewriter animation to finish
//         float typewriterTimeout = 15f;
//         float typewriterElapsed = 0f;
//         while (!LoadingScreen.IsTypewriterComplete && typewriterElapsed < typewriterTimeout)
//         {
//             // If the server RPC fires while we wait, let it handle everything
//             if (_floorTransitionFinalized) yield break;
//             typewriterElapsed += Time.deltaTime;
//             yield return null;
//         }

//         // If the server RPC fires after typewriter, let it handle everything
//         if (_floorTransitionFinalized) yield break;

//         // Ensure minimum loading screen duration for a smooth experience
//         float minimumLoadDuration = 3f;
//         float totalElapsed = elapsed + typewriterElapsed;
//         if (totalElapsed < minimumLoadDuration)
//         {
//             yield return new WaitForSeconds(minimumLoadDuration - totalElapsed);
//         }

//         // If the server RPC fires during the minimum wait, let it handle everything
//         if (_floorTransitionFinalized) yield break;

//         // Fallback: server RPC never arrived — finalize locally
//         Debug.LogWarning($"[floorManager] Client: Server RPC never arrived — finalizing floor transition locally as fallback.");
//         FinalizefloorTransition();

//         Debug.Log($"[floorManager] Client: floor transition complete (fallback) at {Time.realtimeSinceStartup:F3}s");
//     }

//     public void LoadRandomfloor()
//     {
//         Debug.Log("========================================");
//         Debug.Log($"[floorManager] LOAD_RANDOM_floor CALLED at {Time.realtimeSinceStartup:F3}s");

//         // MULTIPLAYER: Only server can load floors
//         var networkManager = InstanceFinder.NetworkManager;
//         Debug.Log($"[floorManager] NetworkManager exists: {networkManager != null}");
//         if (networkManager != null)
//         {
//             Debug.Log($"[floorManager] IsServerStarted: {networkManager.IsServerStarted}");
//             Debug.Log($"[floorManager] IsClientStarted: {networkManager.IsClientStarted}");
//         }

//         if (networkManager != null && networkManager.IsClientStarted && !networkManager.IsServerStarted)
//         {
//             Debug.LogWarning("[floorManager] Client cannot load floors - server is authoritative");
//             Debug.Log("========================================");
//             return;
//         }

//         floorConfig floor = null;

//         // Check if teleporter pre-selected an floor
//         if (preSelectedfloor != null)
//         {
//             Debug.Log($"[floorManager] Using pre-selected floor from teleporter: {preSelectedfloor.floorName}");
//             floor = preSelectedfloor;
//             preSelectedfloor = null; // Clear after use

//             if (hasPreSelectedEnemyLevel)
//             {
//                 currentEnemyLevel = Mathf.Max(1, preSelectedEnemyLevel);
//                 Debug.Log($"[floorManager] Using pre-selected enemy level from teleporter: {currentEnemyLevel}");
//                 hasPreSelectedEnemyLevel = false;
//                 preSelectedEnemyLevel = 1;
//             }
//         }
//         else
//         {
//             // No pre-selected floor, pick random
//             Debug.Log($"[floorManager] floorListConfig: {(floorListConfig != null ? "assigned" : "NULL")}");

//             if (floorListConfig == null)
//             {
//                 Debug.LogError("[floorManager] floorListConfig is NOT ASSIGNED!");
//                 Debug.LogError("[floorManager] Cannot load floor without config!");
//                 Debug.Log("========================================");
//                 return;
//             }

//             Debug.Log($"[floorManager] Calling floorListConfig.GetRandomfloor()...");
//             floor = floorListConfig.GetRandomfloor();
//             Debug.Log($"[floorManager] GetRandomfloor() returned: {(floor != null ? floor.floorName : "NULL")}");
//         }

//         if (floor != null)
//         {
//             Debug.Log($"[floorManager] floor selected: {floor.floorName}");
//             Debug.Log($"[floorManager] floor prefab: {(floor.environmentPrefab != null ? floor.environmentPrefab.name : "NULL")}");
//             Debug.Log($"[floorManager] Calling Loadfloor() with {floor.floorName}");
//             Debug.Log("========================================");
//             Loadfloor(floor);
//         }
//         else
//         {
//             Debug.LogError("[floorManager] GetRandomfloor() returned NULL!");
//             Debug.LogError("[floorManager] Cannot load null floor!");
//             Debug.Log("========================================");
//         }
//     }

//     public void Loadfloor(floorConfig floor)
//     {
//         // MULTIPLAYER: Only server can load floors
//         var networkManager = InstanceFinder.NetworkManager;
//         if (networkManager != null && networkManager.IsClientStarted && !networkManager.IsServerStarted)
//         {
//             Debug.LogWarning("[floorManager] Client cannot load floors - server is authoritative");
//             return;
//         }

//         if (floor == null)
//         {
//             Debug.LogError("[Command] [floorManager] Cannot load null floor!");
//             return;
//         }

//         Debug.Log($"[TIMING] [floorManager] Loadfloor called at {Time.realtimeSinceStartup:F3}s for: {floor.floorName}");

//         // CRITICAL: Ensure GameObject is active and component enabled before starting coroutine
//         if (!gameObject.activeInHierarchy)
//         {
//             Debug.LogWarning($"[floorManager] GameObject became inactive after DontDestroyOnLoad - reactivating before coroutine");
//             gameObject.SetActive(true);
//         }

//         if (!enabled)
//         {
//             Debug.LogWarning($"[floorManager] Component was disabled - re-enabling before coroutine");
//             enabled = true;
//         }

//         StartCoroutine(LoadfloorWithLoadingScreen(floor));
//     }

//     /// <summary>
//     /// Loads an floor with loading screen covering all initialization time.
//     /// Timing Strategy:
//     /// - 0.5s client sync delay (multiplayer only) - covered by loading screen
//     /// - floor instantiation and network spawn - covered by loading screen  
//     /// - Typewriter animation - covered by loading screen
//     /// - Minimum 2s total duration - ensures smooth transitions without flicker
//     /// </summary>
//     private IEnumerator LoadfloorWithLoadingScreen(floorConfig floor)
//     {
//         Debug.Log($"[TIMING] [floorManager] Coroutine started at {Time.realtimeSinceStartup:F3}s for {floor.floorName}");

//         // Track total loading time to ensure minimum duration
//         float loadStartTime = Time.realtimeSinceStartup;
//         float minimumLoadDuration = 2.0f; // Minimum loading screen display time for smooth transitions

//         // IMMEDIATELY set loading flag to prevent any other actions
//         isLoadingfloor = true;

//         // LoadingScreen scene should always be loaded
//         if (LoadingScreen.Instance == null)
//         {
//             isLoadingfloor = false;
//             yield break;
//         }

//         // If coming from teleporter, loading screen is already shown
//         // If auto-loading (debug mode), need to show it
//         if (comingFromCommandScene)
//         {

//             comingFromCommandScene = false; // Reset flag after checking
//         }
//         else
//         {
//             LoadingScreen.ShowLoading(floor.floorName, $"Enemy Level: {currentEnemyLevel}", "Defeat all enemies");
//         }

//         // Give one frame for loading screen to render
//         yield return null;
//         // Hide any previous floor messages
//         if (floorMessageUI.Instance != null)
//         {
//             floorMessageUI.Instance.HideMessage(true);
//         }
//         ClearCurrentEnvironment();       
//         yield return null;
//         currentfloor = floor;

//         // Infinite-world floors ALWAYS use the permanent scene floorRoot — never spawn a prefab.
//         // This avoids FishNet disabling the spawned NetworkObject on clients and causing tiles
//         // to be invisible. environmentPrefab is ignored when enableInfiniteWorld is true.
//         if (floor.enableInfiniteWorld)
//         {
//             currentEnvironment = floorRoot != null ? floorRoot.gameObject : null;
//             if (currentEnvironment == null)
//                 Debug.LogError("[floorManager] Infinite-world floor requires an 'Environment' or 'floorRoot' " +
//                                "GameObject in GameScene. None found!");
//             else
//             {
//                 Debug.Log($"[floorManager] Infinite-world floor — using scene root '{currentEnvironment.name}' " +
//                           $"(pos {currentEnvironment.transform.position}). environmentPrefab ignored.");

//                 // Place tiles NOW (while loading screen is up) so MobSpawners exist before
//                 // floorClearWatcher polls for enemies. Waiting until FinalizefloorTransition
//                 // is too late — floorClearWatcher fires a 2-second auto-return first.
//                 if (floor.tileSet != null)
//                 {
//                     InfiniteWorldManager worldManager = new GameObject("InfiniteWorldManager").AddComponent<InfiniteWorldManager>();
//                     Transform worldRoot = floorRoot != null ? floorRoot : currentEnvironment.transform;
//                     worldManager.InitializeWithTileSet(floor.tileSet, worldRoot);
//                     Debug.Log($"[floorManager] Tiles placed — {floor.tileSet.tileWidth}×{floor.tileSet.tileHeight} wu per tile");
//                 }
//                 else
//                 {
//                     Debug.LogWarning($"[floorManager] Infinite-world floor '{floor.floorName}' has no tileSet assigned!");
//                 }
//             }
//         }
//         else if (floor.environmentPrefab != null)
//         {
//             // MULTIPLAYER: Use FishNet Spawn for NetworkObjects
//             var networkManager = InstanceFinder.NetworkManager;
//             if (networkManager != null && networkManager.IsServerStarted)
//             {
//                 // Server spawns the floor using FishNet (synchronizes to all clients)
//                 currentEnvironment = Instantiate(floor.environmentPrefab, environmentParent);

//                 // Register with FishNet
//                 NetworkObject nob = currentEnvironment.GetComponent<NetworkObject>();
//                 if (nob != null)
//                 {
//                     networkManager.ServerManager.Spawn(currentEnvironment);
//                 }
//                 else
//                 {
//                     Debug.LogError($"[floorManager] ✗ floor prefab '{floor.environmentPrefab.name}' missing NetworkObject component at root!");
//                 }
//             }
//             else
//             {
//                 Debug.Log($"[floorManager] SINGLE-PLAYER MODE - Using regular Instantiate");
//                 currentEnvironment = Instantiate(floor.environmentPrefab, environmentParent);
//             }

//             DetachObstaclesFromfloor(currentEnvironment);
//         }
//         else
//         {
//             Debug.LogWarning($"[floorManager] No environmentPrefab assigned to floor: {floor.floorName}");
//         }

//         // Apply floor-specific lighting
//         if (globalLight != null)
//         {
//             globalLight.intensity = floor.globalLightIntensity;
//             Debug.Log($"[floorManager] Set global light intensity to {floor.globalLightIntensity}");
//         }
//         else
//         {
//             Debug.LogWarning("[floorManager] No global light assigned!");
//         }

    
//         yield return null;

//         // Wait for typewriter animation to complete before hiding
//         Debug.Log($"[TIMING] [floorManager] Waiting for typewriter to complete at {Time.realtimeSinceStartup:F3}s");
//         float typewriterTimeout = 30f; // Safety timeout
//         float waitElapsed = 0f;

//         while (!LoadingScreen.IsTypewriterComplete && waitElapsed < typewriterTimeout)
//         {
//             waitElapsed += Time.deltaTime;
//             yield return null;
//         }

//         if (LoadingScreen.IsTypewriterComplete)
//         {
//             Debug.Log($"[TIMING] [floorManager] Typewriter completed at {Time.realtimeSinceStartup:F3}s");
//         }
//         else
//         {
//             Debug.LogWarning($"[TIMING] [floorManager] Typewriter TIMED OUT at {Time.realtimeSinceStartup:F3}s after {waitElapsed:F3}s");
//         }

//         // Ensure minimum loading screen duration for smooth experience
//         float currentLoadTime = Time.realtimeSinceStartup - loadStartTime;
//         if (currentLoadTime < minimumLoadDuration)
//         {
//             float additionalWait = minimumLoadDuration - currentLoadTime;
//             Debug.Log($"[TIMING] [floorManager] Extending loading screen by {additionalWait:F3}s to meet minimum duration of {minimumLoadDuration}s");
//             yield return new WaitForSeconds(additionalWait);
//             Debug.Log($"[TIMING] [floorManager] Total loading time: {minimumLoadDuration:F3}s (enforced minimum)");
//         }
//         else
//         {
//             Debug.Log($"[TIMING] [floorManager] Total loading time: {currentLoadTime:F3}s (natural completion, exceeds minimum of {minimumLoadDuration}s)");
//         }

//         // CRITICAL: Reposition players BEFORE hiding loading screen
//         // This prevents camera from flying across map when screen fades out
//         Debug.Log($"[TIMING] [floorManager] Repositioning players at {Time.realtimeSinceStartup:F3}s (before hiding loading screen)");
//         RepositionPlayersToSpawn();

//         // CRITICAL: Force camera to snap to new player position immediately
//         // This prevents camera from smoothly panning when loading screen fades
//         ForceCameraToPlayerPosition();

//         // Give one frame for camera to update to new player position
//         yield return null;

//         // Apply enemy level scaling to all enemies in the floor
//         ApplyEnemyLevelScaling();

//         // Signal ALL clients (including host) to finalize the transition.
//         // floorManager is now a NetworkBehaviour, so we send the ObserversRpc directly.
//         if (IsServerStarted)
//         {
//             Debug.Log($"[floorManager] Server: Signalling all clients via floorTransitionCompleteRpc");
//             floorTransitionCompleteRpc();
//         }
//         else
//         {
//             // Single-player (no network): finalize locally
//             Debug.Log($"[TIMING] [floorManager] Single-player: finalizing floor transition locally");
//             FinalizefloorTransition();
//         }

//         isLoadingfloor = false;

        
//         PlayBackgroundMusic(floor);
//     }
//     private void PlayBackgroundMusic(floorConfig floor)
//     {
//         AudioManager.Instance.PlayMusic(floor.backgroundMusic, 0.1f, true);
//     }

//     void ClearCurrentEnvironment()
//     {

//         EnemyLeashManager leashManager = FindFirstObjectByType<EnemyLeashManager>();
//         if (leashManager != null)
//             Destroy(leashManager.gameObject);

//         InfiniteWorldManager worldManager = FindFirstObjectByType<InfiniteWorldManager>();
//         if (worldManager != null)
//             Destroy(worldManager.gameObject);

//         if (currentEnvironment != null)
//         {
//             // Never destroy the scene's floorRoot — it's a permanent GameScene object.
//             bool isScenefloorRoot = (floorRoot != null && currentEnvironment == floorRoot.gameObject);
//             if (isScenefloorRoot)
//             {
//                 // Only clear tiles spawned under the Tileset child.
//                 Transform tileset = currentEnvironment.transform.Find("Tileset");
//                 if (tileset != null)
//                 {
//                     int count = tileset.childCount;
//                     for (int i = count - 1; i >= 0; i--)
//                         Destroy(tileset.GetChild(i).gameObject);
//                     Debug.Log($"[floorManager] Cleared {count} tiles from Tileset child.");
//                 }
//             }
//             else
//             {
//                 // MULTIPLAYER: Despawn networked objects using FishNet
//                 NetworkObject nob = currentEnvironment.GetComponent<NetworkObject>();
//                 var networkManager = InstanceFinder.NetworkManager;

//                 if (nob != null && networkManager != null && networkManager.IsServerStarted)
//                 {
//                     networkManager.ServerManager.Despawn(currentEnvironment);
//                     Debug.Log($"[floorManager] Server despawned networked floor '{currentEnvironment.name}'");
//                 }
//                 else
//                 {
//                     Destroy(currentEnvironment);
//                     Debug.Log($"[floorManager] Destroyed non-networked floor '{currentEnvironment.name}'");
//                 }
//             }

//             currentEnvironment = null;
//         }

//         // Reset the floorClearWatcher if it exists in the environment parent
//         if (environmentParent != null)
//         {
//             floorClearWatcher watcher = environmentParent.GetComponent<floorClearWatcher>();
//             if (watcher != null)
//             {
//                 watcher.ResetForNewfloor();
//             }
//         }
//     }

//     /// <summary>
//     /// Increment enemy level when traveling to a new floor
//     /// </summary>
//     public void IncrementEnemyLevel()
//     {
//         currentEnemyLevel++;
//     }

//     /// <summary>
//     /// Detach objects with SpriteRenderers from floor parent so they sort by world Y position
//     /// </summary>
//     private void DetachObstaclesFromfloor(GameObject floorRoot)
//     {
//         if (floorRoot == null) return;

//         // Look for a parent GameObject named "WorldObjects" or similar
//         Transform worldObjectsParent = floorRoot.transform.Find("Detach");

//         // Get all direct children
//         int childCount = worldObjectsParent.childCount;

//         // Detach all children from the parent (iterate backwards to avoid index issues)
//         for (int i = childCount - 1; i >= 0; i--)
//         {
//             Transform child = worldObjectsParent.GetChild(i);

//             // Store world transform
//             Vector3 worldPos = child.position;
//             Quaternion worldRot = child.rotation;
//             Vector3 worldScale = child.lossyScale;

//             // Detach from parent
//             child.SetParent(null);

//             // Restore world transform
//             child.position = worldPos;
//             child.rotation = worldRot;
//             child.localScale = worldScale;
//         }
//     }

//     /// <summary>
//     /// Apply level-based stat scaling to all enemies in the current floor
//     /// </summary>
//     private void ApplyEnemyLevelScaling()
//     {
//         float healthMultiplier = GetEnemyStatMultiplier();
//         float damageMultiplier = GetEnemyDamageMultiplier();
//         MapEnemyLevelScalingData levelConfig = mapLevelScalingConfig != null
//             ? mapLevelScalingConfig.GetLevelEntry(currentEnemyLevel)
//             : null;

//         Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
//         foreach (Enemy enemy in enemies)
//         {
//             if (levelConfig != null)
//             {
//                 enemy.ApplyMapLevelScaling(healthMultiplier, damageMultiplier, levelConfig);
//             }
//             else
//             {
//                 enemy.ApplyLevelScaling(healthMultiplier);
//                 enemy.ApplySpawnerScaling(1f, damageMultiplier);
//             }
//         }
//     }

//     /// <summary>
//     /// Re-enable player components after floor teleporter disabled them
//     /// </summary>
//     /// <summary>
//     /// Reposition all players to spawn point (called BEFORE loading screen hides)
//     /// </summary>
//     private void RepositionPlayersToSpawn()
//     {
//         Debug.Log("========================================");
//         Debug.Log($"[floorManager] RepositionPlayersToSpawn() called at {Time.realtimeSinceStartup:F3}s");

//         PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
//         if (spawner == null)
//         {
//             Debug.LogWarning("[floorManager] No PlayerSpawner found in floor - cannot reposition players");
//             Debug.Log("========================================");
//             return;
//         }

//         Vector3 spawnPosition = spawner.transform.position;
//         Debug.Log($"[floorManager] Spawn position: {spawnPosition}");
        
//         // Check if we're in multiplayer - if so, reposition all players
//         if (BootstrapManager.IsNetworkActive)
//         {
//             Debug.Log("[floorManager] Multiplayer mode - repositioning all players");

//             // Find all players (including those in DontDestroyOnLoad scene)
//             PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//             Debug.Log($"[floorManager] Found {allPlayers.Length} PlayerController objects");

//             if (allPlayers.Length == 0)
//             {
//                 Debug.LogWarning("[floorManager] No PlayerController objects found!");
//             }

//             foreach (PlayerController player in allPlayers)
//             {
//                 player.transform.position = spawnPosition;
//                 Debug.Log($"[floorManager] Repositioned player {player.gameObject.name} to {spawnPosition}");
//             }
//         }
//         else
//         {
//             // Single-player - reposition local player only
//             PlayerController player = PlayerController.GetLocalPlayer();
//             if (player != null)
//             {
//                 player.transform.position = spawnPosition;
//                 Debug.Log($"[floorManager] Repositioned local player to {spawnPosition}");
//             }
//             else
//             {
//                 Debug.LogWarning("[floorManager] No local player found to reposition");
//             }
//         }
//         Debug.Log("========================================");
//     }

//     /// <summary>
//     /// Force camera to snap to player position immediately (no smooth transition)
//     /// Called before loading screen hides to prevent visible camera movement
//     /// </summary>
//     private void ForceCameraToPlayerPosition()
//     {
//         Debug.Log($"[floorManager] ForceCameraToPlayerPosition() called at {Time.realtimeSinceStartup:F3}s");

//         // Get the local player
//         PlayerController localPlayer = PlayerController.GetLocalPlayer();
//         if (localPlayer == null)
//         {
//             Debug.LogWarning("[floorManager] No local player found - cannot force camera position");
//             return;
//         }

//         // Find CameraManager on the local player
//         CameraManager cameraManager = localPlayer.GetComponentInChildren<CameraManager>();
//         if (cameraManager == null)
//         {
//             Debug.LogWarning("[floorManager] CameraManager not found on local player - cannot force camera position");
//             return;
//         }

//         // Camera is parented to the player — teleporting the player moves the camera instantly.
//         // No snap logic needed.
//         var mainCamera = cameraManager.GetMainCamera();
//         if (mainCamera != null)
//         {
//             Debug.Log($"[floorManager] Camera will follow player to: {localPlayer.transform.position}");
//         }
//     }

//     /// <summary>
//     /// Re-enable player components after loading screen hides (they can move during fade)
//     /// </summary>
//     private void ReenablePlayerComponents()
//     {
//         Debug.Log("========================================");
//         Debug.Log($"[floorManager] ReenablePlayerComponents() called at {Time.realtimeSinceStartup:F3}s");
//         var nm = InstanceFinder.NetworkManager;
//         bool isNetworkActive = nm != null && (nm.IsServerStarted || nm.IsClientStarted);
//         // Check if we're in multiplayer - if so, re-enable all players
//         if (isNetworkActive)
//         {
//             Debug.Log("[floorManager] Multiplayer mode - re-enabling all player components");

//             // Find all players (including those in DontDestroyOnLoad scene)
//             PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//             Debug.Log($"[floorManager] Found {allPlayers.Length} PlayerController objects (includeInactive=true)");

//             if (allPlayers.Length == 0)
//             {
//                 Debug.LogWarning("[floorManager] No PlayerController objects found! Players may not have persisted during scene transition.");
//                 Debug.LogWarning("[floorManager] Check that NetworkSpawner.SpawnPlayerForConnection() calls DontDestroyOnLoad(player)");
//             }

//             foreach (PlayerController player in allPlayers)
//             {
//                 Debug.Log($"[floorManager] Re-enabling player: {player.gameObject.name} (scene: {player.gameObject.scene.name})");
//                 ReenablePlayerComponentsInternal(player.gameObject);
//             }
//         }
//         else
//         {
//             // Single-player - re-enable local player only
//             PlayerController player = PlayerController.GetLocalPlayer();
//             if (player == null)
//             {
//                 Debug.LogWarning("[floorManager] No local player found to re-enable components");
//                 Debug.Log("========================================");
//                 return;
//             }

//             Debug.Log($"[floorManager] Single-player mode - re-enabling local player: {player.gameObject.name}");
//             ReenablePlayerComponentsInternal(player.gameObject);
//         }
//         Debug.Log("========================================");
//     }

//     /// <summary>
//     /// Internal method to re-enable components on a specific player GameObject
//     /// </summary>
//     private void ReenablePlayerComponentsInternal(GameObject playerObject)
//     {
//         if (playerObject == null) return;

//         // Re-enable player controller
//         PlayerController player = playerObject.GetComponent<PlayerController>();
//         if (player != null)
//         {
//             player.enabled = true;
//         }

//         // Re-enable sprite renderer
//         SpriteRenderer playerSprite = playerObject.GetComponent<SpriteRenderer>();
//         if (playerSprite != null)
//         {
//             playerSprite.enabled = true;
//         }

//         // Re-enable character glow
//         CharacterGlow glow = playerObject.GetComponent<CharacterGlow>();
//         if (glow != null)
//         {
//             glow.enabled = true;
//         }

//         // Re-enable weapon holders (both main hand and off hand)
//         WeaponHolder weaponHolder = playerObject.GetComponent<WeaponHolder>();
//         if (weaponHolder != null)
//         {
//             weaponHolder.enabled = true;
//         }

//         OffHandWeaponHolder offHandHolder = playerObject.GetComponent<OffHandWeaponHolder>();
//         if (offHandHolder != null)
//         {
//             offHandHolder.enabled = true;
//         }

//         // Re-enable all child sprite renderers (weapons, gear, etc.)
//         SpriteRenderer[] childSprites = playerObject.GetComponentsInChildren<SpriteRenderer>(true);
//         foreach (var sprite in childSprites)
//         {
//             sprite.enabled = true;
//         }

//         // Reset movement/rotation/sorting state to prevent stale velocity (running-left)
//         // or stale weapon rotation/sorting after the disable/enable cycle
//         if (player != null)
//         {
//             player.ResetAfterfloorTransition();
//         }

//         Debug.Log($"[floorManager] Player {playerObject.name} components re-enabled + state reset (position already set before screen hide)");
//     }

//     // ─────────────────────────────────────────────────────────────────────────
//     // floor TRANSITION RPC (ObserversRpc)
//     // Called by the server at the end of LoadfloorWithLoadingScreen to tell
//     // ALL clients (including the host) to reposition, hide the loading screen,
//     // and re-enable player components simultaneously.
//     // ─────────────────────────────────────────────────────────────────────────

//     [ObserversRpc]
//     private void floorTransitionCompleteRpc()
//     {
//         Debug.Log($"[floorManager] floorTransitionCompleteRpc received (IsServer={IsServerStarted}, IsOwner={IsOwner}) at {Time.realtimeSinceStartup:F3}s");

//         if (_floorTransitionFinalized)
//         {
//             Debug.Log("[floorManager] Transition already finalized — skipping duplicate RPC.");
//             return;
//         }

//         // Reset the command-scene flag if still set
//         comingFromCommandScene = false;

//         FinalizefloorTransition();
//     }

//     /// <summary>
//     /// The shared finalization sequence: reposition players, snap camera,
//     /// hide the loading screen, and re-enable all player components.
//     /// Called from the server's floorTransitionCompleteRpc or as a local
//     /// fallback for single-player / timeout scenarios.
//     /// </summary>
//     private void FinalizefloorTransition()
//     {
//         if (_floorTransitionFinalized)
//         {
//             Debug.Log("[floorManager] FinalizefloorTransition: already finalized — skipping.");
//             return;
//         }
//         _floorTransitionFinalized = true;

//         Debug.Log($"[floorManager] FinalizefloorTransition at {Time.realtimeSinceStartup:F3}s");

//         RepositionPlayersToSpawn();
//         ForceCameraToPlayerPosition();

//         Debug.Log($"[floorManager] Hiding loading screen at {Time.realtimeSinceStartup:F3}s");
//         if (LoadingScreen.Instance != null)
//         {
//             StartCoroutine(HideLoadingAndRestoreGameplayState());
//         }
//         else
//         {
//             Debug.LogWarning("[floorManager] LoadingScreen.Instance is null during finalize; restoring gameplay state directly.");
//             RestoreGameplayActionFlags();
//         }

//         // Re-enable player components so the player can move
//         ReenablePlayerComponents();

//         Debug.Log($"[floorManager] floor transition finalized at {Time.realtimeSinceStartup:F3}s");

//         // Notify listeners (e.g. floorTimer) that the floor config is now available
//         if (currentfloor != null)
//         {
//             Debug.Log($"[floorManager] Firing OnfloorLoaded for '{currentfloor.floorName}'");
//             OnfloorLoaded?.Invoke(currentfloor);
//         }
//     }

//     private IEnumerator HideLoadingAndRestoreGameplayState()
//     {
//         yield return StartCoroutine(LoadingScreen.Instance.Hide());
//         RestoreGameplayActionFlags();
//     }

//     private void RestoreGameplayActionFlags()
//     {
//         PlayerController.InputEnabled = true;
//         Enemy.ActionsEnabled = true;
//     }

//     /// <summary>
//     /// Call this from floorTeleporter before loading GameScene to signal floor should auto-load
//     /// </summary>
//     public static void SetComingFromCommandScene()
//     {

//         comingFromCommandScene = true;
//         // Reset guards so the upcoming transition can be handled/finalized
//         if (Instance != null)
//         {
//             Instance._floorTransitionFinalized = false;
//             Instance._gameSceneHandled = false;
//         }

//     }

//     /// <summary>
//     /// Call this from floorTeleporter to pre-select which floor to load
//     /// </summary>
//     public static void SetPreSelectedfloor(Floor floor)
//     {
//         preSelectedfloor = floor;
//         if (floor != null)
//         {
//             Debug.Log($"[FloorManager] Pre-selected floor set to: {floor.floorName}");
//         }
//     }

//     /// <summary>
//     /// Call this from floorTeleporter to pre-select which enemy level to load.
//     /// </summary>
//     public static void SetPreSelectedEnemyLevel(int enemyLevel)
//     {
//         preSelectedEnemyLevel = Mathf.Max(1, enemyLevel);
//         hasPreSelectedEnemyLevel = true;
//         Debug.Log($"[FloorManager] Pre-selected enemy level set to: {preSelectedEnemyLevel}");
//     }
// }
