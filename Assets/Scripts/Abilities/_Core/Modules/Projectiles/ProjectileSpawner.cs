using UnityEngine;
using UnityEngine.Rendering.Universal;
using FishNet;

/// <summary>
/// Utility class for spawning projectiles from ProjectileConfig.
/// Handles multi-shot, spread, and initialization.
/// Supports both local (single-player) and networked (multiplayer) spawning.
/// </summary>
public static class ProjectileSpawner
{
    private const string AbilityPipelineTag = "[Ability pipeline]";

    /// <summary>
    /// Spawns projectile(s) based on config (backwards compatibility)
    /// </summary>
    /// <param name="config">Projectile configuration</param>
    /// <param name="spawnPosition">World position to spawn at</param>
    /// <param name="direction">Direction to fire</param>
    /// <param name="character">Character GameObject (to query CharacterTraitManager for modifiers)</param>
    /// <param name="damageMultiplier">Optional damage multiplier</param>
    /// <param name="abilityName">Name of the ability spawning these projectiles (for tag-based modifiers)</param>
    /// <param name="abilityTags">Tags of the ability (for tag-based damage modifiers)</param>
    /// <param name="cursorPosition">Cursor position for homing target search (null uses owner position)</param>
    /// <param name="isAutocast">True if ability was fired via autocast (homing uses owner position instead of cursor)</param>
    public static void SpawnProjectiles(
        ProjectileConfig config,
        Vector3 spawnPosition,
        Vector3 direction,
        GameObject character = null,
        float damageMultiplier = 1f,
        string abilityName = null,
        System.Collections.Generic.List<string> abilityTags = null,
        float passedTime = 0f,
        uint tick = 0,
        Vector3? cursorPosition = null,
        bool isAutocast = false,
        AbilityDataConfig parentConfig = null,
        GameObject muzzleFlashEntity = null)
    {
        if (config == null || config.hitbox.prefab == null)
        {
            Debug.LogError("ProjectileConfig or projectile prefab is null!");
            return;
        }

        // Log spawn info for debugging
        float angleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        string ownerName = character != null ? character.name : "Unknown";
        Debug.Log($"{AbilityPipelineTag} ProjectileSpawner.SpawnProjectiles: owner={ownerName}, projectile={config.hitbox.prefab.name}, pos={spawnPosition}, dir={direction}, angle={angleDeg:F1}, ability={abilityName}, autocast={isAutocast}, passedTime={passedTime:F3}, tick={tick}");
        Debug.Log($"[ProjectileSpawner] {ownerName} spawning projectile: pos={spawnPosition}, dir={direction}, angle={angleDeg:F1}°, ability={abilityName}");

        // Always operate on a shallow copy so we never mutate the original ScriptableObject asset.
        ProjectileConfig workingConfig = new ProjectileConfig();
        foreach (var f in typeof(ProjectileConfig).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            f.SetValue(workingConfig, f.GetValue(config));
        config = workingConfig;

        // Check if projectile prefab has ProjectilePrefab component with overrides
        ProjectilePrefab projectilePrefabComponent = config.hitbox.prefab.GetComponent<ProjectilePrefab>();
        if (projectilePrefabComponent != null)
        {
            config = projectilePrefabComponent.ApplyOverrides(config);
            Debug.Log($"[ProjectileSpawner] Applied ProjectilePrefab overrides from {config.hitbox.prefab.name}");
        }

        // Get projectile count modifier from CharacterTraitManager if available.
        // ProjectileSpawner owns only the per-volley multishot fan-out. Timed salvo sequencing
        // is already handled by DataDrivenAbility.SalvoCoroutine before each call into this method.
        int projectileCount = config.hasMultiShot ? config.projectileCount : 1;
        float sizeMultiplier = 1f;

        Debug.Log($"<color=cyan>[ProjectileCount] ability={abilityName}, hasMultiShot={config.hasMultiShot}, config.projectileCount={config.projectileCount} → initial count={projectileCount}</color>");

        if (character != null)
        {
            CharacterTraitManager traitManager = character.GetComponent<CharacterTraitManager>();
            if (traitManager != null)
            {
                // Apply projectile count modifier (only if hasMultiShot is enabled)
                if (config.hasMultiShot)
                {
                    float projectileCountModifier = traitManager.CalculateFinalStat("ProjectileCount", 0f);
                    int prevCount = projectileCount;
                    projectileCount = Mathf.RoundToInt(config.projectileCount + projectileCountModifier);
                    Debug.Log($"<color=cyan>[ProjectileCount] TraitManager modifier: CalculateFinalStat(\"ProjectileCount\", 0) = {projectileCountModifier}  →  {config.projectileCount} (base) + {projectileCountModifier} (trait) = {config.projectileCount + projectileCountModifier} → rounded = {projectileCount}</color>");
                }
                else
                {
                    Debug.Log($"<color=cyan>[ProjectileCount] hasMultiShot=false, skipping trait modifier. Firing 1 projectile.</color>");
                }

                // Apply size modifier from AbilitySize stat (percentage increase)
                Organism organism = character.GetComponent<Organism>();
                if (organism != null)
                {
                    float abilitySizePercent = organism.AllStats.GetStat("AbilitySize");
                    if (abilitySizePercent != 0f)
                    {
                        sizeMultiplier = 1f + (abilitySizePercent);
                        Debug.Log($"[ProjectileSpawner] AbilitySize stat: +{abilitySizePercent}%, multiplier: {sizeMultiplier}");
                    }
                }

            }
        }

        // Spawn muzzle flash effect at spawn position, attached to muzzleFlashEntity (if provided) or character
        SpawnMuzzleFlash(config, spawnPosition, direction, muzzleFlashEntity != null ? muzzleFlashEntity : character);

        int totalProjectiles = Mathf.Max(1, projectileCount);
        Debug.Log($"<color=cyan>[ProjectileCount] FINAL: spawning {totalProjectiles} projectile(s) for ability={abilityName} (projectileCount={projectileCount}, salvoSize handled upstream={Mathf.Max(1, config.salvoSize)})</color>");

        // Spawn projectiles using simplified internal method
        SpawnProjectilesInternal(
            config,
            totalProjectiles,
            spawnPosition,
            direction,
            damageMultiplier,
            character,
            abilityName,
            abilityTags,
            passedTime,
            tick,
            sizeMultiplier,
            cursorPosition,
            isAutocast,
            parentConfig
        );
    }

    /// <summary>
    /// Spawns projectiles from a weapon transform, automatically finding LaunchZone.
    /// Weapon classes should use this method for proper launch point handling.
    /// </summary>
    /// <param name="config">Projectile configuration</param>
    /// <param name="weaponTransform">Weapon transform (will search for LaunchZone child)</param>
    /// <param name="character">Character GameObject (for trait modifiers)</param>
    /// <param name="damageMultiplier">Optional damage multiplier</param>
    public static void SpawnProjectilesFromWeapon(ProjectileConfig config, Transform weaponTransform, GameObject character = null, float damageMultiplier = 1f)
    {
        if (weaponTransform == null)
        {
            Debug.LogError("[ProjectileSpawner] Weapon transform is null!");
            return;
        }

        // Get launch position and direction from weapon
        Vector3 spawnPosition = WeaponLaunchPoint.GetLaunchPosition(weaponTransform);
        Vector3 direction = WeaponLaunchPoint.GetLaunchDirection(weaponTransform);

        // Spawn projectiles using the launch point
        SpawnProjectiles(config, spawnPosition, direction, character, damageMultiplier);
    }

    /// <summary>
    /// Get ability tags for the character's current ability
    /// </summary>
    private static System.Collections.Generic.List<string> GetCurrentAbilityTags(GameObject character)
    {
        if (character == null) return null;

        // Try to get from CharacterAbilityManager
        var abilityManager = character.GetComponent<CharacterAbilityManager>();
        if (abilityManager == null) return null;

        var primaryAbility = abilityManager.GetWeaponAbility();
        if (primaryAbility == null) return null;

        var tags = primaryAbility.Tags;
        if (tags == null) return null;

        return tags.SelectedTags;
    }

    /// <summary>
    /// Copy all fields from source EffectData to destination
    /// </summary>
    private static void CopyEffectData(EffectData source, EffectData destination)
    {
        // Copy bleed
        destination.canBleed = source.canBleed;
        destination.bleedEffect = source.bleedEffect;
        destination.bleedDamage = source.bleedDamage;
        destination.bleedDuration = source.bleedDuration;
        destination.bleedApplicationChance = source.bleedApplicationChance;

        // Copy burn
        destination.canBurn = source.canBurn;
        destination.burnEffect = source.burnEffect;
        destination.burnDamage = source.burnDamage;
        destination.burnDuration = source.burnDuration;
        destination.burnApplicationChance = source.burnApplicationChance;

        // Copy poison
        destination.canPoison = source.canPoison;
        destination.poisonEffect = source.poisonEffect;
        destination.poisonDamage = source.poisonDamage;
        destination.poisonDuration = source.poisonDuration;
        destination.poisonApplicationChance = source.poisonApplicationChance;

        // Copy root
        destination.canRoot = source.canRoot;
        destination.rootEffect = source.rootEffect;
        destination.rootDuration = source.rootDuration;
        destination.rootApplicationChance = source.rootApplicationChance;

        // Copy slow
        destination.canSlow = source.canSlow;
        destination.slowEffect = source.slowEffect;
        destination.slowDuration = source.slowDuration;
        destination.slowApplicationChance = source.slowApplicationChance;

        // Copy stun
        destination.canStun = source.canStun;
        destination.stunEffect = source.stunEffect;
        destination.stunDuration = source.stunDuration;
        destination.stunApplicationChance = source.stunApplicationChance;

        // Copy triggered ability
        destination.canTriggerAbility = source.canTriggerAbility;
        foreach (var triggeredAbility in source.triggeredAbilityConfigs)
        {
            var newTriggeredAbility = new EffectData.TriggeredAbilityConfig
            {
                abilityConfig = triggeredAbility.abilityConfig,
                triggerChance = triggeredAbility.triggerChance,
                triggerTiming = triggeredAbility.triggerTiming
            };
            destination.triggeredAbilityConfigs = destination.triggeredAbilityConfigs ?? new EffectData.TriggeredAbilityConfig[0];
            System.Array.Resize(ref destination.triggeredAbilityConfigs, destination.triggeredAbilityConfigs.Length + 1);
            destination.triggeredAbilityConfigs[destination.triggeredAbilityConfigs.Length - 1] = newTriggeredAbility;
        }
    }

    /// <summary>
    /// Get the current ability name from the character (for matching status effect modifiers)
    /// </summary>
    private static string GetCurrentAbilityName(GameObject character)
    {
        if (character == null) return "";

        // Try to get from CharacterAbilityManager
        var abilityManager = character.GetComponent<CharacterAbilityManager>();
        if (abilityManager != null)
        {
            var primaryAbility = abilityManager.GetWeaponAbility();
            if (primaryAbility != null)
            {
                return primaryAbility.AbilityName;
            }
        }

        return "";
    }

    /// <summary>
    /// Internal projectile spawning - handles spread, network spawning, and initialization.
    /// </summary>
    private static void SpawnProjectilesInternal(
        ProjectileConfig config,
        int projectileCount,
        Vector3 spawnPosition,
        Vector3 direction,
        float damageMultiplier,
        GameObject owner,
        string abilityName,
        System.Collections.Generic.List<string> abilityTags,
        float passedTime,
        uint tick,
        float sizeMultiplier = 1f,
        Vector3? cursorPosition = null,
        bool isAutocast = false,
        AbilityDataConfig parentConfig = null)
    {
        int count = Mathf.Max(1, projectileCount);

        // Calculate spread angles
        float angleStep = 0f;
        float startAngle = 0f;

        if (count > 1)
        {
            if (config.spreadAnglePerProjectile > 0f)
            {
                angleStep = config.spreadAnglePerProjectile;
                startAngle = -(angleStep * (count - 1)) / 2f;
            }
            else if (config.spreadAngle >= 360f)
            {
                // Full-circle spread: divide evenly by N so first and last projectiles
                // don't overlap (e.g. 3 projectiles → 120° apart, 2 → 180° apart).
                angleStep = config.spreadAngle / count;
                startAngle = 0f;
            }
            else
            {
                angleStep = config.spreadAngle / (count - 1);
                startAngle = -config.spreadAngle / 2f;
            }
        }

        // Check network state once before the loop
        var networkManager = InstanceFinder.NetworkManager;
        bool isNetworkActive = networkManager != null && (networkManager.IsServerStarted || networkManager.IsClientStarted);
        bool isServer = networkManager != null && networkManager.IsServerStarted;

        // Spawn each projectile
        for (int i = 0; i < count; i++)
        {
            // Calculate spread angle for this projectile
            float currentSpreadAngle = count > 1 ? startAngle + (angleStep * i) : 0f;

            // Apply spread to direction
            Quaternion spreadRotation = Quaternion.Euler(0, 0, currentSpreadAngle);
            Vector3 projectileDirection = spreadRotation * direction.normalized;

            // Instantiate projectile
            GameObject projectileObj = Object.Instantiate(config.hitbox.prefab, spawnPosition, Quaternion.identity);

            // Apply size modifier
            if (sizeMultiplier != 1f)
            {
                projectileObj.transform.localScale *= sizeMultiplier;
            }

            // Get projectile component first - needed for both paths
            Projectile projectile = projectileObj.GetComponent<Projectile>();
            if (projectile == null)
            {
                Debug.LogWarning($"Projectile prefab {config.hitbox.prefab.name} missing Projectile component!");
                Object.Destroy(projectileObj);
                continue;
            }

            // Handle network spawning
            bool isPredictive = false;
            if (isNetworkActive)
            {
                if (isServer)
                {
                    // Server: Network spawn so all clients can see the projectile
                    var networkObject = projectileObj.GetComponent<FishNet.Object.NetworkObject>();
                    if (networkObject != null)
                    {
                        networkManager.ServerManager.Spawn(projectileObj);
                    }
                    else
                    {
                        Debug.LogWarning($"[ProjectileSpawner] Projectile prefab {config.hitbox.prefab.name} has no NetworkObject - local only!");
                    }
                }
                else
                {
                    // Client: Spawn local predictive projectile for instant visual feedback
                    // This projectile is visual-only (no damage, no network sync)
                    // The server will spawn the real authoritative projectile via ServerRpc
                    isPredictive = true;
                    projectile.SetupAsPredictive();
                }
            }

            // Initialize from config
            projectile.InitializeFromConfig(config);

            Debug.Log($"[ProjectileSpawner] After InitializeFromConfig: isPredictive={isPredictive}, owner={(owner != null ? owner.name : "NULL")}");

            // Set runtime data (only needed for authoritative projectiles that deal damage)
            if (!isPredictive)
            {
                if (owner != null)
                {
                    Debug.Log($"[ProjectileSpawner] Calling SetOwner with owner: {owner.name}");
                    projectile.SetOwner(owner);
                }
                else
                {
                    Debug.LogWarning($"[ProjectileSpawner] Owner is NULL - cannot call SetOwner!");
                }

                if (!string.IsNullOrEmpty(abilityName) && abilityTags != null)
                {
                    projectile.SetAbilityInfo(abilityName, abilityTags);
                }

                if (damageMultiplier != 1f)
                {
                    projectile.SetDamageMultiplier(damageMultiplier);
                }

                if (parentConfig != null)
                {
                    projectile.SetParentConfig(parentConfig);
                }
            }

            // Initialize movement (must be called after InitializeFromConfig)
            // Predictive projectiles don't use passedTime - no lag compensation needed
            projectile.Initialize(spawnPosition, projectileDirection, config.speed, isPredictive ? 0f : passedTime);

            // Lobbed projectiles need a target world position to define the arc.
            // Use cursor/target position when available, otherwise fall back to a point
            // projected along the fire direction using lifetime * speed as max range.

            if (config.behavior == ProjectileBehavior.Lobbed)
            {
                Vector3 lobbedTarget = cursorPosition.HasValue
                    ? cursorPosition.Value
                    : spawnPosition + projectileDirection * (config.speed * (config.useLifetime ? config.lifetime : 10f));
                lobbedTarget.z = 0f;
                projectile.SetLobbedTarget(lobbedTarget);
            }

            // Set homing info for homing projectiles (search center for target acquisition)
            if (config.behavior == ProjectileBehavior.Homing)
            {
                projectile.SetHomingInfo(cursorPosition ?? spawnPosition, isAutocast);
            }
            
            // Server broadcasts to clients for smooth interpolation
            if (isServer)
            {
                projectile.RpcClientInitialize(spawnPosition, projectileDirection, config.speed, tick);

                // Broadcast muzzle flash for first projectile only
                if (i == 0)
                {
                    float muzzleAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    projectile.BroadcastMuzzleFlash(spawnPosition, muzzleAngle);
                }
            }
        }
    }

    /// <summary>
    /// Spawns muzzle flash particle effect and light at fire position
    /// </summary>
    private static void SpawnMuzzleFlash(ProjectileConfig config, Vector3 position, Vector3 direction, GameObject character)
    {
        if (config == null) return;

        // Try to find weapon transform to attach muzzle flash to
        Transform weaponTransform = null;
        if (character != null)
        {
            // Look for Weapon child object
            weaponTransform = character.transform.Find("WeaponHolder/Weapon");
        }

        if(config.muzzleFlashSound != null)
        {
            AudioManager.Instance.PlaySpatialSound(config.muzzleFlashSound, position, 1f, Random.Range(0.9f, 1.1f));
        }

        // Spawn muzzle flash particles
        if (config.muzzleFlashPrefab != null)
        {
            // Calculate rotation to face fire direction
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0, 0, angle);

            ParticleSystem muzzleFlash = Object.Instantiate(config.muzzleFlashPrefab, position, rotation);

            // Attach to weapon if found, otherwise leave in world space
            if (weaponTransform != null)
            {
                muzzleFlash.transform.SetParent(weaponTransform, true);
            }


            // Auto-destroy after particle lifetime
            var main = muzzleFlash.main;
            Object.Destroy(muzzleFlash.gameObject, main.duration + main.startLifetime.constantMax);
        }

        // Spawn muzzle flash light
        if (config.enableMuzzleLight)
        {
            GameObject lightObj = new GameObject("MuzzleFlashLight");
            lightObj.transform.position = position;

            // Attach to weapon if found
            if (weaponTransform != null)
            {
                lightObj.transform.SetParent(weaponTransform, true);
            }

            Light2D light2D = lightObj.AddComponent<Light2D>();
            light2D.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            light2D.color = config.muzzleLightColor;
            light2D.intensity = config.muzzleLightIntensity;
            light2D.pointLightOuterRadius = config.muzzleLightRange;

            // Fade out and destroy
            MuzzleLightFader fader = lightObj.AddComponent<MuzzleLightFader>();
            fader.Initialize(config.muzzleLightDuration);
        }
    }
}

/// <summary>
/// Helper component to fade out muzzle flash light over time
/// </summary>
public class MuzzleLightFader : MonoBehaviour
{
    private Light2D light2D;
    private float duration;
    private float elapsed;
    private float startIntensity;

    public void Initialize(float fadeDuration)
    {
        light2D = GetComponent<Light2D>();
        duration = fadeDuration;
        startIntensity = light2D.intensity;
        elapsed = 0f;
    }

    private void Update()
    {
        if (light2D == null) return;

        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        // Fade out intensity
        light2D.intensity = Mathf.Lerp(startIntensity, 0f, t);

        // Destroy when done
        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}