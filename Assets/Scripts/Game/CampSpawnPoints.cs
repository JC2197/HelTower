using FishNet;
using FishNet.Component.Spawning;
using UnityEngine;

/// <summary>
/// Supplies Camp spawn points to the persistent FishNet PlayerSpawner before clients join.
/// </summary>
public class CampSpawnPoints : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool verboseLogging = true;

    private void Awake()
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        PlayerSpawner playerSpawner = FindFirstObjectByType<PlayerSpawner>();
        if (playerSpawner == null)
        {
            Debug.LogError("[CampSpawnPoints] No active PlayerSpawner was found.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[CampSpawnPoints] No Camp spawn points are assigned.");
            return;
        }

        playerSpawner.Spawns = spawnPoints;

        if (verboseLogging)
            Debug.Log($"[CampSpawnPoints] Bound {spawnPoints.Length} spawn point(s) to '{playerSpawner.name}' while activeScene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}.");
    }

    private void Start()
    {
        if (!InstanceFinder.IsServerStarted || spawnPoints == null || spawnPoints.Length == 0)
            return;

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int index = 0; index < players.Length; index++)
        {
            Transform spawnPoint = spawnPoints[index % spawnPoints.Length];
            if (spawnPoint != null)
                players[index].transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        if (verboseLogging && players.Length > 0)
            Debug.Log($"[CampSpawnPoints] Positioned {players.Length} player(s) at Camp spawn points.");
    }
}
