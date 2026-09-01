using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Configuration for construct/summon abilities (pylons, turrets, totems, etc.)
/// Handles spawning, tracking, and managing limited persistent objects.
/// </summary>
[System.Serializable]
public class ConstructConfig
{
    [Tooltip("The construct GameObject to spawn (e.g., Pylon, Turret, Totem)")]
    public GameObject constructPrefab;
    
    [Tooltip("Maximum distance from caster to spawn construct")]
    public float maxRange = 10f;
    
    [Tooltip("Spawn at caster's position (ignores mouse position)")]
    public bool spawnAtCaster = false;
    
    [Tooltip("Random radius around the caster when Spawn At Caster is enabled. 0 = exactly at caster feet.")]
    public float spawnAtCasterRadius = 0f;
    
    [Tooltip("Spawn at mouse cursor position (clamped to maxRange)")]
    public bool spawnAtMouse = true;
    
    [Tooltip("Maximum number of constructs that can exist at once. 0 = unlimited")]
    public int maxConstructs = 3;
    
    [Tooltip("What happens when max constructs reached")]
    public ConstructLimitBehavior limitBehavior = ConstructLimitBehavior.DestroyOldest;
    
    [Tooltip("How long construct exists before auto-destroying. -1 = permanent, 0 = until manually destroyed")]
    public float lifetime = 30f;
    
    [Tooltip("Destroy construct when lifetime expires")]
    public bool destroyOnLifetimeEnd = true;
    
    [Tooltip("Animation to play when construct spawns")]
    public string spawnAnimationName = "Spawn";
    
    [Tooltip("Animation to play when construct is active/idle")]
    public string activeAnimationName = "Idle";
    
    [Tooltip("Animation to play when construct is destroyed")]
    public string destructionAnimationName = "Destroy";

    [Tooltip("Animation to play when construct is on fire (if applicable)")]
    public string fireAnimationName = "Fire";
    
    [Tooltip("Delay before construct becomes active after spawn")]
    public float activationDelay = 0f;
    
    [Tooltip("Apply knockback to nearby enemies on spawn")]
    public bool applySpawnKnockback = false;
    
    [Tooltip("Knockback force on spawn")]
    public float spawnKnockbackForce = 10f;
    
    [Tooltip("Knockback radius on spawn")]
    public float spawnKnockbackRadius = 1.5f;
    
    [Tooltip("Collision radius for the construct")]
    public float collisionRadius = 0.5f;
    
    [Tooltip("Block movement (solid collider) vs allow pass-through (trigger)")]
    public bool blockMovement = true;
    
    [Header("Health & Combat")]
    [Tooltip("Maximum health for the construct. 0 = invulnerable")]
    public float maxHealth = 100f;
    
    [Tooltip("Health bar prefab to spawn (e.g., HealthBarCanvas)")]
    public GameObject healthBarPrefab;
    
    [Tooltip("Attack speed for projectile-based constructs (attacks per second)")]
    public float attackSpeed = 1f;
    
    [NonReorderable]
    [Tooltip("Abilities the construct can use (Area effects, Projectiles, etc.)")]
    public List<AbilityDataConfig> constructAbilities = new List<AbilityDataConfig>();

    
    [Header("Placement Preview")]
    [Tooltip("Holding the ability button shows a semi-transparent ghost of the construct " +
             "that tracks the cursor. Releasing the button confirms placement. " +
             "Mana and cooldown are only consumed on confirmation.")]
    public bool holdToPlace = false;

    [Tooltip("Alpha of the placement ghost (0 = invisible, 1 = fully opaque).")]
    [Range(0f, 1f)]
    public float ghostAlpha = 0.45f;

    [Header("8-Way Directional Placement")]
    [Tooltip("When enabled, the ghost and spawned construct are chosen based on the 8-way " +
             "compass direction from the caster to the cursor. " +
             "Assign directional prefabs below — any slot left empty falls back to constructPrefab.")]
    public bool use8WayPlacement = false;


    [Tooltip("Directional prefab overrides. Index order: 0=E, 1=NE, 2=N, 3=NW, 4=W, 5=SW, 6=S, 7=SE. " +
             "Leave a slot empty to fall back to constructPrefab for that direction.")]
    [NonReorderable]
    public GameObject[] directionalPrefabs = new GameObject[8];

    /// <summary>
    /// Returns the prefab for the given 8-way direction index (0=E…7=SE),
    /// falling back to constructPrefab if the slot is unassigned.
    /// </summary>
    public GameObject GetDirectionalPrefab(int directionIndex)
    {
        if (use8WayPlacement && directionalPrefabs != null &&
            directionIndex >= 0 && directionIndex < directionalPrefabs.Length &&
            directionalPrefabs[directionIndex] != null)
            return directionalPrefabs[directionIndex];
        return constructPrefab;
    }

    /// <summary>
    /// Computes the 8-way direction index (0=E, 1=NE, 2=N, 3=NW, 4=W, 5=SW, 6=S, 7=SE)
    /// from a world-space direction vector.
    /// </summary>
    public static int DirectionIndex(Vector2 fromCasterToCursor)
    {
        float angle = Mathf.Atan2(fromCasterToCursor.y, fromCasterToCursor.x) * Mathf.Rad2Deg;
        // Normalise to [0, 360)
        if (angle < 0f) angle += 360f;
        // Each slice is 45°, offset by 22.5° so that 0° (East) is centred on slice 0
        int index = Mathf.RoundToInt(angle / 45f) % 8;
        return index;
    }
}
/// <summary>
/// Behavior when construct limit is reached
/// </summary>
public enum ConstructLimitBehavior
{
    DestroyOldest,  // Destroy oldest construct when spawning new one
    PreventSpawn,   // Don't allow spawning new construct
    ReplaceClosest  // Replace closest construct to new spawn position
}
