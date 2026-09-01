using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Unity.VisualScripting;

/// <summary>
/// Runtime utility for applying AbilityConfigModifier overrides using cached reflection.
/// Accumulates all trait modifiers targeting a specific ability and produces effective config copies.
/// </summary>

public static class AbilityModifierRuntime
{
    private const float MinRateDenominator = 0.01f;
    private const string TriggeredAbilityAddPathPrefix = "__addTriggeredAbility";
    private const string TriggeredAbilityAddPathSeparator = "|";
    private const string TriggeredAbilityAddMetadataSeparator = "#";
    private const BindingFlags SerializableInstanceFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    
    private struct PathSegment
    {
        public string fieldName;
        public int index;
        public bool hasIndex;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // REFLECTION CACHE
    // ══════════════════════════════════════════════════════════════════════════════

    // Cache: propertyPath → (targetType, fieldInfo, subFieldInfo)
    // E.g., "projectileConfig.damage" → (AbilityDataConfig, projectileConfig field, damage field)
    private static readonly Dictionary<string, PropertyPathInfo> _pathCache = new Dictionary<string, PropertyPathInfo>();

    private struct PropertyPathInfo
    {
        public FieldInfo parentField;  // null if top-level
        public FieldInfo middleField;  // null if 1 or 2 levels
        public FieldInfo targetField;
        public Type fieldType;
        public bool isNumeric;
    }

    private static bool IsSerializedInstanceField(FieldInfo field)
    {
        return field != null
            && !field.IsStatic
            && (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null);
    }

    private static FieldInfo FindSerializedInstanceField(Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, SerializableInstanceFieldFlags | BindingFlags.DeclaredOnly);
            if (IsSerializedInstanceField(field))
                return field;

            type = type.BaseType;
        }

        return null;
    }

    private static IEnumerable<FieldInfo> EnumerateSerializedInstanceFields(Type type)
    {
        if (type == null)
            yield break;

        var typeStack = new Stack<Type>();
        for (Type current = type; current != null; current = current.BaseType)
            typeStack.Push(current);

        while (typeStack.Count > 0)
        {
            Type current = typeStack.Pop();
            foreach (FieldInfo field in current.GetFields(SerializableInstanceFieldFlags | BindingFlags.DeclaredOnly))
            {
                if (IsSerializedInstanceField(field))
                    yield return field;
            }
        }
    }

    private static PropertyPathInfo GetPathInfo(string path)
    {

        if (_pathCache.TryGetValue(path, out var cached))
            return cached;


        PropertyPathInfo info = new PropertyPathInfo();
        Type currentType = typeof(AbilityDataConfig);

        // Support single-level (e.g., "cooldown"), two-level (e.g., "projectileConfig.damage"),
        // and three-level paths (e.g., "explosionConfig.hitbox.damage")
        string[] parts = path.Split('.');

        if (parts.Length == 1)
        {
            info.parentField = null;
            info.middleField = null;
            info.targetField = currentType.GetField(parts[0], BindingFlags.Public | BindingFlags.Instance);
        }

        else if (parts.Length == 2)
        {
            info.parentField = currentType.GetField(parts[0], BindingFlags.Public | BindingFlags.Instance);
            info.middleField = null;
            if (info.parentField != null)
            {
                Type subType = info.parentField.FieldType;
                info.targetField = subType.GetField(parts[1], BindingFlags.Public | BindingFlags.Instance);
            }
        }

        else if (parts.Length == 3)
        {
            info.parentField = currentType.GetField(parts[0], BindingFlags.Public | BindingFlags.Instance);
            if (info.parentField != null)
            {
                Type subType = info.parentField.FieldType;
                info.middleField = subType.GetField(parts[1], BindingFlags.Public | BindingFlags.Instance);
                if (info.middleField != null)
                {
                    Type nestedType = info.middleField.FieldType;
                    info.targetField = nestedType.GetField(parts[2], BindingFlags.Public | BindingFlags.Instance);
                }
            }
        }

        if (info.targetField != null)
        {
            info.fieldType = info.targetField.FieldType;
            info.isNumeric = info.fieldType == typeof(float) || info.fieldType == typeof(int) ||
                             info.fieldType == typeof(double) || info.fieldType == typeof(long);
        }


        return info;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // ACCUMULATED STATE
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Accumulated modifier state for a single property path.
    /// Supports flat additive, additive percent, and set overrides.
    /// </summary>
    public class AccumulatedValue
    {
        public float flatDelta = 0f;
        public float percentDelta = 0f;
        public bool hasSetOverride = false;
        public float setNumeric = 0f;
        public string setString = null;
        public UnityEngine.Object setObject = null;

        public void Reset()
        {
            flatDelta = 0f;
            percentDelta = 0f;
            hasSetOverride = false;
            setNumeric = 0f;
            setString = null;
            setObject = null;
        }

        public void Apply(AbilityPropertyOverride o)
        {
            Apply(o, null);
        }

        /// <summary>
        /// Apply an override with optional tier scaling from the source trait.
        /// </summary>
        public void Apply(AbilityPropertyOverride o, TraitData sourceTrait)
        {
            float value = o.numericValue;
            switch (o.overrideMode)
            {
                case OverrideMode.Flat:
                    flatDelta += value;
                    break;
                case OverrideMode.Percent:
                    percentDelta += value;
                    break;
                case OverrideMode.Set:
                    hasSetOverride = true;
                    setNumeric = value;
                    setString = o.stringValue;
                    setObject = o.objectValue;
                    break;
            }
        }

        /// <summary>
        /// Returns true if any modifications have been applied (flat, percent, or set override).
        /// </summary>
        public bool HasAnyModification =>
            flatDelta != 0f || percentDelta != 0f || hasSetOverride;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // TRAIT-MODIFIER PAIRING (for tier scaling)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Pairs a TraitData with its AbilityConfigModifier so tier scaling can be applied.
    /// </summary>
    public struct TraitModifierPair
    {
        public TraitData trait;
        public AbilityConfigModifier modifier;
        public int traitLevel;
        public TraitModifierPair(TraitData trait, AbilityConfigModifier modifier, int traitLevel)
        {
            this.trait = trait;
            this.modifier = modifier;
            this.traitLevel = traitLevel;
        }
    }


    /// <summary>
    /// Accumulates all overrides with tier scaling support.
    /// Pass TraitModifierPairs so each override can be scaled by its source trait's tier.
    /// </summary>
    public static Dictionary<string, AccumulatedValue> AccumulateOverrides(
        AbilityDataConfig targetConfig,
        IEnumerable<TraitModifierPair> traitModifierPairs)
    {
        var result = new Dictionary<string, AccumulatedValue>();
        foreach (var pair in traitModifierPairs)
        {
            if (pair.modifier == null) continue;
            if (!IsMatchingTargetAbility(pair.modifier.targetAbility, targetConfig)) continue;
            if (pair.modifier.overrides == null) continue;

            int level = Mathf.Max(1, pair.traitLevel);

            foreach (var o in pair.modifier.overrides)
            {
                if (o.isEmpty) continue;
                string normalizedPath = NormalizePropertyPath(targetConfig, o.propertyPath);
                if (string.IsNullOrEmpty(normalizedPath))
                    continue;

                if (!result.TryGetValue(normalizedPath, out var acc))
                {
                    acc = new AccumulatedValue();
                    result[normalizedPath] = acc;
                }
                float baseValue = o.numericValue;

                if (o.overrideMode == OverrideMode.Flat || o.overrideMode == OverrideMode.Percent)
                {
                    float scaledValue = baseValue * level;

                    switch (o.overrideMode)
                    {
                        case OverrideMode.Flat:
                            acc.flatDelta += scaledValue;
                            break;
                        case OverrideMode.Percent:
                            acc.percentDelta += scaledValue;
                            break;
                    }

                    Debug.Log($"[AbilityModifierRuntime] Scaled Override -> Path: '{normalizedPath}', Mode: {o.overrideMode}, Base: {baseValue}, Level: {level}, Scaled Applied: {scaledValue} (Total Flat Delta: {acc.flatDelta}, Total % Delta: {acc.percentDelta})");
                }
                else
                {
                    // Set overrides do not scale linearly (e.g., swapping a visual sprite or string text)
                    acc.Apply(o, pair.trait);
                    Debug.Log($"[AbilityModifierRuntime] Set Override -> Path: '{normalizedPath}', Applied exact value without scaling.");
                }
            }
            if (TryGetTriggeredAddition(pair.modifier, out AbilityDataConfig abilityConfig, out float triggerChance, out TriggeredAbilityTriggerTiming triggerTiming))
            {
                AddTriggeredAbilityAccumulatedEntry(result, abilityConfig, triggerChance, triggerTiming, pair.modifier.addTriggeredAbilityPath);
            }
        }
        return result;
    }

    private static bool TryGetTriggeredAddition(
        AbilityConfigModifier modifier,
        out AbilityDataConfig abilityConfig,
        out float triggerChance,
        out TriggeredAbilityTriggerTiming triggerTiming)
    {
        abilityConfig = null;
        triggerChance = 1f;
        triggerTiming = TriggeredAbilityTriggerTiming.OnHit;

        if (modifier == null)
            return false;

        if (modifier.addTriggeredAbilityConfig != null && modifier.addTriggeredAbilityConfig.abilityConfig != null)
        {
            abilityConfig = modifier.addTriggeredAbilityConfig.abilityConfig;
            triggerChance = Mathf.Clamp01(modifier.addTriggeredAbilityConfig.triggerChance);
            triggerTiming = modifier.addTriggeredAbilityConfig.triggerTiming;
            return true;
        }

        if (modifier.addTriggeredAbilityLegacy != null)
        {
            abilityConfig = modifier.addTriggeredAbilityLegacy;
            return true;
        }

        return false;
    }

    private static void AddTriggeredAbilityAccumulatedEntry(
        Dictionary<string, AccumulatedValue> result,
        AbilityDataConfig triggeredAbility,
        float triggerChance,
        TriggeredAbilityTriggerTiming triggerTiming,
        string targetEffectPath)
    {
        if (result == null || triggeredAbility == null)
            return;

        string normalizedPath = string.IsNullOrEmpty(targetEffectPath)
            ? ""
            : targetEffectPath.Trim();

        int entryIndex = 0;
        string key;
        do
        {
            key = $"{TriggeredAbilityAddPathPrefix}{TriggeredAbilityAddPathSeparator}{normalizedPath}{TriggeredAbilityAddPathSeparator}[{entryIndex}]";
            entryIndex++;
        }
        while (result.ContainsKey(key));

        result[key] = new AccumulatedValue
        {
            hasSetOverride = true,
            setObject = triggeredAbility,
            setString = $"{Mathf.Clamp01(triggerChance).ToString("R", CultureInfo.InvariantCulture)}{TriggeredAbilityAddMetadataSeparator}{(int)triggerTiming}"
        };
    }

    private static bool IsMatchingTargetAbility(AbilityDataConfig modifierTarget, AbilityDataConfig runtimeTarget)
    {
        if (modifierTarget == null || runtimeTarget == null)
            return false;

        if (ReferenceEquals(modifierTarget, runtimeTarget))
            return true;

        // Runtime systems may hand around shallow-cloned AbilityDataConfig instances.
        // Fall back to abilityName matching so trait modifiers still bind correctly.
        return string.Equals(modifierTarget.abilityName, runtimeTarget.abilityName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Normalizes legacy construct modifier paths to the current indexed constructAbilities format.
    /// Keeps existing trait assets functional after removing duplicate construct-level sub-config fields.
    /// </summary>
    private static string NormalizePropertyPath(AbilityDataConfig targetConfig, string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return propertyPath;

        if (targetConfig == null || !targetConfig.isConstructAbility || targetConfig.constructConfig == null)
            return propertyPath;

        return propertyPath;
    }


    // ══════════════════════════════════════════════════════════════════════════════
    // APPLICATION
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the effective value for a property path given the base config and accumulated overrides.
    /// Returns the modified value, or the base value if no overrides exist for that path.
    /// </summary>
    public static object GetEffectiveValue(
        AbilityDataConfig baseConfig,
        string propertyPath,
        Dictionary<string, AccumulatedValue> accumulatedOverrides)
    {
        if (!TryResolvePathValue(baseConfig, propertyPath, out object baseValue, out Type fieldType))
            return null;

        bool isNumeric = fieldType == typeof(float)
            || fieldType == typeof(int)
            || fieldType == typeof(double)
            || fieldType == typeof(long);

        if (!accumulatedOverrides.TryGetValue(propertyPath, out var acc) || !acc.HasAnyModification) return baseValue;

        if (acc.hasSetOverride)
        {
            if (fieldType == typeof(bool))
                return acc.setNumeric != 0f;
            if (fieldType.IsEnum)
            {
                int enumIndex = Mathf.RoundToInt(acc.setNumeric);
                Array values = Enum.GetValues(fieldType);
                if (values.Length == 0) return baseValue;
                enumIndex = Mathf.Clamp(enumIndex, 0, values.Length - 1);
                return values.GetValue(enumIndex);
            }
            if (fieldType == typeof(string))
                return !string.IsNullOrEmpty(acc.setString) ? acc.setString : baseValue;
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
                return acc.setObject ?? baseValue;
            if (fieldType == typeof(int))
                return (int)acc.setNumeric;
            return acc.setNumeric;
        }

        if (isNumeric)
        {
            float baseFloat = fieldType == typeof(int) ? (int)baseValue : Convert.ToSingle(baseValue);
            float result = ApplyNumericModifiers(baseFloat, acc, propertyPath);
            return fieldType == typeof(int) ? (object)(int)result : result;
        }
        return baseValue;
    }

    /// <summary>
    /// Creates a shallow copy of a sub-config (e.g., ProjectileConfig) with all accumulated overrides applied.
    /// Returns null if no modifications exist.
    /// </summary>
    public static T BuildEffectiveSubConfig<T>(
        T baseConfig,
        string subConfigPath,
        Dictionary<string, AccumulatedValue> accumulatedOverrides) where T : class, new()
    {
        if (baseConfig == null) return null;
        if (accumulatedOverrides == null || accumulatedOverrides.Count == 0) return null;

        // Check if any overrides exist for this sub-config
        bool hasAnyMods = false;
        string prefix = subConfigPath + ".";
        foreach (var kvp in accumulatedOverrides)
        {
            if (kvp.Key.StartsWith(prefix) && kvp.Value.HasAnyModification)
            {
                hasAnyMods = true;
                break;
            }
        }
        if (!hasAnyMods) return null;

        // Create shallow copy
        T copy = new T();
        foreach (FieldInfo field in EnumerateSerializedInstanceFields(typeof(T)))
        {
            field.SetValue(copy, field.GetValue(baseConfig));
        }

        // Apply overrides (direct fields on T)
        foreach (var kvp in accumulatedOverrides)
        {
            if (!kvp.Key.StartsWith(prefix)) continue;
            if (!kvp.Value.HasAnyModification) continue;
            string remainder = kvp.Key.Substring(prefix.Length);

            if (remainder.Contains("."))
            {
                ApplyAccumulatedToNestedPath(typeof(T), baseConfig, copy, remainder, kvp.Value);
                continue;
            }

            FieldInfo field = FindSerializedInstanceField(typeof(T), remainder);
            if (field == null) continue;

            ApplyAccumulatedToField(field, baseConfig, copy, kvp.Value, kvp.Key);
        }

        return copy;
    }

    /// <summary>
    /// Creates an effective runtime copy of an ability config with top-level and sub-config overrides applied.
    /// Returns the base config if there are no modifications.
    /// </summary>
    public static AbilityDataConfig BuildEffectiveAbilityConfig(
        AbilityDataConfig baseConfig,
        Dictionary<string, AccumulatedValue> accumulatedOverrides)
    {
        if (baseConfig == null)
            return null;

        if (accumulatedOverrides == null || accumulatedOverrides.Count == 0)
            return baseConfig;

        bool hasAnyMods = false;
        foreach (var kvp in accumulatedOverrides)
        {
            if (kvp.Value != null && kvp.Value.HasAnyModification)
            {
                hasAnyMods = true;
                break;
            }
        }

        if (!hasAnyMods)
            return baseConfig;

        AbilityDataConfig copy = ScriptableObject.CreateInstance<AbilityDataConfig>();
        foreach (var field in typeof(AbilityDataConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            field.SetValue(copy, field.GetValue(baseConfig));
        }

        foreach (var kvp in accumulatedOverrides)
        {
            if (kvp.Key.Contains(".") || !kvp.Value.HasAnyModification)
                continue;

            var field = typeof(AbilityDataConfig).GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
                continue;

            ApplyAccumulatedToField(field, baseConfig, copy, kvp.Value);
        }

        copy.projectileConfig = BuildEffectiveSubConfig(baseConfig.projectileConfig, "projectileConfig", accumulatedOverrides) ?? copy.projectileConfig;
        copy.areaConfig = BuildEffectiveSubConfig(baseConfig.areaConfig, "areaConfig", accumulatedOverrides) ?? copy.areaConfig;
        copy.meleeConfig = BuildEffectiveSubConfig(baseConfig.meleeConfig, "meleeConfig", accumulatedOverrides) ?? copy.meleeConfig;
        copy.beamConfig = BuildEffectiveSubConfig(baseConfig.beamConfig, "beamConfig", accumulatedOverrides) ?? copy.beamConfig;
        copy.channelConfig = BuildEffectiveSubConfig(baseConfig.channelConfig, "channelConfig", accumulatedOverrides) ?? copy.channelConfig;
        copy.explosionConfig = BuildEffectiveSubConfig(baseConfig.explosionConfig, "explosionConfig", accumulatedOverrides) ?? copy.explosionConfig;
        copy.movementConfig = BuildEffectiveSubConfig(baseConfig.movementConfig, "movementConfig", accumulatedOverrides) ?? copy.movementConfig;
        copy.summonConfig = BuildEffectiveSubConfig(baseConfig.summonConfig, "summonConfig", accumulatedOverrides) ?? copy.summonConfig;
        copy.constructConfig = BuildEffectiveSubConfig(baseConfig.constructConfig, "constructConfig", accumulatedOverrides) ?? copy.constructConfig;
        copy.trapConfig = BuildEffectiveSubConfig(baseConfig.trapConfig, "trapConfig", accumulatedOverrides) ?? copy.trapConfig;
        copy.holdChargeConfig = BuildEffectiveSubConfig(baseConfig.holdChargeConfig, "holdChargeConfig", accumulatedOverrides) ?? copy.holdChargeConfig;
        copy.passiveConfig = BuildEffectiveSubConfig(baseConfig.passiveConfig, "passiveConfig", accumulatedOverrides) ?? copy.passiveConfig;

        ApplyTriggeredAbilityAdditions(copy, accumulatedOverrides);

        return copy;
    }

    private static void ApplyTriggeredAbilityAdditions(
        AbilityDataConfig targetConfig,
        Dictionary<string, AccumulatedValue> accumulatedOverrides)
    {
        if (targetConfig == null || accumulatedOverrides == null || accumulatedOverrides.Count == 0)
            return;

        foreach (var kvp in accumulatedOverrides)
        {
            if (!kvp.Key.StartsWith(TriggeredAbilityAddPathPrefix, StringComparison.Ordinal))
                continue;

            AbilityDataConfig triggeredAbility = kvp.Value?.setObject as AbilityDataConfig;
            if (triggeredAbility == null)
                continue;

            ParseTriggeredAbilityMetadata(
                kvp.Value?.setString,
                out float triggerChance,
                out TriggeredAbilityTriggerTiming triggerTiming);

            string targetEffectPath = ParseTriggeredAbilityTargetPath(kvp.Key);
            if (!string.IsNullOrEmpty(targetEffectPath))
            {
                if (TryResolvePathValue(targetConfig, targetEffectPath, out object resolvedEffectData, out Type resolvedType)
                    && resolvedType == typeof(EffectData)
                    && resolvedEffectData is EffectData effectData)
                {
                    AppendTriggeredAbility(effectData, triggeredAbility, triggerChance, triggerTiming);
                }

                continue;
            }

            AppendTriggeredAbilityToConfiguredEffects(targetConfig, triggeredAbility, triggerChance, triggerTiming);
        }
    }

    private static void ParseTriggeredAbilityMetadata(
        string encodedMetadata,
        out float triggerChance,
        out TriggeredAbilityTriggerTiming triggerTiming)
    {
        triggerChance = 1f;
        triggerTiming = TriggeredAbilityTriggerTiming.OnHit;

        if (string.IsNullOrEmpty(encodedMetadata))
            return;

        string[] parts = encodedMetadata.Split(new[] { TriggeredAbilityAddMetadataSeparator }, StringSplitOptions.None);

        if (parts.Length > 0 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedChance))
            triggerChance = Mathf.Clamp01(parsedChance);

        if (parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTiming))
        {
            if (Enum.IsDefined(typeof(TriggeredAbilityTriggerTiming), parsedTiming))
                triggerTiming = (TriggeredAbilityTriggerTiming)parsedTiming;
        }
    }

    private static string ParseTriggeredAbilityTargetPath(string encodedKey)
    {
        if (string.IsNullOrEmpty(encodedKey))
            return "";

        string prefixWithSeparator = TriggeredAbilityAddPathPrefix + TriggeredAbilityAddPathSeparator;
        if (!encodedKey.StartsWith(prefixWithSeparator, StringComparison.Ordinal))
            return "";

        int firstSeparator = TriggeredAbilityAddPathPrefix.Length;
        int secondSeparator = encodedKey.IndexOf(TriggeredAbilityAddPathSeparator, firstSeparator + TriggeredAbilityAddPathSeparator.Length, StringComparison.Ordinal);
        if (secondSeparator < 0)
            return "";

        int pathStart = firstSeparator + TriggeredAbilityAddPathSeparator.Length;
        int pathLength = secondSeparator - pathStart;
        if (pathLength <= 0)
            return "";

        return encodedKey.Substring(pathStart, pathLength);
    }

    private static void AppendTriggeredAbilityToConfiguredEffects(
        AbilityDataConfig config,
        AbilityDataConfig triggeredAbility,
        float triggerChance,
        TriggeredAbilityTriggerTiming triggerTiming)
    {
        if (config == null || triggeredAbility == null)
            return;

        if (config.isProjectileAbility && config.projectileConfig?.hitbox != null)
            AppendTriggeredAbility(config.projectileConfig.hitbox.onHitEffects, triggeredAbility, triggerChance, triggerTiming);

        if (config.isAreaAbility && config.areaConfig?.hitbox != null)
            AppendTriggeredAbility(config.areaConfig.hitbox.onHitEffects, triggeredAbility, triggerChance, triggerTiming);

        if (config.isMeleeAbility && config.meleeConfig?.hitbox != null)
            AppendTriggeredAbility(config.meleeConfig.hitbox.onHitEffects, triggeredAbility, triggerChance, triggerTiming);

        if (config.isExplosionAbility && config.explosionConfig?.hitbox != null)
            AppendTriggeredAbility(config.explosionConfig.hitbox.onHitEffects, triggeredAbility, triggerChance, triggerTiming);

        if (config.isBeamAbility && config.beamConfig != null)
            AppendTriggeredAbility(config.beamConfig.onHitEffects, triggeredAbility, triggerChance, triggerTiming);

        if (config.isChanneled && config.channelConfig != null)
            AppendTriggeredAbility(config.channelConfig.onHitEffects, triggeredAbility, triggerChance, triggerTiming);
        if (config.isTrapAbility && config.trapConfig != null)
        {
            if (config.trapConfig.abilityType == TrapAbilityType.Area && config.trapConfig.areaConfig?.hitbox != null)
                AppendTriggeredAbility(config.trapConfig.areaConfig.hitbox.onHitEffects, triggeredAbility, triggerChance, triggerTiming);

            if (config.trapConfig.abilityType == TrapAbilityType.Projectile && config.trapConfig.projectileConfig?.hitbox != null)
                AppendTriggeredAbility(config.trapConfig.projectileConfig.hitbox.onHitEffects, triggeredAbility, triggerChance, triggerTiming);

            if (config.trapConfig.abilityType == TrapAbilityType.Explosion && config.trapConfig.explosionConfig?.hitbox != null)
                AppendTriggeredAbility(config.trapConfig.explosionConfig.hitbox.onHitEffects, triggeredAbility, triggerChance, triggerTiming);
        }
    }

    private static void AppendTriggeredAbility(
        EffectData effectData,
        AbilityDataConfig triggeredAbility,
        float triggerChance,
        TriggeredAbilityTriggerTiming triggerTiming)
    {
        if (effectData == null || triggeredAbility == null)
            return;

        EffectData.TriggeredAbilityConfig[] existing = effectData.triggeredAbilityConfigs ?? Array.Empty<EffectData.TriggeredAbilityConfig>();
        var appended = new EffectData.TriggeredAbilityConfig[existing.Length + 1];

        for (int i = 0; i < existing.Length; i++)
            appended[i] = existing[i];

        appended[existing.Length] = new EffectData.TriggeredAbilityConfig
        {
            abilityConfig = triggeredAbility,
            triggerChance = Mathf.Clamp01(triggerChance),
            triggerTiming = triggerTiming
        };

        effectData.canTriggerAbility = true;
        effectData.triggeredAbilityConfigs = appended;
    }

    private static bool HasPathModification(Dictionary<string, AccumulatedValue> accumulatedOverrides, string propertyPath)
    {
        return accumulatedOverrides.TryGetValue(propertyPath, out var accum)
            && accum != null
            && accum.HasAnyModification;
    }

    /// <summary>
    /// Collects effective overrides for a target ability from the owner's active traits.
    /// </summary>
    public static Dictionary<string, AccumulatedValue> AccumulateOverridesFromOwner(GameObject owner, AbilityDataConfig targetConfig)
    {
        if (owner == null || targetConfig == null)
            return null;

        CharacterTraitManager traitManager = owner.GetComponent<CharacterTraitManager>();
        if (traitManager == null)
            return null;

        var traitModifierPairs = new List<TraitModifierPair>();

        // Cleanly pull the runtime Trait instances instead of trying to cross-reference data indices
        foreach (Trait runtimeTrait in traitManager.GetActiveRuntimeTraits())
        {
            TraitData data = runtimeTrait.data;
            if (data?.abilityConfigModifiers == null) continue;

            // Extract the runtime level directly from the active wrapper instance
            int currentLevel = Mathf.Max(1, runtimeTrait.level);

            foreach (var modifier in data.abilityConfigModifiers)
            {
                // Pass the data, modifier config, and level safely into the modifier pair array
                traitModifierPairs.Add(new TraitModifierPair(data, modifier, currentLevel));
            }
        }

        if (traitModifierPairs.Count == 0)
            return null;

        return AccumulateOverrides(targetConfig, traitModifierPairs);
    }

    /// <summary>
    /// Apply an AccumulatedValue to a single field on a copy object, reading base from the original.
    /// </summary>
    private static void ApplyAccumulatedToField(FieldInfo field, object baseObj, object copyObj, AccumulatedValue accum, string propertyPath = null)
    {
        object baseValue = field.GetValue(baseObj);

        if (field.FieldType == typeof(StatContainer) && baseValue is StatContainer baseStats)
        {
            StatContainer copyStats = field.GetValue(copyObj) as StatContainer;
            if (copyStats == null || ReferenceEquals(copyStats, baseStats))
            {
                // Source 22: Leverage your built-in deep-cloning routine to prevent asset corruption!
                copyStats = baseStats.Clone();
                field.SetValue(copyObj, copyStats);
            }

            // Extract the sub-stat name from the path property string map (e.g., "summonConfig.baseStatTemplate.MoveSpeed")
            if (!string.IsNullOrEmpty(propertyPath))
            {
                int lastDot = propertyPath.LastIndexOf('.');
                if (lastDot >= 0 && lastDot < propertyPath.Length - 1)
                {
                    string targetStatId = propertyPath.Substring(lastDot + 1);

                    // Source 22: Pull the clean, un-modified base template number safely
                    float baseStatValue = baseStats.GetStat(targetStatId, 0f);

                    // Apply your standard flat and percentage trait modifiers
                    float modifiedValue = baseStatValue + accum.flatDelta;

                    if (UsesRateDurationFormula(targetStatId)) // Cooldown/Speed checks
                    {
                        float denominator = Mathf.Max(MinRateDenominator, 1f + (accum.percentDelta / 100f));
                        modifiedValue /= denominator;
                    }
                    else
                    {
                        modifiedValue *= (1f + accum.percentDelta / 100f);
                    }

                    if (accum.hasSetOverride) modifiedValue = accum.setNumeric;

                    // Source 22: Commit the fresh tier-scaled property value straight into the copy container!
                    copyStats.SetStat(targetStatId, modifiedValue);
                    Debug.Log($"[StatModifierSync] Calculated override: {propertyPath} -> Base: {baseStatValue}, Scaled: {modifiedValue}");
                }
            }
            return;
        }
        //for setting values
        if (accum.hasSetOverride)
        {
            switch (field.FieldType)
            {
                case Type t when t == typeof(bool):
                    field.SetValue(copyObj, accum.setNumeric != 0f);
                    break;
                case Type t when t.IsEnum:
                    int enumIndex = Mathf.RoundToInt(accum.setNumeric);
                    Array values = Enum.GetValues(field.FieldType);
                    if (values.Length > 0)
                    {
                        enumIndex = Mathf.Clamp(enumIndex, 0, values.Length - 1);
                        field.SetValue(copyObj, values.GetValue(enumIndex));
                    }
                    break;
                case Type t when t == typeof(string) && !string.IsNullOrEmpty(accum.setString):
                    field.SetValue(copyObj, accum.setString);
                    break;
                case Type t when typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && accum.setObject != null:
                    field.SetValue(copyObj, accum.setObject);
                    break;
                case Type t when t == typeof(int):
                    field.SetValue(copyObj, (int)accum.setNumeric);
                    break;
                case Type t when t == typeof(float):
                    field.SetValue(copyObj, accum.setNumeric);
                    break;
            }
        }
        //multiplies or adds to values
        else if (field.FieldType == typeof(float) || field.FieldType == typeof(int))
        {
            float baseFloat = field.FieldType == typeof(int) ? (int)baseValue : (float)baseValue;
            float result = ApplyNumericModifiers(baseFloat, accum, propertyPath);
            if (field.FieldType == typeof(int))
                field.SetValue(copyObj, (int)result);
            else
                field.SetValue(copyObj, result);
        }
    }

    private static float ApplyNumericModifiers(float baseFloat, AccumulatedValue accum, string propertyPath)
    {
        float flatAdjusted = baseFloat + accum.flatDelta;

        // Cooldown-like durations use a rate model so positive % speeds recharge and
        // asymptotically approaches 0 without ever reaching it.
        if (UsesRateDurationFormula(propertyPath))
        {
            float denominator = Mathf.Max(MinRateDenominator, 1f + (accum.percentDelta / 100f));
            float rateAdjusted = flatAdjusted / denominator;
            return rateAdjusted;
        }
        float finalValue = flatAdjusted * (1f + accum.percentDelta / 100f);
        return finalValue;
    }

    private static bool UsesRateDurationFormula(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return false;

        return propertyPath == "cooldownTime"
            || propertyPath == "chargeRechargeTime"
            || propertyPath == "holdChargeConfig.barDuration";
    }

    /// <summary>
    /// Applies an accumulated override to a nested field path on a copied sub-config.
    /// Supports arbitrary depth (e.g., "meleeConfig.onHitEffects.canBurn").
    /// </summary>
    private static void ApplyAccumulatedToNestedPath(Type rootType, object baseRoot, object copyRoot, string nestedPath, AccumulatedValue accum)
    {
        if (baseRoot == null || copyRoot == null || string.IsNullOrEmpty(nestedPath))
            return;

        string[] rawParts = nestedPath.Split('.');
        if (rawParts.Length < 2)
            return;

        var parts = new List<PathSegment>(rawParts.Length);
        for (int i = 0; i < rawParts.Length; i++)
        {
            if (!TryParsePathSegment(rawParts[i], out PathSegment segment))
                return;
            parts.Add(segment);
        }

        object currentBase = baseRoot;
        object currentCopy = copyRoot;
        Type currentType = rootType;

        // Track the immediate parent container object and its Type layer to allow correct re-assignment
        object parentCopyObject = null;
        FieldInfo parentFieldInfo = null;

        for (int i = 0; i < parts.Count - 1; i++)
        {
            PathSegment segment = parts[i];
            if (currentType == typeof(StatContainer) && currentBase is StatContainer baseStats)
            {
                return;
            }

            FieldInfo stepField = FindSerializedInstanceField(currentType, segment.fieldName);
            if (stepField == null) return;

            // Cache parent assignment scopes before advancing down the type tree
            parentCopyObject = currentCopy;
            parentFieldInfo = stepField;

            object nextBase = stepField.GetValue(currentBase);
            if (nextBase == null) return;
            object nextCopy = stepField.GetValue(currentCopy);

            if (segment.hasIndex)
            {
                if (nextBase is Array baseArray)
                {
                    if (!(nextCopy is Array copyArray)) return;
                    if (segment.index < 0 || segment.index >= baseArray.Length || segment.index >= copyArray.Length) return;
                    if (ReferenceEquals(copyArray, baseArray))
                    {
                        copyArray = (Array)baseArray.Clone();
                        stepField.SetValue(currentCopy, copyArray);
                    }
                    object baseElement = baseArray.GetValue(segment.index);
                    if (baseElement == null) return;
                    object copyElement = copyArray.GetValue(segment.index);
                    if (copyElement == null || ReferenceEquals(copyElement, baseElement))
                    {
                        copyElement = CloneSerializableObject(baseElement);
                        if (copyElement == null) return;
                        copyArray.SetValue(copyElement, segment.index);
                    }
                    currentBase = baseElement;
                    currentCopy = copyElement;
                    currentType = baseElement.GetType();
                    continue;
                }
                if (nextBase is IList baseList)
                {
                    if (!(nextCopy is IList copyList)) return;
                    if (segment.index < 0 || segment.index >= baseList.Count) return;
                    if (ReferenceEquals(copyList, baseList))
                    {
                        object listClone = CloneListShallow(baseList);
                        if (!(listClone is IList clonedList)) return;
                        copyList = clonedList;
                        stepField.SetValue(currentCopy, copyList);
                    }
                    if (segment.index >= copyList.Count) return;
                    object baseElement = baseList[segment.index];
                    if (baseElement == null) return;
                    object copyElement = copyList[segment.index];
                    if (copyElement == null || ReferenceEquals(copyElement, baseElement))
                    {
                        copyElement = CloneSerializableObject(baseElement);
                        if (copyElement == null) return;
                        copyList[segment.index] = copyElement;
                    }
                    currentBase = baseElement;
                    currentCopy = copyElement;
                    currentType = baseElement.GetType();
                    continue;
                }
                return;
            }

            if (nextCopy == null || ReferenceEquals(nextCopy, nextBase))
            {
                nextCopy = CloneSerializableObject(nextBase);
                if (nextCopy == null) return;
                stepField.SetValue(currentCopy, nextCopy);
            }

            currentBase = nextBase;
            currentCopy = nextCopy;
            currentType = nextCopy != null ? nextCopy.GetType() : nextBase.GetType();
        }

        PathSegment targetSegment = parts[parts.Count - 1];
        if (targetSegment.hasIndex) return;

        // ── 🔒 FIXED: BOUNDED STAT CONTAINER OVERRIDE APPLICATION ──
        if (currentType == typeof(StatContainer) && currentBase is StatContainer statContainerBase)
        {
            StatContainer statContainerCopy = currentCopy as StatContainer;
            if (statContainerCopy == null || ReferenceEquals(statContainerCopy, statContainerBase))
            {
                // Clone the baseline stats cleanly
                statContainerCopy = statContainerBase.Clone();

                // FIX: Use the cached parent field info context to re-assign the isolated clone instance back into the copied structure!
                if (parentFieldInfo != null && parentCopyObject != null)
                {
                    parentFieldInfo.SetValue(parentCopyObject, statContainerCopy);
                }
            }

            string targetStatId = targetSegment.fieldName;
            float baseStatValue = statContainerBase.GetStat(targetStatId, 0f);
            float modifiedValue = baseStatValue + accum.flatDelta;

            string fullPath = string.IsNullOrEmpty(nestedPath) ? null : $"{rootType.Name}.{nestedPath}";

            // Fixed: Check rate modifiers via accum.percentDelta instead of raw flat deltas
            if (UsesRateDurationFormula(fullPath ?? targetStatId))
            {
                float denominator = Mathf.Max(MinRateDenominator, 1f + (accum.percentDelta / 100f));
                modifiedValue = (baseStatValue + accum.flatDelta) / denominator;
            }
            else
            {
                modifiedValue *= (1f + accum.percentDelta / 100f);
            }

            if (accum.hasSetOverride) modifiedValue = accum.setNumeric;

            // Update only the decoupled instance wrapper
            statContainerCopy.SetStat(targetStatId, modifiedValue);
            return;
        }

        FieldInfo targetField = FindSerializedInstanceField(currentType, targetSegment.fieldName);
        if (targetField == null) return;

        string normalizedFullPath = string.IsNullOrEmpty(nestedPath) ? null : $"{rootType.Name}.{nestedPath}";
        if (rootType == typeof(HoldChargeConfig) && !string.IsNullOrEmpty(nestedPath))
            normalizedFullPath = $"holdChargeConfig.{nestedPath}";

        ApplyAccumulatedToField(targetField, currentBase, currentCopy, accum, normalizedFullPath);
    }

    private static bool TryParsePathSegment(string rawSegment, out PathSegment segment)
    {
        segment = default;
        if (string.IsNullOrEmpty(rawSegment))
            return false;

        int bracketStart = rawSegment.IndexOf('[');
        if (bracketStart < 0)
        {
            segment.fieldName = rawSegment;
            segment.index = -1;
            segment.hasIndex = false;
            return true;
        }

        int bracketEnd = rawSegment.IndexOf(']', bracketStart + 1);
        if (bracketEnd < 0)
            return false;

        string fieldName = rawSegment.Substring(0, bracketStart);
        string indexText = rawSegment.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
        if (string.IsNullOrEmpty(fieldName) || !int.TryParse(indexText, out int index))
            return false;

        segment.fieldName = fieldName;
        segment.index = index;
        segment.hasIndex = true;
        return true;
    }

    private static bool TryResolvePathValue(object root, string propertyPath, out object value, out Type valueType)
    {
        value = null;
        valueType = null;

        if (root == null || string.IsNullOrEmpty(propertyPath))
            return false;

        string[] rawParts = propertyPath.Split('.');
        object current = root;
        Type currentType = root.GetType();

        for (int i = 0; i < rawParts.Length; i++)
        {
            if (!TryParsePathSegment(rawParts[i], out PathSegment segment))
                return false;

            FieldInfo field = FindSerializedInstanceField(currentType, segment.fieldName);
            if (field == null)
                return false;

            object fieldValue = field.GetValue(current);
            if (fieldValue == null)
                return false;

            if (segment.hasIndex)
            {
                if (fieldValue is Array arr)
                {
                    if (segment.index < 0 || segment.index >= arr.Length)
                        return false;

                    object elementValue = arr.GetValue(segment.index);
                    if (elementValue == null)
                        return false;

                    current = elementValue;
                    currentType = elementValue.GetType();
                    continue;
                }

                if (fieldValue is IList list)
                {
                    if (segment.index < 0 || segment.index >= list.Count)
                        return false;

                    object elementValue = list[segment.index];
                    if (elementValue == null)
                        return false;

                    current = elementValue;
                    currentType = elementValue.GetType();
                    continue;
                }

                return false;
            }

            current = fieldValue;
            currentType = fieldValue.GetType();
        }

        value = current;
        valueType = currentType;
        return true;
    }

    /// <summary>
    /// Creates a shallow clone of a serializable object by copying public instance fields.
    /// </summary>
    private static object CloneSerializableObject(object source)
    {
        if (source == null)
            return null;

        Type type = source.GetType();

        if (type == typeof(string) || type.IsValueType)
            return source;

        if (source is StatContainer statContainer)
            return statContainer.Clone();

        if (typeof(ScriptableObject).IsAssignableFrom(type))
            return UnityEngine.Object.Instantiate((ScriptableObject)source);

        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return source;

        if (source is IList sourceList)
            return CloneListShallow(sourceList);

        object clone;
        try
        {
            clone = Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }

        foreach (FieldInfo field in EnumerateSerializedInstanceFields(type))
        {
            field.SetValue(clone, field.GetValue(source));
        }

        return clone;
    }

    /// <summary>
    /// Clones a list instance by creating the same list type and copying element references.
    /// This preserves indexed paths while still allowing per-element clone-on-write.
    /// </summary>
    private static IList CloneListShallow(IList source)
    {
        if (source == null)
            return null;

        IList clone;
        try
        {
            clone = Activator.CreateInstance(source.GetType()) as IList;
        }
        catch
        {
            return null;
        }

        if (clone == null)
            return null;

        for (int i = 0; i < source.Count; i++)
            clone.Add(source[i]);

        return clone;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // UTILITY - GET ALL MODIFIABLE PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════════

    // Top-level fields we want to expose for modification
    private static readonly HashSet<string> _topLevelModifiableFields = new HashSet<string>
    {
        "attackSpeed", "cooldownTime", "energyCost", "maxCharges", "chargeRechargeTime",
    "movementBlockDuration", "autocastRange", "autocastTargets", "castAtFeet", "castAtTargets", "castAtFriendlyTargets", "baseCritChance", "baseCritDamageMultiplier",
       "mainhandAnimationName", "precastAnimationName", "retaliationCast"
    };

    /// <summary>
    /// Returns all modifiable property paths for an AbilityDataConfig.
    /// Used by the editor drawer to populate the property dropdown.
    /// </summary>
    public static List<string> GetAllModifiableProperties(AbilityDataConfig config)
    {
        var result = new List<string>();

        // Top-level numeric fields
        AddFieldsFromType(typeof(AbilityDataConfig), "", result, topLevelOnly: true);

        // Sub-config fields (only if enabled on the config)
        if (config != null)
        {
            if (config.isProjectileAbility && config.projectileConfig != null)
            {
                AddHitboxConfigFields("projectileConfig.hitbox.", result, config.projectileConfig.hitbox);
                AddFieldsFromType(typeof(ProjectileConfig), "projectileConfig.", result);
            }
            if (config.isAreaAbility && config.areaConfig != null)
            {
                AddHitboxConfigFields("areaConfig.hitbox.", result, config.areaConfig.hitbox);
                AddFieldsFromType(typeof(AreaConfig), "areaConfig.", result);
            }
            if (config.isMeleeAbility && config.meleeConfig != null)
            {
                AddHitboxConfigFields("meleeConfig.hitbox.", result, config.meleeConfig.hitbox);
                AddFieldsFromType(typeof(MeleeConfig), "meleeConfig.", result);

            }
            if (config.isBeamAbility && config.beamConfig != null)
            {
                AddFieldsFromType(typeof(BeamAbilityConfig), "beamConfig.", result);
                AddEffectDataFields("beamConfig.onHitEffects.", result, config.beamConfig.onHitEffects);
                AddLifeStealConfigFields("beamConfig.lifeSteal.", result);
            }
            if (config.isChanneled && config.channelConfig != null)
            {
                AddFieldsFromType(typeof(ChannelAbilityConfig), "channelConfig.", result);
                AddEffectDataFields("channelConfig.onHitEffects.", result, config.channelConfig.onHitEffects);
                AddLifeStealConfigFields("channelConfig.lifeSteal.", result);
            }
            if (config.isExplosionAbility && config.explosionConfig != null)
            {
                AddHitboxConfigFields("explosionConfig.hitbox.", result, config.explosionConfig.hitbox);
                AddFieldsFromType(typeof(ExplosionConfig), "explosionConfig.", result);
            }
            if (config.isMovementAbility && config.movementConfig != null)
                AddFieldsFromType(typeof(MovementConfig), "movementConfig.", result);
            if (config.holdChargeConfig != null)
                AddFieldsFromType(typeof(HoldChargeConfig), "holdChargeConfig.", result);
            if (config.isPassiveAbility && config.passiveConfig != null && config.passiveConfig.PassiveAbility != null)
            {
                AddCustomPassiveConfigFields("passiveConfig.passiveAbility.", result, config.passiveConfig.PassiveAbility.GetType());
            }
            if (config.isSummonAbility && config.summonConfig != null)
            {
                AddFieldsFromType(typeof(SummonConfig), "summonConfig.", result);
                if (config.summonConfig.statContainer != null)
                {
                    // Source 22: Extract every registered entry inside the template's baseline collection
                    var allTemplateStats = config.summonConfig.statContainer.GetAllStats();
                    foreach (StatValue stat in allTemplateStats)
                    {
                        if (stat != null && !string.IsNullOrWhiteSpace(stat.StatId))
                        {
                            result.Add($"summonConfig.statContainer.{stat.StatId}");
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns all effect-data paths that can receive appended triggered abilities.
    /// Used by the trait drawer to target summon/construct sub-ability sources explicitly.
    /// </summary>
    public static List<string> GetTriggeredAbilityAppendTargets(AbilityDataConfig config)
    {
        var result = new List<string>();
        if (config == null)
            return result;

        if (config.isProjectileAbility && config.projectileConfig?.hitbox != null)
            result.Add("projectileConfig.hitbox.onHitEffects");

        if (config.isAreaAbility && config.areaConfig?.hitbox != null)
            result.Add("areaConfig.hitbox.onHitEffects");

        if (config.isMeleeAbility && config.meleeConfig?.hitbox != null)
            result.Add("meleeConfig.hitbox.onHitEffects");

        if (config.isExplosionAbility && config.explosionConfig?.hitbox != null)
            result.Add("explosionConfig.hitbox.onHitEffects");

        if (config.isBeamAbility && config.beamConfig != null)
            result.Add("beamConfig.onHitEffects");

        if (config.isChanneled && config.channelConfig != null)
            result.Add("channelConfig.onHitEffects");
        if (config.isTrapAbility && config.trapConfig != null)
        {
            if (config.trapConfig.abilityType == TrapAbilityType.Area && config.trapConfig.areaConfig?.hitbox != null)
                result.Add("trapConfig.areaConfig.hitbox.onHitEffects");

            if (config.trapConfig.abilityType == TrapAbilityType.Projectile && config.trapConfig.projectileConfig?.hitbox != null)
                result.Add("trapConfig.projectileConfig.hitbox.onHitEffects");

            if (config.trapConfig.abilityType == TrapAbilityType.Explosion && config.trapConfig.explosionConfig?.hitbox != null)
                result.Add("trapConfig.explosionConfig.hitbox.onHitEffects");
        }

        return result;
    }

    private static void AddFieldsFromType(Type type, string prefix, List<string> result, bool topLevelOnly = false)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            // Skip non-modifiable types.
            // Top-level booleans can still be exposed when explicitly whitelisted
            // (e.g., castAtFeet) and are set via OverrideMode.Set using 1/0.
            if (field.FieldType.IsArray) continue;
            if (field.FieldType.IsGenericType) continue;
            if (field.FieldType == typeof(Vector2) || field.FieldType == typeof(Vector3)) continue;

            // For top-level, only include whitelisted fields
            if (topLevelOnly && !_topLevelModifiableFields.Contains(field.Name)) continue;

            // Include numeric, string, and object reference types
            bool isNumeric = field.FieldType == typeof(float) || field.FieldType == typeof(int);
            bool isString = field.FieldType == typeof(string);
            bool isObject = typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType);
            bool isEnum = field.FieldType.IsEnum;
            bool isBool = field.FieldType == typeof(bool);
            if (isNumeric || isString || isObject || isEnum || isBool)
            {
                result.Add(prefix + field.Name);
            }
        }
    }

    private static void AddCustomPassiveConfigFields(string prefix, List<string> result, Type passiveConfigType)
    {
        if (passiveConfigType == null)
            return;

        foreach (FieldInfo field in EnumerateSerializedInstanceFields(passiveConfigType))
        {
            if (field.DeclaringType == typeof(PassiveAbilityConfigBase))
                continue;

            if (field.FieldType.IsArray) continue;
            if (field.FieldType.IsGenericType) continue;
            if (field.FieldType == typeof(Vector2) || field.FieldType == typeof(Vector3)) continue;

            bool isNumeric = field.FieldType == typeof(float) || field.FieldType == typeof(int);
            bool isString = field.FieldType == typeof(string);
            bool isObject = typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType);
            bool isEnum = field.FieldType.IsEnum;
            bool isBool = field.FieldType == typeof(bool);
            if (isNumeric || isString || isObject || isEnum || isBool)
                result.Add(prefix + field.Name);
        }
    }

    private static void AddEffectDataFields(string prefix, List<string> result, EffectData effectData = null)
    {
        foreach (var field in typeof(EffectData).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType.IsArray || field.FieldType.IsGenericType) continue;
            if (field.FieldType == typeof(Vector2) || field.FieldType == typeof(Vector3)) continue;
            if (field.FieldType.IsEnum) continue;

            bool isBool = field.FieldType == typeof(bool);
            bool isNumeric = field.FieldType == typeof(float) || field.FieldType == typeof(int);
            bool isString = field.FieldType == typeof(string);
            bool isObject = typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType);

            if (isBool || isNumeric || isString || isObject)
            {
                result.Add(prefix + field.Name);
            }
        }

        AddTriggeredAbilityConfigFields(prefix + "triggeredAbilityConfigs", result, effectData);
    }

    private static void AddTriggeredAbilityConfigFields(string arrayPathPrefix, List<string> result, EffectData effectData)
    {
        if (effectData?.triggeredAbilityConfigs == null)
            return;

        for (int i = 0; i < effectData.triggeredAbilityConfigs.Length; i++)
        {
            string entryPrefix = $"{arrayPathPrefix}[{i}].";
            result.Add(entryPrefix + "abilityConfig");
            result.Add(entryPrefix + "triggerChance");
            result.Add(entryPrefix + "triggerTiming");
        }
    }



    /// <summary>
    /// Adds modifiable LifeStealConfig fields to the result list.
    /// Skips <c>type</c> (enum) since the modifier system cannot set enum values.
    /// Exposes <c>enabled</c> (bool, use Set override with value 1/0) and <c>amount</c> (float).
    /// </summary>
    private static void AddLifeStealConfigFields(string prefix, List<string> result)
    {
        result.Add(prefix + "enabled");   // bool — use Set override (1 = true, 0 = false)
        result.Add(prefix + "amount");    // float — Flat / Percent / Set
    }

    /// <summary>
    /// Adds modifiable fields for the shared <see cref="HitboxConfig"/> block plus its nested
    /// modules (knockback, pull, life steal, on-hit effects). Nested paths beyond three segments
    /// still apply correctly at runtime via ApplyAccumulatedToNestedPath.
    /// </summary>
    private static void AddHitboxConfigFields(string prefix, List<string> result, HitboxConfig hitboxConfig)
    {
        result.Add(prefix + "prefab");   // bool — Set override (1/0)
        result.Add(prefix + "damage");
        result.Add(prefix + "damageTypeName");
        result.Add(prefix + "positiveHealing");    // float
        result.Add(prefix + "percentWeaponDamage"); // float (only used when useWeaponDamage)
        result.Add(prefix + "scaleX");     // float
        result.Add(prefix + "scaleY");     // float
        result.Add(prefix + "knockback.enabled");        // bool — Set override (1/0)
        result.Add(prefix + "knockback.force");          // float
        result.Add(prefix + "knockback.directionality"); // float
        result.Add(prefix + "pull.enabled");             // bool — Set override (1/0)
        result.Add(prefix + "pull.force");               // float
        AddLifeStealConfigFields(prefix + "lifeSteal.", result);
        AddEffectDataFields(prefix + "onHitEffects.", result, hitboxConfig?.onHitEffects);
        AddEffectDataFields(prefix + "onHitBuffEffects.", result, hitboxConfig?.onHitBuffEffects);
    }
}