using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine.Serialization;

/// <summary>
/// ScriptableObject definition for a trait/upgrade.
/// Traits can modify stats, grant new abilities, or provide special effects.
/// </summary>
[CreateAssetMenu(fileName = "New Trait", menuName = "Traits/New Trait")]
public class TraitData : ScriptableObject
{
    [Header("Trait Identity")]
    [Tooltip("Unique identifier for this trait")]
    public string traitID;

    [Tooltip("Display name shown to players")]
    public string displayName;

    [TextArea(3, 5)]
    [Tooltip("Description of what this trait does")]
    public string description;

    [Header("Visual")]
    [Tooltip("Icon displayed for this trait")]
    public Sprite traitIcon;

    [Header("Weapon Tag")]
    [Tooltip("Weapon type tag (Pistol, Rifle, Sword, etc.) used for weapon-specific bonuses")]
    [WeaponTypeDropdown]
    public string weaponTraitTag = "";

    /// <summary>
    /// Get all non-empty tags for display or synergy calculation.
    /// </summary>
    public List<string> GetAllTags()
    {
        var tags = new List<string>();
        if (!string.IsNullOrEmpty(weaponTraitTag)) tags.Add(weaponTraitTag);
        return tags;
    }

    /// <summary>
    /// Get only specialized tags for synergy weighting.
    /// </summary>
    public List<string> GetSpecializedTags()
    {
        return new List<string>();
    }

    /// <summary>
    /// Get only weapon tags for weapon-specific bonuses.
    /// </summary>
    public List<string> GetWeaponTags()
    {
        var tags = new List<string>();
        if (!string.IsNullOrEmpty(weaponTraitTag)) tags.Add(weaponTraitTag);
        return tags;
    }

    [Header("Trait Type")]
    [Tooltip("Is this a minor trait or a major/keystone trait?")]
    public TraitType traitType = TraitType.General;

    [Header("Stat Modifiers")]
    [Tooltip("Direct stat modifications this trait provides")]
    public List<TraitStatModifier> statModifiers = new List<TraitStatModifier>();

    [Header("Special Effects")]
    [Tooltip("Custom effect script for complex trait behaviors")]
    public TraitEffect effectScript;

    [Header("Ability Requirement (for AbilityUpgrade traits)")]
    [Tooltip("The ability that must be owned for this trait to appear. Used for ability-specific upgrades.")]
    public AbilityConfig requiredAbility;

    [Tooltip("Minimum level the required ability must be at for this trait to appear. 0 = no level requirement (just need to own it). 5 = ability must be max level for replacement upgrades.")]
    public int requiredAbilityLevel = 0;

    [Header("Trait Requirement")]
    [Tooltip("This trait will only appear in rolls if the player already has at least one of the listed prerequisite traits.")]
    public List<TraitData> requiredTraits = new List<TraitData>();

    [Header("Ability Replacement")]
    [Tooltip("Replace an existing ability with a new one (typically used with requiredAbilityLevel = 5)")]
    public AbilityReplacement abilityReplacement;

    [Header("Ability Unlock")]
    [Tooltip("Unlock new abilities in specific slots when this trait is acquired")]
    public List<TraitAbilityUnlock> unlockedAbilities = new List<TraitAbilityUnlock>();

    [Header("Ability Ammo Modifier")]
    [Tooltip("Modifies the ability's magazine size and reload time when this trait is active. Useful for AbilityUpgrade traits targeting weapon abilities.")]
    public AbilityAmmoModifier weaponAmmoModifier = new AbilityAmmoModifier();

    [Header("Ability Config Modifiers")]
    [Tooltip("Directly modify mechanical fields (cooldown, attack speed, charges, energy cost) on a specific AbilityDataConfig when this trait is active. Reference the ability asset to avoid string-matching errors.")]
    public List<AbilityConfigModifier> abilityConfigModifiers = new List<AbilityConfigModifier>();

    [Header("Mutual Exclusion")]
    [Tooltip("This trait will not roll if any trait in this list has already been taken.")]
    public List<TraitData> mutuallyExclusiveWith = new List<TraitData>();

    public bool IsAbilityTraitType => traitType == TraitType.Ability || traitType == TraitType.AbilityUpgrade;

    public bool IsUpgradeTraitType => traitType == TraitType.AbilityUpgrade;

    public bool IsUniqueTraitType => traitType == TraitType.Ability || traitType == TraitType.AbilityUpgrade || traitType == TraitType.Keystone;

}

/// <summary>
/// Modifier that changes an ability's ammo capacity and reload speed.
/// Authored on AbilityUpgrade TraitData assets and applied at runtime
/// when ability upgrade traits are unlocked.
/// </summary>
[System.Serializable]
public class AbilityAmmoModifier
{
    [Tooltip("Flat rounds added to magazine size (negative to reduce).")]
    public int magazineSizeBonus = 0;
    [Tooltip("Flat seconds added to reload time (negative = faster reload).")]
    public float reloadTimeDelta = 0f;
    public bool IsEmpty => magazineSizeBonus == 0 && reloadTimeDelta == 0f;
}

/// <summary>
/// Types of traits in the system.
/// IMPORTANT: Explicit values preserve backward compatibility with serialized assets.
/// Do NOT change these values or reorder entries.
/// </summary>
public enum TraitType
{
    General = 0,
    // Values 1-2 reserved for removed Weapon/WeaponUpgrade types - do not reuse
    Ability = 3,
    AbilityUpgrade = 4,
    Keystone = 5
}

/// <summary>
/// Defines a stat modification from a trait
/// </summary>
[System.Serializable]
public class TraitStatModifier
{
    [Tooltip("Stat ID from StatTypeDatabase (e.g., 'AttackSpeed', 'MaxHealth')")]
    public string statID;

    public TraitModifierType modifierType;
    public float value;

    [Tooltip("Optional custom icon (if null, uses icon from StatTypeDatabase)")]
    public Sprite customIcon;

    [Tooltip("Description for tooltip display")]
    public string description;
}

/// <summary>
/// How the modifier is applied to stats
/// 
/// For ABSOLUTE stats (Health, Armor, Damage, etc.):
///   - Flat: Adds absolute amount (e.g., value=10 adds +10 health)
///   - Percentage: Multiplicative increase (e.g., value=15 means +15% = 1.15x multiplier)
///   Formula: (base + flat) * (1 + percent/100)
/// 
/// For PERCENTAGE stats (AttackSpeed, CritChance, CooldownReduction, etc.):
///   - Flat: Adds percentage points (e.g., value=15 adds +15% = +0.15)
///   - Percentage: Multiplicative increase (e.g., value=20 means +20% more = 1.20x multiplier)
///   Formula: (base + flat/100) * (1 + percent/100)
/// </summary>
public enum TraitModifierType
{
    Flat,           // Absolute value OR percentage points (value >= 1, e.g., value=15 adds +15 to stat)
    Percentage      // Multiplicative percentage increase (value >= 1, e.g., value=15 means +15% = 1.15x)
}

/// <summary>
/// Defines replacing an ability with another
/// </summary>
[System.Serializable]
public class AbilityReplacement
{
    [Tooltip("The ability that must be owned for this upgrade to appear. The upgrade will replace this ability.")]
    public AbilityConfig requiredAbility;

    [Tooltip("New ability configuration that replaces the required ability")]
    public AbilityConfig newAbilityConfig;

    [Tooltip("Description of the change (auto-generated if empty)")]
    public string description;

    /// <summary>
    /// Helper to get the required ability's name for matching.
    /// </summary>
    public string RequiredAbilityName => requiredAbility != null ? requiredAbility.abilityName : null;
}

/// <summary>
/// [DEPRECATED] Which ability slot/list a trait-unlocked ability should be placed into.
/// Abilities are now added to the next available trait slot.
/// This enum is kept for backward compatibility but should not be used for new code.
/// </summary>
public enum AbilityUISlotType
{
    Weapon = 0,  // LMB - WeaponAbility InputAction
    SecondaryWeapon = 1,  // RMB - secondary weapon ability
    Trait = 2,  // Shift, Q, E, R

    // Legacy aliases for backward compatibility
    [System.Obsolete("Use Weapon instead")] Primary = 0,
    [System.Obsolete("Use Trait instead")] Secondary = 2,
    [System.Obsolete("Use SecondaryWeapon instead")] Tertiary = 1,
    [System.Obsolete("Use Trait instead")] Ultimate = 2,
    [System.Obsolete("Use Trait instead")] Passive = 2,
    [System.Obsolete("Use SecondaryWeapon instead")] Offhand = 1
}

/// <summary>
/// Pairs an AbilityConfig with a trait for ability unlocks.
/// The ability is assigned to the next available trait slot.
/// </summary>
[System.Serializable]
public class TraitAbilityUnlock
{
    [Tooltip("The ability to grant")]
    public AbilityConfig abilityConfig;

    [System.Obsolete("targetSlot is no longer used. Abilities use the next available trait slot.")]
    [Tooltip("[DEPRECATED] Abilities use the next available trait slot. This field is ignored.")]
    public AbilityUISlotType targetSlot = AbilityUISlotType.Trait;
}

/// <summary>
/// Adds status effects to an ability
/// </summary>
[System.Serializable]
public class AbilityStatusEffectModifier
{
    [Tooltip("Name of the ability to modify (e.g., 'Primary Attack', 'Secondary Ability')")]
    public string abilityName;

    [Header("Bleed Effect")]
    [Tooltip("Add bleed effect to this ability?")]
    public bool addBleed = false;
    public BleedEffect bleedEffect;
    public float bleedDamage = 5f;
    public float bleedDuration = 3f;
    [Range(0f, 1f)]
    public float bleedChance = 1f;

    [Header("Burn Effect")]
    [Tooltip("Add burn effect to this ability?")]
    public bool addBurn = false;
    public BurningEffect burnEffect;
    public float burnDamage = 10f;
    public float burnDuration = 3f;
    [Range(0f, 1f)]
    public float burnChance = 1f;

    [Header("Poison Effect")]
    [Tooltip("Add poison effect to this ability?")]
    public bool addPoison = false;
    public PoisonEffect poisonEffect;
    public float poisonDamage = 3f;
    public float poisonDuration = 5f;
    [Range(0f, 1f)]
    public float poisonChance = 1f;

    [Header("Root Effect")]
    [Tooltip("Add root effect to this ability?")]
    public bool addRoot = false;
    public RootEffect rootEffect;
    public float rootDuration = 2f;
    [Range(0f, 1f)]
    public float rootChance = 1f;

    [Header("Slow Effect")]
    [Tooltip("Add slow effect to this ability?")]
    public bool addSlow = false;
    public EffectConfig slowEffect;
    public float slowDuration = 2f;
    [Range(0f, 1f)]
    public float slowChance = 1f;

    [Header("Stun Effect")]
    [Tooltip("Add stun effect to this ability?")]
    public bool addStun = false;
    public EffectConfig stunEffect;
    public float stunDuration = 1f;
    [Range(0f, 1f)]
    public float stunChance = 1f;

    [Tooltip("Description for tooltip display")]
    public string description;
}

public enum OverrideMode
{
    //Flat Increase/Decrease
    Flat,
    //Percentage Increase/Decrease
    Percent,
    //Override base value (ignores original stat and sets to this value)
    Set
}

[System.Serializable]
public class AbilityPropertyOverride
{
    public string propertyPath = "";
    public OverrideMode overrideMode = OverrideMode.Flat;
    public float numericValue = 0f;
    public string stringValue = "";
    public UnityEngine.Object objectValue;

    public bool isEmpty
    {
        get
        {
            if (string.IsNullOrEmpty(propertyPath))
                return true;
            if (overrideMode == OverrideMode.Set)
            {
                // Set overrides may legitimately use 0 / first-enum-index / false, so once
                // a property path is selected we treat the override as intentionally authored.
                return false;
            }
            else
            {
                return numericValue == 0f;
            }
        }
    }


    /// <summary>
    /// Get a formatted display name from the property path.
    /// E.g., "projectileConfig.damage" → "Projectile Damage"
    /// </summary>
    public string GetDisplayName()
    {
        if (string.IsNullOrEmpty(propertyPath))
            return "(none)";
        if (propertyPath.Contains("."))
        {
            string[] parts = propertyPath.Split('.');
            string prefix = parts[0].Replace("Config", "");
            prefix = char.ToUpper(prefix[0]) + prefix.Substring(1);
            string field = FormatFieldName(parts[parts.Length - 1]);
            return $"{prefix}: {field}";
        }
        return FormatFieldName(propertyPath);
    }
    private static string FormatFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return fieldName;
        var sb = new System.Text.StringBuilder();
        sb.Append(char.ToUpper(fieldName[0]));
        for (int i = 1; i < fieldName.Length; i++)
        {
            if (char.IsUpper(fieldName[i]) && !char.IsUpper(fieldName[i - 1]))
                sb.Append(' ');
            sb.Append(fieldName[i]);
        }
        return sb.ToString();
    }
}

/// <summary>
/// Modifies a specific AbilityDataConfig at runtime using property path overrides.
/// 
/// USAGE:
/// 1. Assign targetAbility to the AbilityDataConfig you want to modify
/// 2. Add overrides to the list, each specifying:
///    - propertyPath: The field to modify (e.g., "cooldownTime", "projectileConfig.damage")
///    - mode: How to apply (Flat add, Percent multiply, Set replace)
///    - value: The modification value
/// 
/// EXAMPLES:
/// - Reduce cooldown by 0.5s: path="cooldownTime", mode=Flat, numericValue=-0.5
/// - Increase damage by 25%: path="projectileConfig.damage", mode=Percent, numericValue=25
/// - Change damage type: path="projectileConfig.damageTypeName", mode=Set, stringValue="Fire"
/// - Swap projectile: path="projectileConfig.projectilePrefab", mode=Set, objectValue=(prefab)
/// </summary>
[System.Serializable]
public class AbilityConfigModifier
{
    public AbilityDataConfig targetAbility;
    public Sprite abilityIcon;
    [Tooltip("Appends this triggered-ability config to applicable on-hit triggered-ability arrays on the target ability at runtime.")]
    public EffectData.TriggeredAbilityConfig addTriggeredAbilityConfig = new EffectData.TriggeredAbilityConfig();
    [FormerlySerializedAs("addTriggeredAbility")]
    [HideInInspector]
    public AbilityDataConfig addTriggeredAbilityLegacy;
    [Tooltip("Optional target source path for Add Triggered Ability (for example summon/construct sub-abilities). Leave empty to apply to all valid trigger sources.")]
    public string addTriggeredAbilityPath = "";
    public List<AbilityPropertyOverride> overrides = new List<AbilityPropertyOverride>();

    public bool isEmpty
    {
        get
        {
            if (targetAbility == null) return true;
            bool hasTriggeredAddition = (addTriggeredAbilityConfig != null && addTriggeredAbilityConfig.abilityConfig != null)
                || addTriggeredAbilityLegacy != null;
            if (hasTriggeredAddition) return false;
            if (overrides == null || overrides.Count == 0) return true;
            foreach (var o in overrides)
                if (!o.isEmpty) return false;
            return true;
        }
    }
}
