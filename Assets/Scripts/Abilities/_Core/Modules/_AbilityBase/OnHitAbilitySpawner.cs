using FishNet;
using UnityEngine;

/// <summary>
/// Utility that fires an AbilityDataConfig's effect at a world position on behalf of an owner.
/// Called by EffectData when a triggered-ability proc fires on projectile/melee/explosion hit.
/// Supported ability types: Area, Explosion, Standalone Projectile.
/// </summary>
public static class OnHitAbilitySpawner
{
    /// <summary>
    /// Spawn the effect(s) defined in <paramref name="config"/> at <paramref name="hitPosition"/>,
    /// attributed to <paramref name="owner"/> for damage/modifier purposes.
    /// <paramref name="damageMultiplier"/> and <paramref name="sizeMultiplier"/> are forwarded
    /// from the parent ability so the triggered ability inherits crit/size scaling.
    /// </summary>
    public static void Trigger(AbilityDataConfig config, GameObject owner, Vector3 hitPosition, float damageMultiplier = 1f, float sizeMultiplier = 1f)
    {
        if (config == null || owner == null) return;

        // If the ability is marked as triggered-only and the owner has it in their
        // triggered loadout slot, resolve the per-character version so that trait
        // modifiers authored against that SO reference apply correctly.
        if (config.isTriggeredOnly)
        {
            CharacterAbilityManager mgr = owner.GetComponent<CharacterAbilityManager>();
            if (mgr != null)
                config = mgr.ResolveTriggeredAbility(config);
        }

        var accumulatedOverrides = AbilityModifierRuntime.AccumulateOverridesFromOwner(owner, config);
        AbilityDataConfig effectiveConfig = AbilityModifierRuntime.BuildEffectiveAbilityConfig(config, accumulatedOverrides) ?? config;
        // BuildEffectiveAbilityConfig already bakes sub-config overrides into the returned
        // runtime copy. Rebuilding sub-configs here can yield null when there are no direct
        // path overrides for that slice, which makes a valid triggered ability look unsupported.
        AreaConfig effectiveAreaConfig = effectiveConfig.areaConfig;
        ProjectileConfig effectiveProjectileConfig = effectiveConfig.projectileConfig;
        ExplosionConfig effectiveExplosionConfig = effectiveConfig.explosionConfig;

        string abilityName = effectiveConfig.abilityName;
        var tags = effectiveConfig.abilityTags?.GetAllTags();

        // Compute the owner's AbilitySize modifier so triggered abilities scale correctly
        if (sizeMultiplier == 1f)
        {
            Organism organism = owner.GetComponent<Organism>();
            if (organism != null)
            {
                float abilitySizePercent = organism.AllStats.GetStat("AbilitySize");
                if (abilitySizePercent != 0f)
                    sizeMultiplier = 1f + abilitySizePercent;
            }
        }

        bool abilityExecuted = false;

        if (effectiveAreaConfig != null)
        {
            abilityExecuted |= SpawnAreaAbility(effectiveConfig, effectiveAreaConfig, owner, hitPosition, sizeMultiplier);
        }

        if (effectiveExplosionConfig != null)
        {
            abilityExecuted |= SpawnExplosionAbility(effectiveConfig, effectiveExplosionConfig, owner, hitPosition, sizeMultiplier);
        }

        if (effectiveProjectileConfig != null)
        {
            abilityExecuted |= SpawnProjectileAbility(effectiveConfig, effectiveProjectileConfig, owner, hitPosition, damageMultiplier, tags);
        }

        if (!abilityExecuted)
        {
            Debug.LogWarning($"[OnHitAbilitySpawner] Config '{abilityName}' has no supported ability type for on-hit triggering (Area, Explosion, and Standalone Projectile are supported).");
        }
    }

    private static bool SpawnAreaAbility(AbilityDataConfig parentConfig, AreaConfig areaConfig, GameObject owner, Vector3 spawnPosition, float sizeMultiplier)
    {
        if (parentConfig == null || areaConfig == null)
            return false;

        GameObject areaAbilityGO;

        if (areaConfig.hitbox != null && areaConfig.hitbox.prefab != null)
        {
            areaAbilityGO = Object.Instantiate(areaConfig.hitbox.prefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            GameObject auraPrefab = Resources.Load<GameObject>("Prefabs/Abilities/Aura_Area");
            if (auraPrefab == null)
            {
                Debug.LogError($"[OnHitAbilitySpawner] Aura_Area prefab not found in Resources/Prefabs/Abilities for '{parentConfig.abilityName}'.");
                return false;
            }

            areaAbilityGO = Object.Instantiate(auraPrefab, spawnPosition, Quaternion.identity);
            areaAbilityGO.name = $"TriggeredArea_{parentConfig.abilityName}";
        }

        var networkManager = InstanceFinder.NetworkManager;
        if (networkManager != null && networkManager.IsServerStarted)
        {
            networkManager.ServerManager.Spawn(areaAbilityGO);
        }

        AreaAbility areaAbilityComponent = areaAbilityGO.GetComponentInChildren<AreaAbility>();
        if (areaAbilityComponent == null)
        {
            areaAbilityComponent = areaAbilityGO.AddComponent<AreaAbility>();
        }

        areaAbilityComponent.SetContext(new SubAbilityContext
        {
            rawParentConfig = parentConfig,
            parentConfig = parentConfig,
            owner = owner,
            statOwner = owner
        });
        areaAbilityComponent.InitializeFromConfig(areaConfig);

        if (areaConfig.isAura && areaConfig.followCaster && owner != null)
        {
            areaAbilityComponent.SetCaster(owner.transform);
        }

        areaAbilityComponent.ConfigureParticles(areaConfig);

        if (sizeMultiplier != 1f)
        {
            areaAbilityGO.transform.localScale *= sizeMultiplier;
        }

        areaAbilityComponent.Activate();
        return true;
    }

    private static bool SpawnExplosionAbility(AbilityDataConfig parentConfig, ExplosionConfig explosionConfig, GameObject owner, Vector3 hitPosition, float sizeMultiplier)
    {
        if (parentConfig == null || explosionConfig == null)
            return false;

        GameObject go = new GameObject($"TriggeredExplosion_{parentConfig.abilityName}");
        go.transform.position = hitPosition;
        ExplosionAbility explosion = go.AddComponent<ExplosionAbility>();
        explosion.SetContext(new SubAbilityContext
        {
            rawParentConfig = parentConfig,
            parentConfig = parentConfig,
            owner = owner,
            statOwner = owner
        });
        explosion.Initialize(explosionConfig, sizeMultiplier);
        Debug.Log($"[DmgPipeline] <{parentConfig.abilityName}> Triggered explosion | sizeMult={sizeMultiplier:F2}x");
        return true;
    }

    private static bool SpawnProjectileAbility(AbilityDataConfig parentConfig, ProjectileConfig projectileConfig, GameObject owner, Vector3 hitPosition, float damageMultiplier, System.Collections.Generic.List<string> tags)
    {
        if (parentConfig == null || projectileConfig == null)
            return false;

        Vector3 spawnPos;
        if (parentConfig.autocast)
        {
            spawnPos = owner.transform.position;
        }
        else
        {
            spawnPos = owner.transform.position;
            Transform weaponTransform = owner.transform.Find("WeaponHolder/Weapon");
            if (weaponTransform != null)
                spawnPos = WeaponLaunchPoint.GetLaunchPosition(weaponTransform);
        }

        Vector3 dir = hitPosition - spawnPos;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.right;

        ProjectileSpawner.SpawnProjectiles(
            projectileConfig,
            spawnPos,
            dir.normalized,
            owner,
            damageMultiplier,
            parentConfig.abilityName,
            tags,
            isAutocast: parentConfig.autocast,
            parentConfig: parentConfig
        );
        return true;
    }
}
