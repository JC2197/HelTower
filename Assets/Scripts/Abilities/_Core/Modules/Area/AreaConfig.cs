using UnityEngine;

/// <summary>
/// Configuration for area-based effects (ground effects, AoE zones, auras, etc.).
/// Inline serializable configuration.
/// The collider lives on the spell prefab. Use <c>scale</c> to resize it uniformly.
/// </summary>
[System.Serializable]
public class AreaConfig
{
    [Header("Hitbox")]
    [Tooltip("Shared hitbox config: prefab (visual + collider), scale, hit layers, damage, weapon damage, knockback, pull, on-hit effects, life steal, and hit feedback. Scale is applied at runtime.")]
    public HitboxConfig hitbox = new HitboxConfig();

    [Header("Area Settings")]
    [Tooltip("Is this a point-blank AoE (centered on caster)?")]
    public bool isPointBlank = false;
    
    [Tooltip("Maximum distance from caster to spawn area")]
    public float range = 10f;

    [Tooltip("Number of areas spawned per cast. For autocast, each area targets a different enemy.")]
    public int areaCount = 1;
    
    [Tooltip("Is this an aura that stays active with the parent object?")]
    public bool isAura = false;
    
    [Tooltip("Aura follows the caster (stays parented to caster's transform)")]
    public bool followCaster = true;
    
    [Tooltip("Delay before aura appears (in seconds)")]
    public float auraDelay = 0f;
    
    [Tooltip("How long the area stays active (0 = destroy after spawn animation, -1 = permanent)")]
    public float duration = 3f;

    [Header("Damage")]
    [Tooltip("How often the area applies damage to entities inside (0 = only on enter)")]
    public float damageInterval = 0.5f;

    [Tooltip("Play the spellPrefab particle systems on each damage tick?")]
    public bool hasDamageTick = false;
    
    [Tooltip("Apply damage over time to entities in the area?")]
    public bool dealsDamageOverTime = false;
    
    [Tooltip("Damage per second")]
    public float damagePerSecond = 5f;
    
    [Tooltip("How often DoT damage ticks (e.g., 0.5 = twice per second)")]
    public float dotInterval = 0.5f;
    
    [Tooltip("Duration of damage over time effect in seconds")]
    public float dotDuration = 3f;
    
    [Tooltip("Particle effect to attach to target while DoT is active")]
    public ParticleSystem dotParticleEffectPrefab;

    [Tooltip("If true, DoT particle spawns at feet (bottom) instead of center")] 
    public bool startParticlesFromFeet = false;

    [Tooltip("Fade in the area visual over time?")]
    public bool hasFadeIn = false;
    
    [Tooltip("Duration of the fade-in effect in seconds")]
    public float fadeInDuration = 0.5f;
    
    [Header("Effects")]
    [Tooltip("Sound effect on spawn")]
    public AudioClip spawnSound;

    [Header("Light (Optional)")]
    [Tooltip("Add a light to the area?")]
    public bool hasLight = false;
    
    [Tooltip("Color of the light")]
    public Color lightColor = Color.white;
    
    [Tooltip("Intensity of the light")]
    public float lightIntensity = 1f;
    
    [Tooltip("Radius/range of the light")]
    public float lightRadius = 5f;

    // ─── Aura-mode fields ─────────────────────────────────────────────────────
    // Shown/used only when isAura = true. Aura runtime behaviour lives in Aura.cs
    // while placed-area behaviour lives in AreaAbility.cs.

    [Header("Aura (isAura = true)")]
    [Tooltip("Enable this aura (false = inactive)")]
    public bool enabled = true;

    [Tooltip("Offset of the aura center from owner origin")]
    public Vector2 offset = Vector2.zero;
}
