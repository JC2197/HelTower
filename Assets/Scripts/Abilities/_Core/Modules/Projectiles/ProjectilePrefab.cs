using UnityEngine;

/// <summary>
/// Projectile component that can override hit effects from ProjectileConfig.
/// Attach to projectile prefabs to customize visual/audio effects per projectile type.
/// Overrides applied: hit effects, destroy effects, muzzle flash (visual + sound), and status effects.
/// </summary>
public class ProjectilePrefab : Projectile
{
    [Header("Projectile-Specific Overrides")]
    [Tooltip("Enable to override hit effects for this specific projectile prefab")]
    public bool overrideHitEffects = false;
    
    [Header("Visual Hit Effect Overrides")]
    [Tooltip("Override visual hit effect prefab (can contain animations, particles, sprites, etc.)")]
    public GameObject hitVisualPrefabOverride;
    
    [Tooltip("Override hit sound")]
    public AudioClip hitSoundOverride;
    
    [Tooltip("Override hit flash color")]
    public Color hitFlashColorOverride = Color.white;
    
    [Header("Destroy Effect Overrides")]
    [Tooltip("Enable to override destroy effects for this specific projectile prefab")]
    public bool overrideDestroyEffects = false;
    
    [Tooltip("Override destroy visual prefab (can contain animations, particles, sprites, etc.)")]
    public GameObject destroyVisualPrefabOverride;
    
    [Tooltip("Override destroy sound")]
    public AudioClip destroySoundOverride;
    
    [Header("Muzzle Flash Overrides")]
    [Tooltip("Enable to override muzzle flash for this specific projectile prefab")]
    public bool overrideMuzzleFlash = false;
    
    [Tooltip("Override muzzle flash particle effect")]
    public GameObject muzzleFlashPrefabOverride;

    [Tooltip("Override muzzle flash sound")]
    public AudioClip muzzleFlashSoundOverride;
    
    [Tooltip("Override muzzle flash light settings")]
    public bool overrideMuzzleLight = false;
    public Color muzzleLightColorOverride = Color.yellow;
    public float muzzleLightIntensityOverride = 3f;
    public float muzzleLightRangeOverride = 2f;
    public float muzzleLightDurationOverride = 0.1f;
    
    [Header("On Hit Status Effect Overrides")]
    [Tooltip("Enable to override status effects for this specific projectile prefab")]
    public bool overrideStatusEffects = false;
    
    [Tooltip("Override on-hit status effects")]
    public EffectData onHitEffectsOverride = new EffectData();
    
    /// <summary>
    /// Apply this projectile's overrides to a ProjectileConfig
    /// Called by ProjectileSpawner before initializing the projectile
    /// </summary>
    /// <param name="config">Config to apply overrides to</param>
    /// <returns>Modified config with overrides applied</returns>
    public ProjectileConfig ApplyOverrides(ProjectileConfig config)
    {
        if (config == null) return config;

        Debug.Log($"[ProjectilePrefab] ApplyOverrides called on {gameObject.name}");

        // Shallow-copy every field via reflection so no new ProjectileConfig fields are ever missed.
        ProjectileConfig modifiedConfig = new ProjectileConfig();
        foreach (var f in typeof(ProjectileConfig).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            f.SetValue(modifiedConfig, f.GetValue(config));

        // Apply prefab-specific overrides on top
        if (overrideHitEffects || overrideStatusEffects)
        {
            // Clone the shared hitbox so per-prefab overrides never mutate the ability's config.
            HitboxConfig clonedHitbox = new HitboxConfig();
            foreach (var f in typeof(HitboxConfig).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                f.SetValue(clonedHitbox, f.GetValue(config.hitbox));
            modifiedConfig.hitbox = clonedHitbox;

            if (overrideHitEffects)
            {
                clonedHitbox.effects = new HitFeedbackModule
                {
                    hitEffectPrefab = hitVisualPrefabOverride,
                    hitSound = hitSoundOverride,
                    hitFlashColor = hitFlashColorOverride
                };
            }

            if (overrideStatusEffects)
                clonedHitbox.onHitEffects = onHitEffectsOverride;
        }

        if (overrideDestroyEffects)
        {
            modifiedConfig.destroyVisualPrefab = destroyVisualPrefabOverride;
            modifiedConfig.destroySound = destroySoundOverride;
        }

        if (overrideMuzzleFlash)
        {
            modifiedConfig.muzzleFlashPrefab = muzzleFlashPrefabOverride;
            if (muzzleFlashSoundOverride != null)
                modifiedConfig.muzzleFlashSound = muzzleFlashSoundOverride;
        }

        if (overrideMuzzleLight)
        {
            modifiedConfig.enableMuzzleLight = true;
            modifiedConfig.muzzleLightColor = muzzleLightColorOverride;
            modifiedConfig.muzzleLightIntensity = muzzleLightIntensityOverride;
            modifiedConfig.muzzleLightRange = muzzleLightRangeOverride;
            modifiedConfig.muzzleLightDuration = muzzleLightDurationOverride;
        }

        return modifiedConfig;
    }
}
