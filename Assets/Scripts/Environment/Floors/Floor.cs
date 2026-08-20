using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// the configuration for a floor.
/// </summary>

[CreateAssetMenu(fileName = "Floor", menuName = "Floors/Floor")]
public class Floor : ScriptableObject
{
    [Header("Floor Name")]
    public string floorName;

    public GameObject floorPrefab;

    public EnemySpawnGroup spawnGroup;

    [Header("Audio")]
    [Tooltip("Background music for this arena")]
    public AudioClip backgroundMusic;
    
}