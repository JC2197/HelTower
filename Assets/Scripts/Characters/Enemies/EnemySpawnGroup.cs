using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Enemy_", menuName = "Enemy/Enemy Spawn Group")]

public class EnemySpawnGroup : ScriptableObject
{
    public string GroupName;
    public Wave[] waves;

    [Tooltip("Radius around the EnemySpawner transform used to randomize spawn positions.")]
    public float spawnRadius = 1.5f;
}

[System.Serializable]
public class Wave
{
    [Tooltip("The total number range of enemies to spawn in this wave (e.g., 3-5).")]
    public Vector2Int enemyCountRange;
    public List<EnemySpawnData> enemies;
}

[System.Serializable]
public class EnemySpawnData
{
    public GameObject enemyPrefab;
    [Tooltip("Relative chance this enemy is selected when spawning this wave.")]
    public int spawnWeight = 1;
    public float spawnDelay;

}