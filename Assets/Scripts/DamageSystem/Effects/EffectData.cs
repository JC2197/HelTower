using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic effect data configuration usable in projectiles, areas, and other game systems.
/// Uses EffectConfig ScriptableObject references for behavior, with inline values for damage/duration/chance.
/// </summary>
[Serializable]
public enum TriggeredAbilityTriggerTiming
{
    OnHit,
    OnDestroy,
    Both
}

[Serializable]
public class EffectData
{
    [Serializable]
    public class StatBuffApplication
    {
        [Tooltip("Stat buff effect configuration (ScriptableObject asset)")]
        public StatBuffEffect statBuffEffect;

        [Tooltip("Duration override (0 = use effect default)")]
        public float durationOverride = 0f;

        [Tooltip("Application chance (0-1)")]
        [Range(0f, 1f)]
        public float applicationChance = 1f;
    }

    [Serializable]
    public class TriggeredAbilityConfig : ISerializationCallbackReceiver
    {
        [Tooltip("The ability to trigger when this effect fires. Supports Explosion and Standalone Projectile types.")]
        public AbilityDataConfig abilityConfig;

        [Tooltip("Chance to trigger the ability (0 = never, 1 = always)")]
        [Range(0f, 1f)]
        public float triggerChance = 1f;

        [Tooltip("When this triggered ability should fire.")]
        public TriggeredAbilityTriggerTiming triggerTiming = TriggeredAbilityTriggerTiming.OnHit;

        [HideInInspector]
        [SerializeField]
        private bool legacyTriggersOnDestroy = false;

        public bool TriggersOnDestroy => triggerTiming == TriggeredAbilityTriggerTiming.OnDestroy || triggerTiming == TriggeredAbilityTriggerTiming.Both;
        public bool TriggersOnHit => triggerTiming == TriggeredAbilityTriggerTiming.OnHit || triggerTiming == TriggeredAbilityTriggerTiming.Both;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (legacyTriggersOnDestroy && triggerTiming == TriggeredAbilityTriggerTiming.OnHit)
                triggerTiming = TriggeredAbilityTriggerTiming.OnDestroy;
        }
    }

    [Header("Crowd Control")]
    [Tooltip("Apply a root effect?")]
    public bool canRoot = false;
    [Tooltip("Root effect configuration (ScriptableObject asset)")]
    public RootEffect rootEffect;
    [Tooltip("Duration override (0 = use effect default)")]
    public float rootDuration = 0f;
    [Tooltip("Application chance (0-1)")]
    [Range(0f, 1f)]
    public float rootApplicationChance = 1f;
    
    [Tooltip("Apply a slow effect?")]
    public bool canSlow = false;
    [Tooltip("Slow effect configuration (ScriptableObject asset)")]
    public EffectConfig slowEffect;
    [Tooltip("Duration override (0 = use effect default)")]
    public float slowDuration = 0f;
    [Tooltip("Application chance (0-1)")]
    [Range(0f, 1f)]
    public float slowApplicationChance = 1f;
    
    [Tooltip("Apply a stun effect?")]
    public bool canStun = false;
    [Tooltip("Stun effect configuration (ScriptableObject asset)")]
    public EffectConfig stunEffect;
    [Tooltip("Duration override (0 = use effect default)")]
    public float stunDuration = 0f;
    [Tooltip("Application chance (0-1)")]
    [Range(0f, 1f)]
    public float stunApplicationChance = 1f;
    
    [Header("Damage Over Time")]
    [Tooltip("Apply a bleed effect?")]
    public bool canBleed = false;
    [Tooltip("Bleed effect configuration (ScriptableObject asset)")]
    public BleedEffect bleedEffect;
    [Tooltip("Damage per tick")]
    public float bleedDamage = 5f;
    [Tooltip("Duration in seconds")]
    public float bleedDuration = 3f;
    [Tooltip("Application chance (0-1)")]
    [Range(0f, 1f)]
    public float bleedApplicationChance = 1f;
    
    [Tooltip("Apply a burning effect?")]
    public bool canBurn = false;
    [Tooltip("Burning effect configuration (ScriptableObject asset)")]
    public BurningEffect burnEffect;
    [Tooltip("Damage per tick")]
    public float burnDamage = 10f;
    [Tooltip("Duration in seconds")]
    public float burnDuration = 3f;
    [Tooltip("Application chance (0-1)")]
    [Range(0f, 1f)]
    public float burnApplicationChance = 1f;
    
    [Tooltip("Apply a poison effect?")]
    public bool canPoison = false;
    [Tooltip("Poison effect configuration (ScriptableObject asset)")]
    public PoisonEffect poisonEffect;
    [Tooltip("Damage per tick")]
    public float poisonDamage = 3f;
    [Tooltip("Duration in seconds")]
    public float poisonDuration = 5f;
    [Tooltip("Application chance (0-1)")]
    [Range(0f, 1f)]
    public float poisonApplicationChance = 1f;

    [Header("Stat Buffs")]
    [Tooltip("Apply one or more temporary stat buffs/debuffs on hit?")]
    public bool canApplyStatBuffs = false;

    [Tooltip("Array of stat buff effects to try applying on hit.")]
    public StatBuffApplication[] statBuffApplications = Array.Empty<StatBuffApplication>();
    
    [Header("Triggered Abilities")]
    [Tooltip("Trigger another ability on hit?")]
    public bool canTriggerAbility = false;
    [NonReorderable]
    [Tooltip("The abilities to trigger when this effect fires. Supports Explosion and Standalone Projectile types.")]
    public TriggeredAbilityConfig[] triggeredAbilityConfigs = Array.Empty<TriggeredAbilityConfig>();
    
    /// <summary>
    /// Apply all enabled effects to a target with inline configuration values.
    /// Also triggers a secondary ability if configured. Pass <paramref name="owner"/>
    /// (the caster/attacker) so the triggered ability is attributed correctly.
    /// <paramref name="damageMultiplier"/> and <paramref name="sizeMultiplier"/> are forwarded
    /// to any triggered ability so it inherits the parent's crit/size scaling.
    /// </summary>
    public void ApplyEffects(GameObject target, GameObject source, GameObject owner, float damageMultiplier = 1f, float sizeMultiplier = 1f)
    {
        ApplyEffects(target, source);
        TryTriggerAbilities(TriggeredAbilityTriggerTiming.OnHit, target, source, owner, damageMultiplier, sizeMultiplier);
    }

    /// <summary>
    /// Executes configured triggered abilities for a specific timing.
    /// When <paramref name="singleRandom"/> is true, one random eligible entry is selected,
    /// matching the destroy-trigger behavior used by hitbox OnDestroy hooks.
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
        if (!canTriggerAbility || triggeredAbilityConfigs == null || triggeredAbilityConfigs.Length == 0)
            return;

        List<TriggeredAbilityConfig> candidates = new List<TriggeredAbilityConfig>();
        foreach (var config in triggeredAbilityConfigs)
        {
            if (config == null || config.abilityConfig == null)
                continue;

            bool matches = timing == TriggeredAbilityTriggerTiming.OnDestroy
                ? config.TriggersOnDestroy
                : config.TriggersOnHit;

            if (!matches)
                continue;

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
            {
                OnHitAbilitySpawner.Trigger(selected.abilityConfig, triggerOwner, spawnPos, damageMultiplier, sizeMultiplier);
            }
            return;
        }

        foreach (TriggeredAbilityConfig config in candidates)
        {
            if (UnityEngine.Random.value <= config.triggerChance)
            {
                OnHitAbilitySpawner.Trigger(config.abilityConfig, triggerOwner, spawnPos, damageMultiplier, sizeMultiplier);
            }
        }
    }

    public bool HasTriggeredAbilitiesForTiming(TriggeredAbilityTriggerTiming timing)
    {
        if (!canTriggerAbility || triggeredAbilityConfigs == null || triggeredAbilityConfigs.Length == 0)
            return false;

        foreach (TriggeredAbilityConfig config in triggeredAbilityConfigs)
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

    /// <summary>
    /// Apply all enabled effects to a target with inline configuration values
    /// </summary>
    public void ApplyEffects(GameObject target, GameObject source)
    {
        EffectManager effectManager = target.GetComponentInParent<EffectManager>();
        if (effectManager == null)
        {
            effectManager = target.GetComponent<EffectManager>();
        }
        if (effectManager == null)
        {
            effectManager = target.GetComponentInChildren<EffectManager>();
        }
        if (effectManager == null)
        {
            Debug.LogWarning($"Cannot apply effects to {target.name} - no EffectManager component found!");
            return;
        }
        
        Debug.Log($"[EffectData] Attempting to apply effects to {target.name}. canBleed={canBleed}, bleedEffect={bleedEffect != null}, bleedApplicationChance={bleedApplicationChance}");
        
        // Crowd Control
        if (canRoot && rootEffect != null && UnityEngine.Random.value <= rootApplicationChance)
        {
            EffectConfig effect = rootDuration > 0 ? rootEffect.WithDuration(rootDuration) : rootEffect;
            effectManager.ApplyEffect(effect, source);
        }
            
        if (canSlow && slowEffect != null && UnityEngine.Random.value <= slowApplicationChance)
        {
            EffectConfig effect = slowDuration > 0 ? slowEffect.WithDuration(slowDuration) : slowEffect;
            effectManager.ApplyEffect(effect, source);
        }
            
        if (canStun && stunEffect != null && UnityEngine.Random.value <= stunApplicationChance)
        {
            EffectConfig effect = stunDuration > 0 ? stunEffect.WithDuration(stunDuration) : stunEffect;
            effectManager.ApplyEffect(effect, source);
        }
        
        // Damage Over Time
        if (canBleed && bleedEffect != null && UnityEngine.Random.value <= bleedApplicationChance)
        {
            Debug.Log($"[EffectData] Applying bleed effect! Damage={bleedDamage}, Duration={bleedDuration}");
            DamageOverTimeConfig effect = bleedEffect.WithDamageAndDuration(bleedDamage, bleedDuration);
            effectManager.ApplyEffect(effect, source);
        }
        else if (canBleed)
        {
            Debug.LogWarning($"[EffectData] Bleed NOT applied. bleedEffect null? {bleedEffect == null}, failed chance? {UnityEngine.Random.value > bleedApplicationChance}");
        }
            
        if (canBurn && burnEffect != null && UnityEngine.Random.value <= burnApplicationChance)
        {
            DamageOverTimeConfig effect = burnEffect.WithDamageAndDuration(burnDamage, burnDuration);
            effectManager.ApplyEffect(effect, source);
        }
            
        if (canPoison && poisonEffect != null && UnityEngine.Random.value <= poisonApplicationChance)
        {
            DamageOverTimeConfig effect = poisonEffect.WithDamageAndDuration(poisonDamage, poisonDuration);
            effectManager.ApplyEffect(effect, source);
        }

        // Stat Buffs
        if (canApplyStatBuffs && statBuffApplications != null)
        {
            foreach (StatBuffApplication app in statBuffApplications)
            {
                if (app == null || app.statBuffEffect == null)
                    continue;

                if (UnityEngine.Random.value > app.applicationChance)
                    continue;

                EffectConfig effect = app.durationOverride > 0f
                    ? app.statBuffEffect.WithDuration(app.durationOverride)
                    : app.statBuffEffect;

                effectManager.ApplyEffect(effect, source);
            }
        }
    }
}
