using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Managing.Scened;
using NUnit.Framework;
using System.Linq;
using FishNet.Component.Spawning;
using System.Collections;
using System.Collections.Generic;
/// <summary>
/// Server-authoritative source of truth for the current floor of the infinite dungeon.
/// Spawns the starting floor when the server starts, then lets FloorPortal request a transition:
/// despawn the current floor, spawn the next random one at world origin, and reposition every
/// player there. No loading screen, no enemy-level scaling, no per-frame animation timing —
/// deliberately stripped down to the minimum needed for "clear -> portal -> next floor".
/// </summary>
public class FloorManager : NetworkBehaviour
{

    public static FloorManager Instance { get; private set; }

    [Header("Floor Pool")]
    [SerializeField] private FloorListConfig floorListConfig;

    [Tooltip("Floor spawned when the server starts. Leave null to pick a random floor from floorListConfig instead.")]
    [SerializeField] private Floor startingFloor;

    private GameObject currentFloorInstance;
    private Floor currentFloor;
    private EnemySpawner enemySpawner;
    public Floor CurrentFloor => currentFloor;

    private void Awake()
    {
        Instance = this;
        enemySpawner = GetComponent<EnemySpawner>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        Floor floorToLoad = startingFloor != null ? startingFloor : floorListConfig?.GetRandomFloor();
        if (floorToLoad == null)
        {
            Debug.LogError("[FloorManager] No startingFloor and no floorListConfig assigned — cannot spawn the initial floor.");
            return;
        }

        SpawnFloor(floorToLoad);
        InstanceFinder.SceneManager.OnLoadEnd += OnSceneLoadEnd;
    }

    public override void OnStopServer()
    {
        InstanceFinder.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
        base.OnStopServer();
    }

    private void OnSceneLoadEnd(SceneLoadEndEventArgs sceneLoadEnd)
    {
        foreach (UnityEngine.SceneManagement.Scene scene in sceneLoadEnd.LoadedScenes)
        {
            if (scene != gameObject.scene)
                continue;

            RepositionAllPlayers(GetSpawnPoints());
            InstanceFinder.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
            return;
        }
    }

    /// <summary>Called by FloorPortal when a player interacts with it after the floor is cleared.</summary>
    public void TransitionToRandomFloor()
    {
        if (!IsServerStarted) return;

        Floor nextFloor = floorListConfig != null ? floorListConfig.GetRandomFloor() : null;
        if (nextFloor == null)
        {
            Debug.LogError("[FloorManager] floorListConfig has no floors assigned — cannot transition.");
            return;
        }

        DespawnCurrentFloor();
        SpawnFloor(nextFloor);
        RepositionAllPlayers(GetSpawnPoints());
        BeginRound();
    }

    private void SpawnFloor(Floor floor)
    {
        if (floor.floorPrefab == null)
        {
            Debug.LogError($"[FloorManager] Floor '{floor.floorName}' has no floorPrefab assigned.");
            return;
        }

        currentFloorInstance = Instantiate(floor.floorPrefab, Vector3.zero, Quaternion.identity);
        currentFloor = floor;
        NetworkObject nob = currentFloorInstance.GetComponent<NetworkObject>();

        Transform[] spawnPoints = GetSpawnPoints();
        PlayerSpawner fishNetSpawner = InstanceFinder.NetworkManager.GetComponent<PlayerSpawner>();
        if (fishNetSpawner != null)
        {
            fishNetSpawner.Spawns = spawnPoints;
        }
        else
        {
            Debug.LogWarning("[FloorManager] No PlayerSpawner found — spawn points will not be assigned.");
        }
        if (nob != null)
            InstanceFinder.ServerManager.Spawn(currentFloorInstance);
        else
            Debug.LogError($"[FloorManager] Floor prefab '{floor.floorPrefab.name}' has no NetworkObject on its root — it will not replicate to clients.");

        if (floor.backgroundMusic != null)
            AudioManager.Instance.PlayMusic(floor.backgroundMusic, 0.1f, true);

        Debug.Log($"[FloorManager] Spawned floor '{floor.floorName}' at world origin.");
    }

    private void BeginRound()
    {
        enemySpawner.TryStartSpawnSequence();
    }

    private void DespawnCurrentFloor()
    {

        if (currentFloorInstance == null) return;
        RemoveAllSummons();
        NetworkObject nob = currentFloorInstance.GetComponent<NetworkObject>();
        if (nob != null && nob.IsSpawned)
            InstanceFinder.ServerManager.Despawn(currentFloorInstance);
        else
            Destroy(currentFloorInstance);

        currentFloorInstance = null;
    }

    private void RemoveAllSummons()
    {
        Summon[] summons = FindObjectsByType<Summon>(FindObjectsSortMode.None);
        foreach (var summon in summons)
        {
            Destroy(summon.gameObject);
        }
    }

    /// <summary>
    /// Repositions every player to the new floor's "SpawnPoint" child if one exists, otherwise
    /// world origin. Setting transform.position directly on the server relies on the same
    /// Transform[] spawnPositions = GetSpawnPoints();
    /// NetworkTransform replication already used elsewhere for player position.
    /// </summary>
    private void RepositionAllPlayers(Transform[] spawnPositions)
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (PlayerController player in players)
            player.transform.position = spawnPositions.Length > 0 ? spawnPositions[Random.Range(0, spawnPositions.Length)].position : Vector3.zero;
    }

    private Transform[] GetSpawnPoints()
    {
        if (currentFloorInstance == null) return new Transform[0];
        string searchString = "SpawnPoint";
        return currentFloorInstance.GetComponentsInChildren<Transform>()
            .Where(obj => obj.name.Contains(searchString))
            .ToArray();
    }
}

