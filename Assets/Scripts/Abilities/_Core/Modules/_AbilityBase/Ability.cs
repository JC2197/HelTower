using UnityEngine;
using FishNet.Object;

/// <summary>
/// Base ability class - contains only UI/display data.
/// All mechanical logic (cooldowns, energy, charges) is handled by DataDrivenAbility.
/// All configuration data is in AbilityConfig/AbilityDataConfig.
/// Inherits from NetworkBehaviour for multiplayer support.
/// </summary>
public abstract class Ability : NetworkBehaviour
{
    // Reference to config for UI data
    protected AbilityDataConfig config;
    protected AbilityReference abilityReference;
    
    // UI Properties (read from config)
    public string AbilityName => config?.abilityName ?? "";
    public Sprite AbilityIcon => config?.abilityIcon;
    public string Description => config?.abilityDescription ?? "";
    public AbilityTagSelector Tags => config?.abilityTags;

    // Called by CharacterAbilityManager to set the config reference
    public void SetAbilityReference(AbilityReference reference)
    {
        abilityReference = reference;
        if (reference?.Config != null)
        {
            config = reference.Config as AbilityDataConfig;
        }
    }

    protected T GetConfig<T>() where T : AbilityConfig
    {
        return abilityReference?.Config as T;
    }

    /// <summary>
    /// Returns the AbilityDataConfig for this ability (or null if not set).
    /// Used by CharacterAbilityManager to compare offhand vs primary ability configs.
    /// </summary>
    public AbilityDataConfig GetAbilityConfig()
    {
        return config;
    }

    // Abstract method - implemented by DataDrivenAbility
    public abstract bool TryUseAbility();
}