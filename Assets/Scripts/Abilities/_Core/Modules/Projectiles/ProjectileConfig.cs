using UnityEngine;

/// <summary>
/// Configuration for projectile behavior, damage, and visual effects.
/// Inline serializable configuration.
/// </summary>
[System.Serializable]
public class ProjectileConfig
{
    // Note: Custom drawer in ProjectileConfigDrawer.cs handles conditional display
    [Header("Hitbox")]
    [Tooltip("Shared hitbox configuration: prefab, scale, hit layers, damage, weapon damage, on-hit effects, knockback, pull, life steal, and hit feedback.")]
    public HitboxConfig hitbox = new HitboxConfig();

    [Tooltip("Allow weapon to override projectile prefab, muzzle flash, and hit effects. If false, always uses ability's configured values.")]
    public bool allowOverride = false;

    [Tooltip("Deal damage over time instead of instant damage on hit")]
    public bool dealsDamageOverTime = false;

    [Tooltip("Damage per tick when dealsDamageOverTime = true")]
    public float damagePerTick = 5f;

    [Tooltip("How often DoT damage ticks (e.g., 0.5 = twice per second)")]
    public float dotInterval = 0.5f;

    [Tooltip("Duration of damage over time effect in seconds")]
    public float dotDuration = 3f;

    [Tooltip("Particle effect to attach to target while DoT is active")]
    public ParticleSystem dotParticleEffectPrefab;

    [Tooltip("If true, DoT particle box starts at feet (bottom) of target instead of center")]
    public bool startParticlesFromFeet = false;

    [Tooltip("Speed of the projectile")]
    public float speed = 15f;

    public bool useLifetime = true;
    [Tooltip("How long projectile lives before despawning")]
    public float lifetime = 3f;

    [Tooltip("Maximum distance projectile can travel before despawning (0 = no range limit)")]
    public float maxRange = 0f;

    [Tooltip("Behavior pattern of the projectile")]
    public ProjectileBehavior behavior = ProjectileBehavior.Straight;

    [Header("Charge / Precast Modifiers")]
    [Tooltip("Damage multiplier applied when ability has precast animation (hasPrecast in AbilityDataConfig)")]
    public float chargeDamageMultiplier = 1f;

    [Tooltip("Can cancel precast/charge animation before launching")]
    public bool canCancelCharge = true;


    [Tooltip("Enable multi-shot configuration")]
    public bool hasMultiShot = false;



    [Tooltip("Number of projectiles fired per shot")]
    [Min(1)]
    public int projectileCount = 1;

    [Tooltip("Angle spread between projectiles (total spread)")]
    public float spreadAngle = 15f;

    [Tooltip("Additional angle per projectile (alternative to spreadAngle)")]
    public float spreadAnglePerProjectile = 0f;

    [Header("Salvo")]
    [Tooltip("Total number of projectile bursts per fire (1 = single shot, 2 = two shots staggered, etc.).")]
    [Min(1)]
    public int salvoSize = 1;

    [Tooltip("Delay in seconds between each salvo burst.")]
    public float salvoInterval = 0.15f;
    [Tooltip("Maximum positive random angle applied independently to each salvo burst. For example, 90 chooses an offset from 0 to 90 degrees.")]
    [Min(0f)]
    public float salvoAngle = 0f;
    [Tooltip("Enable pierce configuration")]
    public bool hasPierce = false;
    [Tooltip("Number of targets projectile can pierce (0 = destroys on first hit)")]
    public int pierceCount = 0;

    [Tooltip("Enable projectile chaining (redirect to another target after hit)")]
    public bool hasChaining = false;

    [Tooltip("Maximum distance to search for next chain target")]
    public float chainRange = 10f;

    [Tooltip("Maximum number of targets this projectile can chain to (0 = destroys on first hit)")]
    public int maxChains = 0;

    [Tooltip("Strength of homing behavior")]
    public float homingStrength = 5f;

    [Tooltip("Amplitude for wave pattern")]
    public float waveAmplitude = 1f;

    [Tooltip("Frequency for wave pattern")]
    public float waveFrequency = 2f;

    [Tooltip("Radius for spiral pattern")]
    public float spiralRadius = 1f;

    [Tooltip("Speed for spiral pattern")]
    public float spiralSpeed = 2f;

    [Header("Lobbed Settings")]
    [Tooltip("World-units height of the arc peak above the straight-line path to the target. Only used when Behavior = Lobbed.")]
    public float lobbedArcHeight = 3f;

    [Header("Boomerang Settings")]
    [Tooltip("Distance / time graph for the full round trip.\nX = normalized time: 0 = just fired, 1 = trip complete.\nY = normalized distance: 0 = at caster, 1 = at Max Range.\nDraw this as a parabola — peak Y of 1 reaches Max Range, returning to 0 brings it back to the caster.")]
    public AnimationCurve boomerangDistanceCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

    [Header("Rotation")]
    [Tooltip("When enabled, the projectile sprite will not rotate to face its movement direction. Use this for symmetrical sprites (e.g. orbs, circles) or when you want a fixed orientation.")]
    public bool freezeRotation = false;
    [Tooltip("Degrees per second the sprite spins on its own axis. Works independently of Freeze Rotation — when both are active the sprite spins on top of its frozen orientation; when Freeze Rotation is off the sprite still spins on top of its travel-facing rotation.")]
    public float spinSpeed = 0f;

    [Header("Collision Layers")]
    [Tooltip("Layers that count toward pierce limit (usually same as hitLayers)")]
    public LayerMask canPierceLayers;

    [Tooltip("Layers that will destroy the projectile on contact")]
    public LayerMask destroyOnLayers = -1;

    [Header("Destroy Effects")]
    [Tooltip("Visual prefab to spawn when projectile is destroyed (can contain animations, particles, sprites, etc.)")]
    public GameObject destroyVisualPrefab;

    [Tooltip("Sound effect when projectile is destroyed")]
    public AudioClip destroySound;

    [Header("Muzzle Flash Effects")]
    [Tooltip("Particle effect to spawn at fire point when projectile is created")]
    public GameObject muzzleFlashPrefab;
    public AudioClip muzzleFlashSound;
    [Tooltip("Enable muzzle flash light")]
    public bool enableMuzzleLight = false;

    [Tooltip("Color of the muzzle flash light")]
    public Color muzzleLightColor = Color.yellow;

    [Tooltip("Intensity of the muzzle flash light")]
    public float muzzleLightIntensity = 3f;

    [Tooltip("Range of the muzzle flash light")]
    public float muzzleLightRange = 2f;

    [Tooltip("How long the muzzle flash light lasts")]
    public float muzzleLightDuration = 0.1f;
    [Tooltip("Should prefab be spawned locally on weapon or in world space.")]
    public bool localMuzzle = false;

    /// <summary>
    /// Get the DamageTypeData from the database
    /// </summary>
    public DamageTypeData GetDamageType()
    {
        return DamageTypeDatabase.Instance?.GetDamageType(hitbox.damageTypeName);
    }
}
