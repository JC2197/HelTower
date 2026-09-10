using UnityEngine;

/// <summary>
/// Utility class for spawning area spells from AreaAbilityConfig.
/// Handles instantiation, configuration, and initialization.
/// </summary>
public static class AreaAbilitySpawner
{
    /// <summary>
    /// Spawns an area spell at the specified position
    /// </summary>
    /// <param name="config">Area spell configuration</param>
    /// <param name="spawnPosition">World position to spawn at</param>
    /// <param name="damageMultiplier">Optional damage multiplier</param>
    public static void SpawnAreaAbility(AreaConfig config, Vector3 spawnPosition, float damageMultiplier = 1f)
    {
        if (config == null || config.hitbox.prefab == null)
        {
            Debug.LogError("AreaAbilityConfig or spell prefab is null!");
            return;
        }
        GameObject spellObj;
        PlayerController auraOwner = null;
        if (config.isAura)
        {
            auraOwner = PlayerUtil.GetPlayer();
            if (auraOwner != null)
            {
                spawnPosition = auraOwner.transform.position;
            }
            else
            {
                Debug.LogWarning("Player not found for aura spell, using original spawn position");
            }
            spellObj = Object.Instantiate(config.hitbox.prefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            spellObj = Object.Instantiate(config.hitbox.prefab, spawnPosition, Quaternion.identity);
        }
        // Instantiate visual prefab (no script on it)

        // Get or add AreaAbility component
        AreaAbility spell = spellObj.GetComponent<AreaAbility>();
        if (spell == null)
        {
            spell = spellObj.AddComponent<AreaAbility>();
        }

        if (spell != null)
        {
            // Create temporary config with damage multiplier applied
            AreaConfig tempConfig = new AreaConfig
            {
                // Shared hitbox (cloned so the damage multiplier doesn't mutate the source config)
                hitbox = config.hitbox.Clone(),

                // Area Settings
                isPointBlank = config.isPointBlank,
                range = config.range,
                isAura = config.isAura,
                followCaster = config.followCaster,
                auraDelay = config.auraDelay,
                duration = config.duration,

                // Damage
                damageInterval = config.damageInterval,
                hasDamageTick = config.hasDamageTick,
                hasFadeIn = config.hasFadeIn,
                fadeInDuration = config.fadeInDuration,

                // Effects
                spawnSound = config.spawnSound,

                // Light
                hasLight = config.hasLight,
                lightColor = config.lightColor,
                lightIntensity = config.lightIntensity,
                lightRadius = config.lightRadius
            };
            // Apply the damage multiplier on the cloned hitbox.
            tempConfig.hitbox.damage = config.hitbox.damage * damageMultiplier;

            // Configure spell from config
            spell.InitializeFromConfig(tempConfig);

            if (auraOwner != null)
                spell.SetCaster(auraOwner.transform);

            // Configure particles to match area shape
            spell.ConfigureParticles(tempConfig);

            if (!tempConfig.isAura)
                spell.Activate();
        }
        else
        {
            Debug.LogError($"Failed to add AreaAbility component to {config.hitbox.prefab.name}!");
            Object.Destroy(spellObj);
        }
    }

    /// <summary>
    /// Spawns an area spell with a spawn delay
    /// </summary>
    public static void SpawnAreaAbilityDelayed(AreaConfig config, Vector3 spawnPosition, float delay, float damageMultiplier = 1f)
    {
        // Create a temporary GameObject to handle the delay
        GameObject delayObj = new GameObject("SpellDelayHandler");
        var handler = delayObj.AddComponent<SpellDelayHandler>();
        handler.Initialize(config, spawnPosition, delay, damageMultiplier);
    }

    /// <summary>
    /// Helper component for handling delayed spell spawning
    /// </summary>
    private class SpellDelayHandler : MonoBehaviour
    {
        private AreaConfig config;
        private Vector3 position;
        private float spawnTime;
        private float damageMultiplier;

        public void Initialize(AreaConfig cfg, Vector3 pos, float delay, float damageMult)
        {
            config = cfg;
            position = pos;
            spawnTime = Time.time + delay;
            damageMultiplier = damageMult;
        }

        private void Update()
        {
            if (Time.time >= spawnTime)
            {
                SpawnAreaAbility(config, position, damageMultiplier);
                Destroy(gameObject);
            }
        }
    }
}
