using UnityEngine;
using System.Collections.Generic;

public class EnemySpawnGroup : ScriptableObject
{
    public string GroupName;
    public Wave[] waves;
}

[System.Serializable]
public class Wave
{
    [Tooltip("The number range of enemies to spawn in this wave (e.g., 3-5).")]
    public Vector2Int enemyCountRange;
    public List<EnemySpawnData> enemies;
}

[System.Serializable]
public class EnemySpawnData
{
    public GameObject enemyPrefab;
    public int spawnCount;
    public float spawnDelay;

}