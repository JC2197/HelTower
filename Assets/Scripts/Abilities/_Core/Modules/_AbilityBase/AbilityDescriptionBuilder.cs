using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Resolves {fieldName} placeholders in ability descriptions
/// by looking up public fields on the AbilityDataConfig and its sub-configs.
/// When a DataDrivenAbility is provided, uses trait-modified effective values.
/// Example: "Deals {damage} damage every {tickRate}s" 
/// </summary>
public static class AbilityDescriptionBuilder
{
    private static readonly Regex PlaceholderRegex = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Replace all {fieldName} tokens using effective (trait-modified) values from the live ability.
    /// Falls back to base config values for fields without overrides.
    /// </summary>
    public static string FormatDescription(string description, AbilityDataConfig config, DataDrivenAbility ability)
    {
        if (string.IsNullOrEmpty(description) || config == null)
            return description ?? "";

        var overrides = ability != null ? ability.AccumulatedOverrides : null;

        return PlaceholderRegex.Replace(description, match =>
        {
            string fieldName = match.Groups[1].Value;

            // Try top-level fields (with overrides applied)
            if (TryGetEffectiveValue(config, fieldName, fieldName, overrides, out string result))
                return result;

            // Try sub-configs — only search configs that are active for this ability type
            // to prevent default-initialized structs (e.g. projectileConfig.damageTypeName="Physical")
            // from shadowing the correct config's field on unrelated ability types.
            if (config.isProjectileAbility && TryGetSubConfigValue(
                    ability?.EffectiveProjectileConfig, config.projectileConfig,
                    "projectileConfig", fieldName, overrides, out result)) return result;
            if ((config.isAreaAbility || config.areaFollowsProjectile) && TryGetSubConfigValue(
                    ability?.EffectiveAreaConfig, config.areaConfig,
                    "areaConfig", fieldName, overrides, out result)) return result;
            if (config.isMeleeAbility && TryGetSubConfigValue(
                    ability?.EffectiveMeleeConfig, config.meleeConfig,
                    "meleeConfig", fieldName, overrides, out result)) return result;
            if (config.isExplosionAbility && TryGetSubConfigValue(
                    ability?.EffectiveExplosionConfig, config.explosionConfig,
                    "explosionConfig", fieldName, overrides, out result)) return result;
            if (config.isSummonAbility && TryGetSubConfigValue(
                    ability?.EffectiveSummonConfig, config.summonConfig,
                    "summonConfig", fieldName, overrides, out result)) return result;

            // Sub-configs without effective copies — gated by ability type flag
            if (config.isBeamAbility && TryGetEffectiveValue(config.beamConfig, fieldName, $"beamConfig.{fieldName}", overrides, out result)) return result;
            if (config.isConstructAbility && TryGetEffectiveValue(config.constructConfig, fieldName, $"constructConfig.{fieldName}", overrides, out result)) return result;
            if (config.isTrapAbility && TryGetEffectiveValue(config.trapConfig, fieldName, $"trapConfig.{fieldName}", overrides, out result)) return result;
            if (config.isChanneled && TryGetEffectiveValue(config.channelConfig, fieldName, $"channelConfig.{fieldName}", overrides, out result)) return result;
            if (config.isProjectileAbility && TryGetEffectiveValue(config.weaponData, fieldName, $"weaponData.{fieldName}", overrides, out result)) return result;
            if (config.isMovementAbility && TryGetEffectiveValue(config.movementConfig, fieldName, $"movementConfig.{fieldName}", overrides, out result)) return result;

            // Unresolved — leave as-is
            return match.Value;
        });
    }

    /// <summary>
    /// Replace all {fieldName} tokens in the description with base config values (no trait modifiers).
    /// </summary>
    public static string FormatDescription(string description, AbilityDataConfig config)
    {
        return FormatDescription(description, config, null);
    }

    /// <summary>
    /// For sub-configs that have an effective copy, read directly from the effective copy.
    /// Falls back to the base sub-config if no effective copy exists.
    /// </summary>
    private static bool TryGetSubConfigValue(
        object effectiveSubConfig, object baseSubConfig,
        string subConfigPrefix, string fieldName,
        Dictionary<string, AbilityModifierRuntime.AccumulatedValue> overrides,
        out string result)
    {
        result = null;
        // If we have an effective copy, just read the already-modified value directly
        object source = effectiveSubConfig ?? baseSubConfig;
        if (source == null) return false;

        if (effectiveSubConfig != null)
            return TryGetFieldValue(effectiveSubConfig, fieldName, out result);

        // No effective copy — apply overrides manually
        return TryGetEffectiveValue(baseSubConfig, fieldName, $"{subConfigPrefix}.{fieldName}", overrides, out result);
    }

    /// <summary>
    /// Get a field value with accumulated overrides applied.
    /// </summary>
    private static bool TryGetEffectiveValue(
        object obj, string fieldName, string propertyPath,
        Dictionary<string, AbilityModifierRuntime.AccumulatedValue> overrides,
        out string result)
    {
        result = null;
        if (obj == null) return false;

        FieldInfo field = obj.GetType().GetField(fieldName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field == null) return false;

        object value = field.GetValue(obj);
        if (value == null) return false;

        // Rebuild path using the actual C# field name so it matches the override dict keys,
        // which are authored from serialized field names (always camelCase).
        string canonicalPath = propertyPath.Length == fieldName.Length
            ? field.Name
            : propertyPath.Substring(0, propertyPath.Length - fieldName.Length) + field.Name;

        // Apply accumulated overrides if available
        if (overrides != null && value is float baseFloat &&
            overrides.TryGetValue(canonicalPath, out var accum) && accum.HasAnyModification)
        {
            float effective = baseFloat;
            if (accum.hasSetOverride)
                effective = accum.setNumeric;
            else
                effective = (effective + accum.flatDelta) * (1f + accum.percentDelta / 100f);

            result = effective % 1 == 0 ? effective.ToString("0") : effective.ToString("0.##");
            return true;
        }

        if (overrides != null && value is int baseInt &&
            overrides.TryGetValue(canonicalPath, out var accumInt) && accumInt.HasAnyModification)
        {
            float effective = baseInt;
            if (accumInt.hasSetOverride)
                effective = accumInt.setNumeric;
            else
                effective = (effective + accumInt.flatDelta) * (1f + accumInt.percentDelta / 100f);

            result = Mathf.RoundToInt(effective).ToString();
            return true;
        }

        return TryFormatValue(value, out result);
    }

    private static bool TryGetFieldValue(object obj, string fieldName, out string result)
    {
        result = null;
        if (obj == null) return false;

        FieldInfo field = obj.GetType().GetField(fieldName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field == null) return false;

        object value = field.GetValue(obj);
        if (value == null) return false;

        return TryFormatValue(value, out result);
    }

    private static bool TryFormatValue(object value, out string result)
    {
        if (value is float f)
            result = f % 1 == 0 ? f.ToString("0") : f.ToString("0.##");
        else if (value is int i)
            result = i.ToString();
        else if (value is bool b)
            result = b ? "Yes" : "No";
        else
            result = value.ToString();
        return true;
    }
}
