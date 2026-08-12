using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Shared initialization context for all sub-abilities spawned by DataDrivenAbility.
/// Bundles the common parameters every sub-ability needs, replacing the pattern of
/// passing parentConfig, owner, abilityName, and abilityTags
/// as separate parameters.
/// </summary>
public struct SubAbilityContext
{
    public AbilityDataConfig rawParentConfig;
    public AbilityDataConfig parentConfig;
    public GameObject owner;
    public GameObject statOwner;

    public string AbilityName => parentConfig?.abilityName;
    public List<string> AbilityTags => parentConfig?.abilityTags?.GetAllTags();
}

/// <summary>
/// Interface for sub-abilities that receive a common context from DataDrivenAbility.
/// Implement SetContext to store parentConfig and owner.
/// Type-specific initialization (sub-config, direction, etc.) remains on each sub-ability's
/// own methods since effective configs may differ from parentConfig's raw sub-configs.
/// </summary>
public interface ISubAbility
{
    void SetContext(SubAbilityContext context);
}
