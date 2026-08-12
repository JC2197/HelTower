using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages passive aura abilities for the player.
/// Aura abilities are always-on effects that follow the player and do not require activation.
/// This component is driven by CharacterAbilityManager when the ability loadout is loaded.
/// </summary>
public class PlayerAuraManager : MonoBehaviour
{
    private readonly Dictionary<string, Aura> _activeAuras = new Dictionary<string, Aura>();

    /// <summary>
    /// Registers and activates a single aura from an AbilityDataConfig marked with isAuraAbility.
    /// Creates a child GameObject with an Aura component and initializes it with the config's areaConfig.
    /// Applies trait modifiers to the aura config before initialization.
    /// </summary>
    public void AddAura(AbilityDataConfig abilityDataConfig)
    {
        if (abilityDataConfig == null || !abilityDataConfig.isAuraAbility || abilityDataConfig.areaConfig == null)
            return;

        string key = abilityDataConfig.abilityName;

        // Don't double-register
        if (_activeAuras.ContainsKey(key))
            return;

        AreaConfig effectiveAreaConfig = BuildEffectiveAuraConfig(abilityDataConfig);
        AreaConfig traitEffectiveConfig = effectiveAreaConfig; // capture before AbilitySize is applied

        // Apply AbilitySize stat so auras respect the same sizing stat as regular area abilities
        Organism organism = GetComponent<Organism>();
        if (organism != null)
        {
            float abilitySizePercent = organism.AllStats.GetStat("AbilitySize");
            if (abilitySizePercent != 0f)
            {
                // Clone so we don't mutate the shared config reference
                AreaConfig sized = new AreaConfig();
                foreach (var field in typeof(AreaConfig).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    field.SetValue(sized, field.GetValue(effectiveAreaConfig));
                // Clone the hitbox too so the size multiplier doesn't mutate the shared config.
                sized.hitbox = effectiveAreaConfig.hitbox.Clone();
                sized.hitbox.scaleX = effectiveAreaConfig.hitbox.scaleX * (1f + abilitySizePercent);
                sized.hitbox.scaleY = effectiveAreaConfig.hitbox.scaleY * (1f + abilitySizePercent);
                effectiveAreaConfig = sized;
            }
        }

        GameObject auraGO = new GameObject($"Aura_{key}");
        auraGO.transform.SetParent(transform, worldPositionStays: false);
        auraGO.transform.localPosition = Vector3.zero;

        Aura aura = auraGO.AddComponent<Aura>();
        aura.SetContext(new SubAbilityContext { parentConfig = abilityDataConfig, owner = gameObject });
        aura.TraitConfig = traitEffectiveConfig;
        aura.Initialize(effectiveAreaConfig);

        _activeAuras[key] = aura;
        Debug.Log($"[PlayerAuraManager] Activated aura: {key}");
    }

    /// <summary>
    /// Builds an effective AreaConfig for aura behavior by applying trait modifier overrides.
    /// Falls back to the base areaConfig if no modifiers apply.
    /// </summary>
    private AreaConfig BuildEffectiveAuraConfig(AbilityDataConfig abilityDataConfig)
    {
        CharacterTraitManager traitManager = GetComponent<CharacterTraitManager>();
        if (traitManager == null) return abilityDataConfig.areaConfig;

        var traitModifierPairs = new List<AbilityModifierRuntime.TraitModifierPair>();
        foreach (TraitData data in traitManager.GetActiveTraits())
        {
            if (data?.abilityConfigModifiers == null) continue;
            foreach (var modifier in data.abilityConfigModifiers)
            {
                traitModifierPairs.Add(new AbilityModifierRuntime.TraitModifierPair(data, modifier));
            }
        }

        if (traitModifierPairs.Count == 0) return abilityDataConfig.areaConfig;

        var accumulatedOverrides = AbilityModifierRuntime.AccumulateOverrides(abilityDataConfig, traitModifierPairs);
        var effective = AbilityModifierRuntime.BuildEffectiveSubConfig(
            abilityDataConfig.areaConfig, "areaConfig", accumulatedOverrides);

        return effective ?? abilityDataConfig.areaConfig;
    }

    /// <summary>
    /// Destroys all active aura GameObjects and clears the registry.
    /// Called before a new loadout is applied.
    /// </summary>
    public void ClearAllAuras()
    {
        foreach (var pair in _activeAuras)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }
        _activeAuras.Clear();
    }

    /// <summary>
    /// Rebuilds all active auras with current trait modifiers applied.
    /// Called by CharacterTraitManager whenever traits change so auras pick up
    /// updated radius, damage, tick rate, etc.
    /// </summary>
    public void RebuildAuraModifiers()
    {
        if (_activeAuras.Count == 0) return;

        var toRebuild = new List<AbilityDataConfig>();
        foreach (var pair in _activeAuras)
        {
            if (pair.Value == null || pair.Value.ParentConfig == null) continue;

            AbilityDataConfig parentConfig = pair.Value.ParentConfig;
            AreaConfig newEffective = BuildEffectiveAuraConfig(parentConfig);

            // Skip rebuild if the config object hasn't changed (covers the common case
            // where a non-aura trait is taken and this aura is unaffected).
            if (ReferenceEquals(newEffective, pair.Value.TraitConfig)) continue;

            toRebuild.Add(parentConfig);
        }

        if (toRebuild.Count == 0) return;

        // Destroy only the auras that need rebuilding
        foreach (var config in toRebuild)
        {
            string key = config.abilityName;
            if (_activeAuras.TryGetValue(key, out Aura aura) && aura != null)
                Destroy(aura.gameObject);
            _activeAuras.Remove(key);
        }

        foreach (var config in toRebuild)
        {
            AddAura(config);
        }

        Debug.Log($"[PlayerAuraManager] Rebuilt {toRebuild.Count} auras with updated trait modifiers");
    }

    public void ClearAura(AbilityDataConfig abilityDataConfig)
    {
        if (abilityDataConfig == null || !abilityDataConfig.isAuraAbility)
            return;

        string key = abilityDataConfig.abilityName;

        if (_activeAuras.TryGetValue(key, out Aura aura))
        {
            if (aura != null)
                Destroy(aura.gameObject);
            _activeAuras.Remove(key);
            Debug.Log($"[PlayerAuraManager] Cleared aura: {key}");
        }
    }
}
