using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// the configuration for a floor.
/// </summary>

public class Floor : MonoBehaviour
{
    [Header("Floor Name")]
    public string floorName;

    public GameObject floorPrefab;

    public EnemySpawnGroup spawnGroup;

    [Header("Audio")]
    [Tooltip("Background music for this arena")]
    public AudioClip backgroundMusic;
    
}