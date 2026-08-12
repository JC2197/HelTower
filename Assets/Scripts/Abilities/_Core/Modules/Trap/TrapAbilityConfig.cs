using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Configuration for trap abilities - dormant objects that trigger when enemies enter range
/// Similar to constructs but inanimate and trigger-based
/// </summary>
[System.Serializable]
public class TrapAbilityConfig
{
    [Tooltip("The trap GameObject to spawn (should have Animator with 'Idle' and 'Trigger' animations)")]
    public GameObject trapPrefab;
    
    // [Tooltip("Maximum distance from caster to place trap")]
    public float maxRange = 10f;
    
    [Tooltip("Spawn at caster's position (ignores mouse position)")]
    public bool spawnAtCaster = false;
    
    [Tooltip("Spawn at mouse cursor position (clamped to maxRange)")]
    public bool spawnAtMouse = true;
    
    [Tooltip("Maximum number of traps that can exist at once. 0 = unlimited")]
    public int maxTraps = 5;
    
    [Tooltip("What happens when max traps reached")]
    public TrapLimitBehavior limitBehavior = TrapLimitBehavior.DestroyOldest;
    
    [Tooltip("How long trap exists before auto-destroying. -1 = permanent")]
    public float lifetime = 60f;
    
    [Tooltip("Destroy trap when lifetime expires")]
    public bool destroyOnLifetimeEnd = true;
    
    [Header("Trigger Settings")]
    [Tooltip("Detection range for triggering (radius around trap)")]
    public float triggerRange = 2f;
    
    [Tooltip("Delay before trap becomes active and can be triggered")]
    public float armingDelay = 0.5f;
    
    [Tooltip("Layer mask for what can trigger the trap (typically Enemy layer)")]
    public LayerMask triggerLayers = 1 << 6; // Enemy layer by default
    
    [Tooltip("Can trap be triggered by multiple enemies or only once")]
    public bool singleTrigger = true;
    
    [Tooltip("Cooldown between triggers if singleTrigger is false")]
    public float retriggerCooldown = 2f;
    
    [Header("Animation")]
    [Tooltip("Animation to play when trap is idle/armed")]
    public string idleAnimationName = "Idle";
    
    [Tooltip("Animation to play when trap is triggered")]
    public string triggerAnimationName = "Trigger";
    
    [Tooltip("Delay before destroying trap after trigger animation")]
    public float destroyDelay = 0.5f;
    
    [Header("Triggered Ability")]
    [Tooltip("Type of ability to trigger when enemy enters range")]
    public TrapAbilityType abilityType = TrapAbilityType.Area;
    
    [Tooltip("Area ability config (used if abilityType = Area)")]
    public AreaConfig areaConfig;
    
    [Tooltip("Projectile config (used if abilityType = Projectile)")]
    public ProjectileConfig projectileConfig;
    
    [Tooltip("Number of projectiles to spawn in all directions (0 = aim at triggering enemy)")]
    public int projectileCount = 0;
    
    [Tooltip("Spread angle for multiple projectiles (360 = full circle)")]
    public float projectileSpread = 360f;
    
    [Tooltip("Explosion config (used if abilityType = Explosion)")]
    public ExplosionConfig explosionConfig;
    
    [Header("Visual Effects")]
    [Tooltip("Particle effect to play when trap is placed")]
    public GameObject spawnEffect;
    
    [Tooltip("Particle effect to play when trap is triggered")]
    public GameObject triggerEffect;
    
    [Tooltip("Show visual indicator of trigger range")]
    public bool showTriggerRadius = true;
    
    [Tooltip("Color of trigger radius indicator")]
    public Color triggerRadiusColor = new Color(1f, 0f, 0f, 0.3f);
}

/// <summary>
/// What type of ability the trap triggers
/// </summary>
public enum TrapAbilityType
{
    Area,           // Spawn area effect at trap location
    Projectile,     // Fire projectile(s) from trap
    Explosion       // Immediate damage in radius (no ongoing effect)
}

/// <summary>
/// Behavior when max trap count is reached
/// </summary>
public enum TrapLimitBehavior
{
    DestroyOldest,  // Remove oldest trap and spawn new one
    PreventSpawn,   // Don't spawn new trap if at limit
    ReplaceClosest  // Replace trap closest to new spawn position
}
