using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Utility class for generating dynamic trait descriptions based on their effects and tier scaling.
/// This creates descriptions that reflect the actual values after tier multipliers are applied.
/// </summary>
public static class TraitDescriptionBuilder
{
    /// <summary>
    /// Build a complete dynamic description for a trait based on all its effects.
    /// Takes into account tier scaling for stat modifiers.
    /// </summary>
    public static string BuildDynamicDescription(TraitData trait)
    {
        if (trait == null) return "";
        
        // If the description has {v1}, {v2}, etc. placeholders, use the custom template
        if (!string.IsNullOrEmpty(trait.description) && trait.description.Contains("{v"))
        {
            return FormatCustomDescription(trait);
        }
        
        StringBuilder description = new StringBuilder();
        bool hasAnyEffects = false;
        
        // Determine ability context for ability upgrade traits
        string abilitySuffix = "";
        if (trait.requiredAbility != null)
        {
            abilitySuffix = $" for {trait.requiredAbility.abilityName}";
        }
        
        // ══════════════════════════════════════════════════════════════
        // STAT MODIFIERS (with tier scaling)
        // ══════════════════════════════════════════════════════════════
        if (trait.statModifiers != null && trait.statModifiers.Count > 0)
        {
            foreach (var modifier in trait.statModifiers)
            {
                if (hasAnyEffects)
                    description.AppendLine();
                
                // Get stat display name from database
                string statDisplayName = GetStatDisplayName(modifier.statID);
                
                // Format the description based on modifier type
                string formattedDescription = FormatStatModifier(statDisplayName, modifier.value, modifier.modifierType);
                description.Append(formattedDescription);
                
                // Add ability context for ability-specific traits
                if (!string.IsNullOrEmpty(abilitySuffix))
                    description.Append(abilitySuffix);
                
                hasAnyEffects = true;
            }
        }
        
        // ══════════════════════════════════════════════════════════════
        // ABILITY REPLACEMENT
        // ══════════════════════════════════════════════════════════════
        if (trait.abilityReplacement != null && trait.abilityReplacement.newAbilityConfig != null)
        {
            if (hasAnyEffects)
                description.AppendLine();
            
            if (!string.IsNullOrEmpty(trait.abilityReplacement.description))
            {
                description.Append(trait.abilityReplacement.description);
            }
            else
            {
                string requiredName = trait.abilityReplacement.requiredAbility != null 
                    ? trait.abilityReplacement.requiredAbility.abilityName 
                    : "ability";
                description.Append($"Replaces {requiredName} with {trait.abilityReplacement.newAbilityConfig.abilityName}");
            }
            
            hasAnyEffects = true;
        }
        
        // ══════════════════════════════════════════════════════════════
        // ABILITY UNLOCKS
        // ══════════════════════════════════════════════════════════════
        if (trait.unlockedAbilities != null && trait.unlockedAbilities.Count > 0)
        {
            foreach (var unlock in trait.unlockedAbilities)
            {
                if (unlock.abilityConfig == null) continue;
                
                if (hasAnyEffects)
                    description.AppendLine();
                
                // Just show ability name - slot is auto-determined by ability type
                description.Append($"Unlocks {unlock.abilityConfig.abilityName}");
                
                hasAnyEffects = true;
            }
        }
        
        // ══════════════════════════════════════════════════════════════
        // WEAPON AMMO MODIFIERS (for weapon traits)
        // ══════════════════════════════════════════════════════════════
        if (trait.weaponAmmoModifier != null)
        {
            var ammo = trait.weaponAmmoModifier;
            string weaponType = !string.IsNullOrEmpty(trait.weaponTraitTag) ? trait.weaponTraitTag : "weapons";
            
            if (ammo.magazineSizeBonus != 0)
            {
                if (hasAnyEffects)
                    description.AppendLine();
                
                description.Append($"{(ammo.magazineSizeBonus >= 0 ? "+" : "")}{ammo.magazineSizeBonus} ammo for {weaponType}");
                hasAnyEffects = true;
            }
            
            if (ammo.reloadTimeDelta != 0)
            {
                if (hasAnyEffects)
                    description.AppendLine();
                
                string sign = ammo.reloadTimeDelta >= 0 ? "+" : "";
                description.Append($"{sign}{ammo.reloadTimeDelta:0.##}s reload for {weaponType}");
                
                hasAnyEffects = true;
            }
        }
        
        // ══════════════════════════════════════════════════════════════
        // ABILITY CONFIG MODIFIERS (Property Path System)
        // ══════════════════════════════════════════════════════════════
        if (trait.abilityConfigModifiers != null && trait.abilityConfigModifiers.Count > 0)
        {
            foreach (var modifier in trait.abilityConfigModifiers)
            {
                if (modifier.targetAbility == null || modifier.isEmpty) continue;
                string abilityName = modifier.targetAbility.abilityName;

                AbilityDataConfig addedTriggeredAbility = modifier.addTriggeredAbilityConfig != null
                    ? modifier.addTriggeredAbilityConfig.abilityConfig
                    : null;
                if (addedTriggeredAbility == null)
                    addedTriggeredAbility = modifier.addTriggeredAbilityLegacy;

                if (addedTriggeredAbility != null)
                {
                    if (hasAnyEffects) description.AppendLine();

                    float triggerChance = modifier.addTriggeredAbilityConfig != null
                        ? Mathf.Clamp01(modifier.addTriggeredAbilityConfig.triggerChance)
                        : 1f;
                    TriggeredAbilityTriggerTiming triggerTiming = modifier.addTriggeredAbilityConfig != null
                        ? modifier.addTriggeredAbilityConfig.triggerTiming
                        : TriggeredAbilityTriggerTiming.OnHit;
                    string triggerInfo = $"{triggerChance * 100f:0.##}% ({triggerTiming})";

                    if (!string.IsNullOrEmpty(modifier.addTriggeredAbilityPath))
                    {
                        string sourceName = modifier.addTriggeredAbilityPath.Replace("Config", "").Replace("config", "");
                        description.Append($"Adds triggered ability {addedTriggeredAbility.abilityName} [{triggerInfo}] to {abilityName} ({sourceName})");
                    }
                    else
                    {
                        description.Append($"Adds triggered ability {addedTriggeredAbility.abilityName} [{triggerInfo}] to {abilityName}");
                    }
                    hasAnyEffects = true;
                }

                foreach (var ov in modifier.overrides)
                {
                    if (ov.isEmpty) continue;
                    if (hasAnyEffects) description.AppendLine();

                    string propName = ov.GetDisplayName();
                    
                    // Apply tier scaling to numeric values
                    float displayValue = ov.numericValue;
                    
                    if (ov.overrideMode == OverrideMode.Set)
                    {
                        if (!string.IsNullOrEmpty(ov.stringValue))
                            description.Append($"Changes {abilityName} to {ov.stringValue} {propName.ToLower()}");
                        else if (ov.objectValue != null)
                            description.Append($"Changes {propName.ToLower()} for {abilityName}");
                        else if (TryGetEnumDisplayName(ov.propertyPath, ov.numericValue, out string enumDisplay))
                            description.Append($"Sets {propName.ToLower()} to {enumDisplay} for {abilityName}");
                        else
                            description.Append($"Sets {propName.ToLower()} to {displayValue:0.##} for {abilityName}");
                    }
                    else
                    {
                        string sign = displayValue >= 0 ? "+" : "";
                        string suffix = ov.overrideMode == OverrideMode.Percent ? "%" : "";
                        description.Append($"{sign}{displayValue:0.##}{suffix} {propName.ToLower()} for {abilityName}");
                    }
                    
                    hasAnyEffects = true;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // FALLBACK: Use manual description if no effects found
        // ══════════════════════════════════════════════════════════════
        if (!hasAnyEffects && !string.IsNullOrEmpty(trait.description))
        {
            description.Append(trait.description);
        }
        
        return description.ToString();
    }
    
    // ──────────────────────────────────────────────────────────────────
    // CUSTOM DESCRIPTION WITH VALUE PLACEHOLDERS
    // ──────────────────────────────────────────────────────────────────
    
    /// <summary>
    /// Format a trait's description by replacing {v1}, {v2}, etc. with actual modifier values.
    /// Values are collected in order of appearance across all modifier sources:
    ///   1. Stat Modifiers (each stat modifier = one value)
    ///   2. Weapon Ammo Modifiers (magazineSizeBonus, then reloadTimeDelta if non-zero)
    ///   3. Ability Config Modifiers (each override = one value, iterated per modifier then per override)
    /// Numeric values have tier scaling applied. String/set overrides resolve to their string value.
    /// </summary>
    private static string FormatCustomDescription(TraitData trait)
    {
        List<string> values = CollectModifierValues(trait);
        
        // Replace {v1}, {v2}, ... with formatted values
        string result = Regex.Replace(trait.description, @"\{v(\d+)\}", match =>
        {
            int index = int.Parse(match.Groups[1].Value) - 1; // {v1} = index 0
            if (index >= 0 && index < values.Count)
            {
                return values[index];
            }
            return match.Value; // Leave unresolved placeholders as-is
        });
        
        return result;
    }
    
    /// <summary>
    /// Collect all modifier values from a trait in a stable order.
    /// The index in this list corresponds to {v1}, {v2}, etc. (1-based in the template).
    /// </summary>
    private static List<string> CollectModifierValues(TraitData trait)
    {
        List<string> values = new List<string>();
        
        // 1. Stat Modifiers
        if (trait.statModifiers != null)
        {
            foreach (var mod in trait.statModifiers)
            {
                float value = mod.value;
                if (string.Equals(mod.statID, "AttackSpeed", System.StringComparison.OrdinalIgnoreCase))
                    value *= 100f;
                values.Add(FormatPlaceholderValue(value));
            }
        }
        
        // 2. Weapon Ammo Modifiers
        if (trait.weaponAmmoModifier != null)
        {
            if (trait.weaponAmmoModifier.magazineSizeBonus != 0)
                values.Add(FormatPlaceholderValue(trait.weaponAmmoModifier.magazineSizeBonus));
            if (trait.weaponAmmoModifier.reloadTimeDelta != 0)
                values.Add(FormatPlaceholderValue(trait.weaponAmmoModifier.reloadTimeDelta));
        }
        
        // 3. Ability Config Modifiers (in order of appearance)
        if (trait.abilityConfigModifiers != null)
        {
            foreach (var modifier in trait.abilityConfigModifiers)
            {
                AbilityDataConfig addedTriggeredAbility = modifier.addTriggeredAbilityConfig != null
                    ? modifier.addTriggeredAbilityConfig.abilityConfig
                    : null;
                if (addedTriggeredAbility == null)
                    addedTriggeredAbility = modifier.addTriggeredAbilityLegacy;

                if (addedTriggeredAbility != null)
                    values.Add(addedTriggeredAbility.abilityName);

                if (modifier.overrides == null) continue;
                foreach (var ov in modifier.overrides)
                {
                    values.Add(GetAbilityOverridePlaceholderValue(trait, ov));
                }
            }
        }
        
        return values;
    }

    private static string GetAbilityOverridePlaceholderValue(TraitData trait, AbilityPropertyOverride ov)
    {
        if (ov == null)
            return "";

        if (ov.overrideMode == OverrideMode.Set)
        {
            if (!string.IsNullOrEmpty(ov.stringValue))
                return ov.stringValue;

            if (ov.objectValue != null)
                return ov.objectValue.name;

            if (TryGetEnumDisplayName(ov.propertyPath, ov.numericValue, out string enumDisplay))
                return enumDisplay;
        }

        float value = ov.numericValue;

        // Fields stored as 0-1 fractions (e.g. baseCritChance = 0.05 means 5%) need ×100 for display.
        if (IsPercentagePropertyPath(ov.propertyPath))
            value *= 100f;

        return FormatPlaceholderValue(value);
    }

    // Returns true for property paths whose leaf field stores a 0-1 fraction displayed as a percentage.
    private static bool IsPercentagePropertyPath(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return false;

        // Get the leaf field name (last segment after any dots, e.g. "projectileConfig.baseCritChance" → "baseCritChance")
        int dotIndex = propertyPath.LastIndexOf('.');
        string leafName = dotIndex >= 0 ? propertyPath.Substring(dotIndex + 1) : propertyPath;

        return leafName.EndsWith("Chance", System.StringComparison.OrdinalIgnoreCase)
            || (propertyPath.IndexOf("statContainer.", System.StringComparison.OrdinalIgnoreCase) >= 0
                && leafName.Equals("AttackSpeed", System.StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetEnumDisplayName(string propertyPath, float numericValue, out string displayName)
    {
        displayName = null;
        System.Type fieldType = GetAbilityPropertyType(propertyPath);
        if (fieldType == null || !fieldType.IsEnum)
            return false;

        System.Array values = System.Enum.GetValues(fieldType);
        if (values.Length == 0)
            return false;

        int enumIndex = Mathf.Clamp(Mathf.RoundToInt(numericValue), 0, values.Length - 1);
        object enumValue = values.GetValue(enumIndex);
        displayName = enumValue != null ? enumValue.ToString() : null;
        return !string.IsNullOrEmpty(displayName);
    }

    private static System.Type GetAbilityPropertyType(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return null;

        string[] parts = propertyPath.Split('.');
        System.Type currentType = typeof(AbilityDataConfig);

        for (int i = 0; i < parts.Length; i++)
        {
            var field = currentType.GetField(parts[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field == null)
                return null;
            currentType = field.FieldType;
        }

        return currentType;
    }

    private static string FormatPlaceholderValue(float value)
    {
        return value % 1 == 0 ? value.ToString("0") : value.ToString("0.##");
    }

    // ──────────────────────────────────────────────────────────────────
    // FORMATTING HELPERS
    // ──────────────────────────────────────────────────────────────────
    
    /// <summary>
    /// Format a stat modifier with proper display name and value formatting.
    /// </summary>
    private static string FormatStatModifier(string statDisplayName, float value, TraitModifierType modifierType)
    {
        // Check if this is a percentage-based stat (ends with "%" in database)
        StatTypeDatabase database = StatTypeDatabase.Instance;
        bool isPercentageStat = false;
        
        if (database != null)
        {
            foreach (var statType in database.StatTypes)
            {
                if (statType.DisplayName == statDisplayName)
                {
                    // Check if it's a percentage-type stat (like Crit Chance, Attack Speed, etc.)
                    isPercentageStat = statType.IsPercentage;
                    break;
                }
            }
        }
        
        string sign = value >= 0 ? "+" : "";
        
        if (modifierType == TraitModifierType.Flat)
        {
            if (isPercentageStat)
            {
                // For percentage stats, flat adds percentage points
                return $"{sign}{value:0.##}% {statDisplayName}";
            }
            else
            {
                // For absolute stats, flat adds absolute value
                return $"{sign}{value:0.##} {statDisplayName}";
            }
        }
        else // Percentage
        {
            // Percentage modifier is multiplicative - always shown as %
            return $"{sign}{value:0.##}% {statDisplayName}";
        }
    }
    
    /// <summary>
    /// Format a status effect modifier description.
    /// </summary>
    private static string FormatStatusEffectModifier(AbilityStatusEffectModifier modifier)
    {
        List<string> effects = new List<string>();
        
        if (modifier.addBleed)
        {
            string chanceStr = modifier.bleedChance < 1f ? $" ({modifier.bleedChance * 100:0}% chance)" : "";
            effects.Add($"Bleed ({modifier.bleedDamage:0.##} damage over {modifier.bleedDuration:0.##}s){chanceStr}");
        }
        
        if (modifier.addBurn)
        {
            string chanceStr = modifier.burnChance < 1f ? $" ({modifier.burnChance * 100:0}% chance)" : "";
            effects.Add($"Burn ({modifier.burnDamage:0.##} damage over {modifier.burnDuration:0.##}s){chanceStr}");
        }
        
        if (modifier.addPoison)
        {
            string chanceStr = modifier.poisonChance < 1f ? $" ({modifier.poisonChance * 100:0}% chance)" : "";
            effects.Add($"Poison ({modifier.poisonDamage:0.##} damage over {modifier.poisonDuration:0.##}s){chanceStr}");
        }
        
        if (modifier.addRoot)
        {
            string chanceStr = modifier.rootChance < 1f ? $" ({modifier.rootChance * 100:0}% chance)" : "";
            effects.Add($"Root ({modifier.rootDuration:0.##}s){chanceStr}");
        }
        
        if (modifier.addSlow)
        {
            string chanceStr = modifier.slowChance < 1f ? $" ({modifier.slowChance * 100:0}% chance)" : "";
            effects.Add($"Slow ({modifier.slowDuration:0.##}s){chanceStr}");
        }
        
        if (modifier.addStun)
        {
            string chanceStr = modifier.stunChance < 1f ? $" ({modifier.stunChance * 100:0}% chance)" : "";
            effects.Add($"Stun ({modifier.stunDuration:0.##}s){chanceStr}");
        }
        
        if (effects.Count == 0)
            return "";
        
        string abilityName = string.IsNullOrEmpty(modifier.abilityName) ? "Attacks" : modifier.abilityName;
        return $"{abilityName} inflict: {string.Join(", ", effects)}";
    }
    
    /// <summary>
    /// Get stat display name from database, fallback to statID if not found.
    /// </summary>
    private static string GetStatDisplayName(string statID)
    {
        StatTypeDatabase database = StatTypeDatabase.Instance;
        if (database != null && database.TryGetStatType(statID, out StatTypeData statType))
        {
            if (!string.IsNullOrEmpty(statType.DisplayName))
            {
                return statType.DisplayName;
            }
        }
        
        // Fallback: use statID with some formatting
        return FormatStatID(statID);
    }
    
    /// <summary>
    /// Format a statID into a readable display name by adding spaces before capitals.
    /// Example: "MaxHealth" → "Max Health"
    /// </summary>
    private static string FormatStatID(string statID)
    {
        if (string.IsNullOrEmpty(statID))
            return statID;
        
        StringBuilder result = new StringBuilder();
        result.Append(statID[0]);
        
        for (int i = 1; i < statID.Length; i++)
        {
            if (char.IsUpper(statID[i]) && !char.IsUpper(statID[i - 1]))
            {
                result.Append(' ');
            }
            result.Append(statID[i]);
        }
        
        return result.ToString();
    }
}
