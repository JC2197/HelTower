using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Managing.Scened;

/// <summary>
/// Camp lobby door. When a player interacts with it the server loads the game scene
/// for every connected client via FishNet global scene loading, replacing the Camp
/// scene. Behaves like <see cref="FloorPortal"/> but starts the run instead of moving
/// to the next floor.
///
/// SERVER-AUTHORITATIVE: only the server triggers the scene load; clients follow
/// automatically because the scene is loaded as a global scene.
/// </summary>
public class CampDoor : Interactable
{
    [Header("Camp Door")]
    [Tooltip("Name of the game scene to start for all players. Must be in Build Settings.")]
    [SerializeField] private string gameSceneName = "GameScene";

    private bool isStarting = false;

    public override void OnInteract(GameObject player)
    {
        if (!CanInteract()) return;

        if (!IsServerStarted)
        {
            Debug.Log("[CampDoor] Client interacted, but only the server can start the game.");
            return;
        }

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[CampDoor] gameSceneName is not set — cannot start the game.");
            return;
        }

        isStarting = true;
        SetInteractable(false);

        foreach (PlayerController activePlayer in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            activePlayer.ResetBagGold();

        Debug.Log($"[CampDoor] Server: starting game — loading '{gameSceneName}' for all players.");

        // Move players before Camp unloads so ownership and per-player state persist into GameScene.
        SceneLoadData sld = new SceneLoadData(gameSceneName)
        {
            ReplaceScenes = ReplaceOption.All,
            MovedNetworkObjects = GetSpawnedPlayerObjects()
        };
        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }

    private static NetworkObject[] GetSpawnedPlayerObjects()
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

        return playerObjects;
    }

    public override bool CanInteract()
    {
        return base.CanInteract() && !isStarting;
    }
}
