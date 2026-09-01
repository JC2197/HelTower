using UnityEngine;
using System.Collections.Generic;
using JoeConticello.VisualEffects;

/// <summary>
/// Configuration for summon abilities that spawn pet-like creatures.
/// Summons follow the owner, find nearby enemies, and attack them using a sub-ability.
/// </summary>
[System.Serializable]
public class SummonConfig
{
    [Header("Summon Prefab Profile")]
    [Tooltip("The base entity prefab to spawn.")]
    public GameObject summonPrefab;

    [Header("Summon Limits & Metadata")]
    public int maxSummons = 1;
    
    public SummonLimitBehavior limitBehavior = SummonLimitBehavior.DestroyOldest;
    public float lifetime = -1f;
    public StatContainer statContainer;
    public GameObject healthBarPrefab;
    public Vector3 spawnOffset = Vector3.zero;
    public bool isConstruct = false;

    [Header("AI & Rotational Movement Tracking")]
    public bool seekBehavior = false;
    public float followDistance = 3f;
    [NonReorderable]
    public Vector2[] slotOffsets = new Vector2[] { Vector2.zero };
    public float stopDistance = 1f;
    public float moveSpeed = 4f;
    public float detectionRange = 8f;
    public float attackRange = 1.5f;

    [Header("Target Pathfinding Boundaries")]
    public LayerMask pathfindingObstacleLayers = -1;
    [Range(5f, 50f)] public float obstacleAvoidanceStrength = 25f;
    public bool debugDrawPathfindingRays = false;

    [Tooltip("Standardized abilities this minion entity automatically initializes and cycles.")]
    [NonReorderable]
    public List<AbilityDataConfig> summonAbilities = new List<AbilityDataConfig>();

    [Header("Rotational Turret Overrides")]
    public bool isRotationalTurret = false;
    public string turretChildName = "Turret";

    [Header("Visual presentation profiles")]
    public string idleAnimation = "Idle";
    public string moveAnimation = "Move";
    public string spawnAnimation = "Spawn";
    public string deathAnimation = "Death";
    public GameObject spawnEffectPrefab;
    public GameObject deathEffectPrefab;
}


public enum SummonAttackType
{
    Melee,
    Projectile,
    Beam
}

public enum SummonLimitBehavior
{
    DestroyOldest,
    PreventSpawn,
    ReplaceClosest
}
