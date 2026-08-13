using UnityEngine;
using FishNet;
using FishNet.Object;

/// <summary>
/// Spawns enemies based on a given EnemySpawnGroup configuration.
public class EnemySpawner : NetworkBehaviour
{
    public EnemySpawnGroup spawnGroup;
}