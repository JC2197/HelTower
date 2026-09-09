using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum TriggeredAbilityTriggerTiming
{
    OnHit,
    OnDestroy,
    Both
}

/// <summary>
/// One entry in an <see cref="EffectData"/> list: which EffectConfig asset to apply and the
/// per-ability overrides for it. The asset decides what the effect does; this decides how it lands.
/// </summary>
[Serializable]
public class EffectApplication
{
    [Tooltip("Effect asset to apply. Its own type decides the behaviour (DoT, stat buff, root, stun...).")]
    public EffectConfig effect;

    [Tooltip("Chance to apply (0-1).")]
    [Range(0f, 1f)]
    public float applicationChance = 1f;

    [Tooltip("Seconds to override the effect asset's duration. 0 = use the asset's own duration.")]
    [Min(0f)]
    public float durationOverride = 0f;

    [Tooltip("Damage per tick override for damage-over-time effects. 0 = use the asset's own damage.")]
    [Min(0f)]
    public float damageOverride = 0f;

    /// <summary>Resolves the asset plus overrides into the instance that should be applied.</summary>
    public EffectConfig Resolve()
    {
        if (effect == null)
            return null;

        if (effect is DamageOverTimeConfig dot && (damageOverride > 0f || durationOverride > 0f))
        {
            float damage = damageOverride > 0f ? damageOverride : dot.damagePerTick;
            float duration = durationOverride > 0f ? durationOverride : dot.duration;
            return dot.WithDamageAndDuration(damage, duration);
        }

        return durationOverride > 0f ? effect.WithDuration(durationOverride) : effect;
    }
}

[Serializable]
public class TriggeredAbilityConfig
{
    [Tooltip("The ability to trigger. Supports Explosion and Standalone Projectile types.")]
    public AbilityDataConfig abilityConfig;

    [Tooltip("Chance to trigger the ability (0 = never, 1 = always)")]
    [Range(0f, 1f)]
    public float triggerChance = 1f;

    [Tooltip("When this triggered ability should fire.")]
    public TriggeredAbilityTriggerTiming triggerTiming = TriggeredAbilityTriggerTiming.OnHit;

    public bool TriggersOnDestroy => triggerTiming == TriggeredAbilityTriggerTiming.OnDestroy || triggerTiming == TriggeredAbilityTriggerTiming.Both;
    public bool TriggersOnHit => triggerTiming == TriggeredAbilityTriggerTiming.OnHit || triggerTiming == TriggeredAbilityTriggerTiming.Both;
}

/// <summary>
/// A set of effects and ability procs applied together (on hit, on destroy, on enter...).
/// Effects are an open list of EffectConfig assets — add a "Burning" DoT asset to burn, add a
/// stat buff asset to buff. No per-status fields, no enabling booleans.
/// </summary>
[Serializable]
public class EffectData
{
    [Tooltip("Effects to apply. Each entry references an effect asset plus its per-ability overrides.")]
    [NonReorderable]
    public List<EffectApplication> effects = new List<EffectApplication>();

    [Tooltip("Abilities to trigger when this effect set fires.")]
    [NonReorderable]
    public List<TriggeredAbilityConfig> triggeredAbilities = new List<TriggeredAbilityConfig>();

    /// <summary>
    /// Applies every configured effect and fires on-hit ability procs. <paramref name="owner"/> is
    /// the caster, so procs are attributed correctly; the multipliers forward the parent's scaling.
    /// </summary>
    public void ApplyEffects(GameObject target, GameObject source, GameObject owner, float damageMultiplier = 1f, float sizeMultiplier = 1f)
    {
        ApplyEffects(target, source);
        TryTriggerAbilities(TriggeredAbilityTriggerTiming.OnHit, target, source, owner, damageMultiplier, sizeMultiplier);
    }

    public void ApplyEffects(GameObject target, GameObject source)
    {
        if (target == null || effects == null || effects.Count == 0)
            return;

        EffectManager effectManager = target.GetComponentInParent<EffectManager>();
        if (effectManager == null)
            effectManager = target.GetComponentInChildren<EffectManager>();

        if (effectManager == null)
        {
            Debug.LogWarning($"[EffectData] Cannot apply effects to {target.name} — no EffectManager component found.");
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            EffectApplication application = effects[i];
            if (application == null || application.effect == null)
                continue;

            if (UnityEngine.Random.value > application.applicationChance)
                continue;

            EffectConfig resolved = application.Resolve();
            if (resolved != null)
                effectManager.ApplyEffect(resolved, source);
        }
    }

    /// <summary>
    /// Executes configured ability procs for a specific timing. When <paramref name="singleRandom"/>
    /// is true, one random eligible entry is selected (matches hitbox OnDestroy behaviour).
    /// </summary>
    public void TryTriggerAbilities(
        TriggeredAbilityTriggerTiming timing,
        GameObject target,
        GameObject source,
        GameObject owner,
        float damageMultiplier = 1f,
        float sizeMultiplier = 1f,
        bool singleRandom = false)
    {
        if (triggeredAbilities == null || triggeredAbilities.Count == 0)
            return;

        List<TriggeredAbilityConfig> candidates = new List<TriggeredAbilityConfig>();
        foreach (TriggeredAbilityConfig config in triggeredAbilities)
        {
            if (config == null || config.abilityConfig == null)
                continue;

            bool matches = timing == TriggeredAbilityTriggerTiming.OnDestroy
                ? config.TriggersOnDestroy
                : config.TriggersOnHit;

            if (matches)
                candidates.Add(config);
        }

        if (candidates.Count == 0)
            return;

        Vector3 spawnPos = target != null
            ? target.transform.position
            : (source != null ? source.transform.position : Vector3.zero);

        GameObject triggerOwner = owner ?? source;
        if (triggerOwner == null)
            return;

        if (singleRandom)
        {
            TriggeredAbilityConfig selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (UnityEngine.Random.value <= selected.triggerChance)
                OnHitAbilitySpawner.Trigger(selected.abilityConfig, triggerOwner, spawnPos, damageMultiplier, sizeMultiplier);

            return;
        }

        foreach (TriggeredAbilityConfig config in candidates)
        {
            if (UnityEngine.Random.value <= config.triggerChance)
                OnHitAbilitySpawner.Trigger(config.abilityConfig, triggerOwner, spawnPos, damageMultiplier, sizeMultiplier);
        }
    }

    public bool HasTriggeredAbilitiesForTiming(TriggeredAbilityTriggerTiming timing)
    {
        if (triggeredAbilities == null || triggeredAbilities.Count == 0)
            return false;

        foreach (TriggeredAbilityConfig config in triggeredAbilities)
        {
            if (config == null || config.abilityConfig == null)
                continue;

            bool matches = timing == TriggeredAbilityTriggerTiming.OnDestroy
                ? config.TriggersOnDestroy
                : config.TriggersOnHit;

            if (matches)
                return true;
        }

        return false;
    }

    /// <summary>Appends an effect at runtime — the "add an effect" counterpart to ability procs.</summary>
    public void AddEffect(EffectConfig effect, float applicationChance = 1f, float durationOverride = 0f, float damageOverride = 0f)
    {
        if (effect == null)
            return;

        effects ??= new List<EffectApplication>();
        effects.Add(new EffectApplication
        {
            effect = effect,
            applicationChance = Mathf.Clamp01(applicationChance),
            durationOverride = Mathf.Max(0f, durationOverride),
            damageOverride = Mathf.Max(0f, damageOverride)
        });
    }

    public void AddTriggeredAbility(AbilityDataConfig ability, float triggerChance, TriggeredAbilityTriggerTiming timing)
    {
        if (ability == null)
            return;

        triggeredAbilities ??= new List<TriggeredAbilityConfig>();
        triggeredAbilities.Add(new TriggeredAbilityConfig
        {
            abilityConfig = ability,
            triggerChance = Mathf.Clamp01(triggerChance),
            triggerTiming = timing
        });
    }

    /// <summary>Deep-copies this set so runtime modifiers can mutate a per-cast instance.</summary>
    public EffectData Clone()
    {
        EffectData copy = new EffectData();

        foreach (EffectApplication application in effects)
        {
            if (application == null)
                continue;

            copy.effects.Add(new EffectApplication
            {
                effect = application.effect,
                applicationChance = application.applicationChance,
                durationOverride = application.durationOverride,
                damageOverride = application.damageOverride
            });
        }

        foreach (TriggeredAbilityConfig trigger in triggeredAbilities)
        {
            if (trigger == null)
                continue;

            copy.triggeredAbilities.Add(new TriggeredAbilityConfig
            {
                abilityConfig = trigger.abilityConfig,
                triggerChance = trigger.triggerChance,
                triggerTiming = trigger.triggerTiming
            });
        }

        return copy;
    }
}
