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
    [Header("Summon Prefab")]
    [Tooltip("The pet/summon prefab to spawn. Must have a SpriteRenderer and Animator.")]
    public GameObject summonPrefab;

    [Header("Summon Limits")]
    [Tooltip("Maximum number of summons that can exist at once. 0 = unlimited")]
    public int maxSummons = 1;

    [Tooltip("What happens when max summons is reached")]
    public SummonLimitBehavior limitBehavior = SummonLimitBehavior.DestroyOldest;

    [Header("Lifetime")]
    [Tooltip("How long the summon exists. -1 = permanent, >0 = seconds")]
    public float lifetime = -1f;

    [Header("Health")]
    [Tooltip("Maximum health for the summon. 0 = invulnerable")]
    public float maxHealth = 50f;

    [Tooltip("Health bar prefab to display above the summon")]
    public GameObject healthBarPrefab;

    [Tooltip("Follow or Seek")]
    public bool seekBehavior = false;

    [Header("Follow Behavior")]
    [Tooltip("Distance at which the summon starts following the owner")]
    public float followDistance = 3f;

    [Tooltip("Per-slot world-space offsets relative to the owner. Each active summon is assigned the offset at its index. If there are more summons than entries the list wraps around.")]
    public Vector2[] slotOffsets = new Vector2[] { Vector2.zero };

    [Tooltip("Distance at which the summon stops moving toward the owner")]
    public float stopDistance = 1f;

    [Tooltip("Base movement speed of the summon")]
    public float moveSpeed = 4f;

    [Header("Combat")]
    [Tooltip("Detection range for finding enemy targets")]
    public float detectionRange = 8f;

    [Tooltip("Attacks per second")]
    public float attackSpeed = 1f;

    [Tooltip("Base damage dealt by the summon's attack")]
    public float damage = 10f;

    [DamageTypeDropdown]
    [Tooltip("Damage type dealt by the summon")]
    public string damageTypeName = "Physical";

    [Tooltip("Range at which the summon can attack (melee range or projectile launch range)")]
    public float attackRange = 1.5f;

    [Header("Pathfinding")]
    [Tooltip("Layers the pathfinding rays treat as obstacles (walls, terrain, etc.)")]
    public LayerMask pathfindingObstacleLayers = -1;

    [Tooltip("How strongly obstacles steer the summon away (higher = more aggressive avoidance)")]
    [Range(5f, 50f)]
    public float obstacleAvoidanceStrength = 25f;

    [Tooltip("Draw the five pathfinding rays in the Scene view for debugging")]
    public bool debugDrawPathfindingRays = false;

    [Header("Life Steal")]
    [Tooltip("Heal the player owner on hit. Inherited by melee, projectile, and beam sub-configs at runtime.")]
    public LifeStealConfig lifeSteal = new LifeStealConfig();

    [Header("Sub-Ability")]
    [Tooltip("The type of attack the summon uses")]
    public SummonAttackType attackType = SummonAttackType.Melee;

    [Tooltip("Melee configuration (used when attackType = Melee)")]
    public MeleeConfig meleeConfig;

    [Tooltip("Projectile configuration (used when attackType = Projectile)")]
    public ProjectileConfig projectileConfig;

    [Tooltip("Beam configuration (used when attackType = Beam)")]
    public BeamAbilityConfig beamConfig;

    [Header("Animations")]
    [Tooltip("Animation state name for idle")]
    public string idleAnimation = "Idle";

    [Tooltip("Animation state name for moving")]
    public string moveAnimation = "Move";

    [Tooltip("Animation state name for attacking")]
    public string attackAnimation = "Attack";

    [Tooltip("Normalised time within the attack animation at which the attack fires (0 = first frame, 0.5 = halfway, 1 = last frame).")]
    [Range(0f, 1f)]
    public float attackTriggerNormalizedTime = 0.1f;

    [Header("Spawn")]
    [Tooltip("Offset from the caster where the summon spawns")]
    public Vector2 spawnOffset = new Vector2(1f, 0f);

    [Tooltip("Animation to play on spawn (leave empty for none)")]
    public string spawnAnimation = "";

    [Header("Visual Effects")]
    [Tooltip("Effect prefab spawned when the summon appears. Should have AutoDestroyEffect.")]
    public GameObject spawnEffectPrefab;

    [Tooltip("Effect prefab spawned when the summon dies or is removed. Should have AutoDestroyEffect.")]
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
