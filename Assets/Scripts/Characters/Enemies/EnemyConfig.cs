using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Configuration for enemy behavior, stats, and movement.
/// Uses StatContainer for runtime-modifiable stats (attack speed, movement speed, etc.)
/// </summary>
[CreateAssetMenu(fileName = "Enemy_", menuName = "Enemy/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique identifier for this enemy type")]
    public string enemyName = "Enemy";

    [Tooltip("Display name shown to player")]
    public string displayName = "Enemy";

    [Tooltip("Enemy stat values (health, resistances, etc.)")]
    public StatContainer stats = new StatContainer();

    [Tooltip("Stat type database used to initialize and synchronize this enemy's stat container")]
    public StatTypeDatabase statTypeDatabase;

    public bool isBoss = false;
    [Header("Detection")]
    [Tooltip("How far enemy can detect targets")]
    public float detectionRange = 10f;

    [Header("Ability System")]
    [Tooltip("List of abilities this enemy can use")]
    public List<EnemyAbilitySlot> abilities = new List<EnemyAbilitySlot>();

    [Header("Weapon System")]
    [Tooltip("Main hand weapon config (requires WeaponHolder component on enemy prefab)")]
    public WeaponConfig mainHandWeaponConfig;
    
    [Tooltip("Offhand weapon config for dual-wielding (requires OffHandWeaponHolder component on enemy prefab)")]
    public WeaponConfig offhandWeaponConfig;
    
    public Sprite handSprite;
    [Tooltip("If true and weapon has grantedPrimaryAbility, that ability will be used automatically")]
    public bool useWeaponGrantedAbilities = true;
    
    [Tooltip("Range at which weapon abilities can be used (enemy will stop moving and fire when target is within this range)")]
    public float weaponAbilityRange = 8f;

    [Header("AI Behavior System")]
    [Tooltip("Simple enemies skip weapon aiming and flip their sprite based on movement direction instead")]
    public bool isSimpleEnemy = false;
    
    [Tooltip("List of actions this enemy can perform (evaluated by priority and conditions)")]
    public List<EnemyActionConfig> actions = new List<EnemyActionConfig>();

    [Header("Main Movement")]
    [Tooltip("Can this enemy move toward targets?")]
    public bool canMove = true;
    
    [Tooltip("If true, enemy moves continuously. If false, uses movement/stop timing")]
    public bool continuousMovement = true;
    
    [Tooltip("Duration the enemy moves before stopping (only if continuousMovement is false)")]
    public float movementTime = 1f;
    
    [Tooltip("Duration the enemy waits before moving again (only if continuousMovement is false)")]
    public float stopTime = 0.5f;
    
    [Header("Pathfinding")]
    [Tooltip("Layers that pathfinding rays should detect as obstacles")]
    public LayerMask pathfindingObstacleLayers = -1; // Default to everything
    
    [Tooltip("How strongly to avoid obstacles (higher = more aggressive avoidance, default: 25)")]
    [Range(5f, 50f)]
    public float obstacleAvoidanceStrength = 25f;
    
    [Tooltip("Debug visualization: Draw pathfinding rays in Scene view")]
    public bool debugDrawPathfindingRays = false;
    
    [Header("Collision Damage")]
    [Tooltip("If true, this enemy deals damage on contact with the player")]
    public bool hasCollisionDamage = false;
    
    [Tooltip("Damage dealt per collision tick")]
    public float collisionDamage = 5f;
    
    [Tooltip("Seconds between collision damage ticks")]
    public float collisionDamageCooldown = 1f;
    
    [Tooltip("Damage type for collision damage")]
    [DamageTypeDropdown]
    public string collisionDamageType = "Bludgeoning";

    [Tooltip("Layers that can be hit by collision damage (set to 'Player' layer to hit player)")]
    public LayerMask collisionHitLayers = -1;

    [Header("Enemy Type")]
    [Tooltip("Flying enemies ignore collision with other enemies and the player")]
    public bool isFlying = false;

    [Tooltip("Simple ranged enemy: moves into range and fires a projectile on cooldown. Bypasses the ability/action system.")]
    public bool isProjectileEnemy = false;

    [Tooltip("Range at which the projectile enemy stops moving and starts firing")]
    public float projectileRange = 8f;

    [Tooltip("Projectile configuration to fire")]
    public ProjectileConfig projectileEnemyConfig;

    [Tooltip("Minimum time between projectile fires (seconds)")]
    public float projectileAttackCooldownMin = 2f;

    [Tooltip("Maximum time between projectile fires (seconds)")]
    public float projectileAttackCooldownMax = 3f;

    [Header("Charge Behavior")]
    [Tooltip("If true, this enemy will charge at the player when in range instead of using normal movement")]
    public bool useChargeBehavior = false;

    [Tooltip("Distance at which the enemy will begin its charge")]
    public float chargeRange = 5f;

    [Tooltip("Force applied when charging (impulse)")]
    public float chargeForce = 15f;

    [Tooltip("Friction/drag applied during charge to slow the enemy down")]
    public float chargeFriction = 3f;

    [Tooltip("Minimum speed before charge is considered complete and cooldown begins")]
    public float chargeStopSpeed = 0.1f;

    [Header("Animation Configuration")]
    [Tooltip("Animation name for idle/standing still")]
    public string idleAnimationName = "Idle";

    [Tooltip("Animation name for idle while aiming up")]
    public string idleUpAnimationName = "IdleUp";

    [Tooltip("Animation name for horizontal movement")]
    public string moveAnimationName = "Move";

    [Tooltip("Animation name for upward movement")]
    public string moveUpAnimationName = "MoveUp";

    [Header("Death")]
    [Tooltip("Ability triggered when this enemy dies. Runs from the enemy's position.")]
    public AbilityDataConfig onDeathAbility;
    
    [Tooltip("Maximum total items this enemy can drop (includes universal + specific drops)")]
    public int maxDrops = 3;

    private void OnValidate()
    {
        if (stats == null)
            stats = new StatContainer();

        StatTypeDatabase database = statTypeDatabase != null ? statTypeDatabase : StatTypeDatabase.Instance;
        if (database == null)
            return;

        int addedStats = stats.Migrate(database);
#if UNITY_EDITOR
        if (addedStats > 0)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Synchronize Stats from Database")]
    private void SynchronizeStatsFromDatabase()
    {
        OnValidate();
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[EnemyConfig] Synchronized stats for '{enemyName}' with StatTypeDatabase.");
#endif
    }

}

/// <summary>
/// Ability slot configuration for enemies
/// </summary>
[System.Serializable]
public class EnemyAbilitySlot
{
    [Tooltip("The ability to use")]
    public AbilityDataConfig abilityConfig;

    [Tooltip("Distance at which enemy stops moving and uses this ability")]
    public float range = 2f;

    [Tooltip("Priority (higher priority abilities are used first when in range)")]
    public int priority = 0;
}
