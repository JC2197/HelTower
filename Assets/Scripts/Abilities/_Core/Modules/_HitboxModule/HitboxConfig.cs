using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared configuration for every sub-ability that creates a hitbox
/// (projectiles, melee, area, explosion, auras). Consolidates the common
/// prefab / scale / damage / effect data so each system reuses one block
/// instead of duplicating fields.
/// </summary>
[System.Serializable]
public class HitboxConfig
{
    [Header("Hitbox")]
    [Tooltip("Prefab instantiated for this hitbox. Should contain a Collider2D.")]
    public GameObject prefab;

    [Tooltip("Horizontal scale multiplier applied to the spawned hitbox prefab.")]
    public float scaleX = 1f;

    [Tooltip("Vertical scale multiplier applied to the spawned hitbox prefab.")]
    public float scaleY = 1f;

    [Tooltip("Layers that can be hit by this hitbox.")]
    public LayerMask hitLayers = 6;

    [Header("Damage")]
    [Tooltip("Damage dealt on hit.")]
    public float damage = 20f;

    [Tooltip("Type of damage dealt.")]
    [DamageTypeDropdown]
    public string damageTypeName = "Physical";

    [Tooltip("Use the equipped weapon's damage (and its damage type) instead of the fixed damage/type above.")]
    public bool useWeaponDamage = false;

    [Tooltip("Percentage of weapon damage to deal when Use Weapon Damage is enabled (100 = full weapon damage, 150 = 150%).")]
    public float percentWeaponDamage = 100f;

    [Header("Life Steal")]
    [Tooltip("Heal the ability owner on hit. For summons/constructs, heals the player owner.")]
    public LifeStealConfig lifeSteal = new LifeStealConfig();

    [Header("Knockback")]
    [Tooltip("Knockback applied to hit targets.")]
    public KnockbackModule knockback = new KnockbackModule();

    [Header("Pull")]
    [Tooltip("Pull (vacuum) applied to hit targets.")]
    public PullModule pull = new PullModule();
    public ForcedMovementPreference forcedMovementPreference = ForcedMovementPreference.fromAbilityPosition;
    [Header("On Hit Effects")]
    [Tooltip("Negative on-hit effects (CC/DoT/debuffs) applied to targets in Hit Layers.")]
    public EffectData onHitEffects = new EffectData();

    [Tooltip("If enabled, fire one random Triggered Ability (marked 'triggersOnDestroy') when this hitbox owner is destroyed.")]
    public bool triggerOneTriggeredAbilityOnDestroy = false;

    [Header("Positive Effects")]
    [Tooltip("Layers that can receive positive effects (healing/buffs).")]
    public LayerMask positiveHitLayers = 0;

    [Tooltip("Healing applied to targets in Positive Hit Layers on hit/tick (0 = none).")]
    public float positiveHealing = 0f;

    [Tooltip("Positive on-hit effects (buffs/support effects) applied to targets in Positive Hit Layers.")]
    public EffectData onHitBuffEffects = new EffectData();

    [Header("Visual Effects")]
    [Tooltip("On-hit visual/audio feedback.")]
    public HitFeedbackModule effects = new HitFeedbackModule();

    /// <summary>
    /// Returns a distinct copy of this hitbox so per-instance overrides (weapon effects,
    /// summon damage, prefab swaps) never mutate the shared ability config. Nested modules
    /// are shared by reference — reassign them rather than mutating their internals.
    /// </summary>
    public HitboxConfig Clone()
    {
        var clone = new HitboxConfig();
        foreach (var field in typeof(HitboxConfig).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            field.SetValue(clone, field.GetValue(this));
        }
        return clone;
    }

    // ---------------------------------------------------------------------
    // Reusable hit-processing helpers. Every hitbox system (melee, projectile,
    // area, explosion, aura) should call these instead of duplicating logic.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Apply trait-scaled damage (plus life steal) to a single target.
    /// Returns the final damage dealt (0 if the target is not damageable or no damage is configured).
    /// </summary>
    /// <param name="statAttacker">Whose stats/traits apply to the damage calculation (the player owner for summons).</param>
    /// <param name="damageAttacker">Who is credited as the attacker on the damage event (the summon/construct itself, if any).</param>
    /// <param name="healTarget">Who receives life steal healing (the player owner).</param>
    public float ApplyDamage(Collider2D target, GameObject statAttacker, GameObject damageAttacker, GameObject healTarget,
        Vector3 hitPosition, string abilityName, List<string> abilityTags, AbilityDataConfig parentConfig,
        float baseDamageOverride = -1f, string damageTypeOverride = null)
    {
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return 0f;

        string dmgType;
        float baseDamage = ResolveBaseDamage(statAttacker, out dmgType, baseDamageOverride, damageTypeOverride);

        float dealt = 0f;
        if (baseDamage > 0f)
        {
            DamageContext dc = DamageCalculator.CalculateDamageWithTraitEffects(
                baseDamage, dmgType, abilityName, abilityTags, statAttacker, target.gameObject, hitPosition, parentConfig);
            damageable.TakeDamage(dc.FinalDamage, dmgType, hitPosition, effects.hitFlashColor, damageAttacker, dc.CritMultiplier);

            PlayerController attackerPlayer = statAttacker != null ? statAttacker.GetComponent<PlayerController>() : null;
            attackerPlayer?.NotifyAttackDamage(parentConfig, target.gameObject, dc.FinalDamage, dmgType);

            LifeStealProcessor.Apply(lifeSteal, dc.FinalDamage, healTarget);
            dealt = dc.FinalDamage;
        }

        return dealt;
    }


    /// <summary>
    /// Apply configured positive healing to a single target.
    /// Signature mirrors ApplyDamage so abilities can route mixed effect pipelines consistently.
    /// Returns healing applied (0 if no healing or target is not damageable).
    /// </summary>
    public float ApplyHealing(Collider2D target, GameObject statAttacker, GameObject damageAttacker, GameObject healTarget,
        Vector3 hitPosition, string abilityName, List<string> abilityTags, AbilityDataConfig parentConfig,
        float healingOverride = -1f, string damageTypeOverride = null)
    {
        if (target == null)
            return 0f;

        float healAmount = healingOverride >= 0f ? healingOverride : positiveHealing;
        if (healAmount <= 0f)
            return 0f;

        // Healing must bypass damage/block/armor logic. Using negative TakeDamage causes
        // Organism to classify the value as blocked damage and ignore the heal.
        Organism organism = target.GetComponentInParent<Organism>();
        if (organism == null)
            return 0f;

        organism.Heal(healAmount);
        return healAmount;
    }
    

    /// <summary>
    /// Resolves the base damage and damage type for a hit, honoring <see cref="useWeaponDamage"/>.
    /// When Use Weapon Damage is enabled the equipped weapon's damage AND damage type are used
    /// (scaled by <see cref="percentWeaponDamage"/>); otherwise the configured fixed values (or the
    /// supplied overrides) are used.
    /// </summary>
    public float ResolveBaseDamage(GameObject statAttacker, out string dmgType, float baseDamageOverride = -1f, string damageTypeOverride = null)
    {
        dmgType = string.IsNullOrEmpty(damageTypeOverride) ? damageTypeName : damageTypeOverride;
        float baseDamage = baseDamageOverride >= 0f ? baseDamageOverride : damage;

        if (!useWeaponDamage || statAttacker == null)
            return baseDamage;

        float scale = percentWeaponDamage / 100f;

        // Read damage from the player's class-defined WeaponConfig.
        WeaponConfig weapon = PlayerUtil.GetWeapon(statAttacker);
        if (weapon != null)
        {
            baseDamage = UnityEngine.Random.Range(weapon.weaponDamageMin, weapon.weaponDamageMax + 1) * scale;
            if (!string.IsNullOrEmpty(weapon.weaponDamageType))
                dmgType = weapon.weaponDamageType;
        }

        return baseDamage;
    }

    /// <summary>Apply the configured on-hit status effects to a target.</summary>
    public void ApplyOnHitEffects(GameObject target, GameObject source, GameObject triggerOwner)
    {
        onHitEffects?.ApplyEffects(target, source, triggerOwner);
    }

    /// <summary>
    /// Shared destroy hook for hitbox-based abilities.
    /// Routes destroy-timed triggered ability procs through the same EffectData trigger system.
    /// </summary>
    public void OnDestroy(GameObject source, GameObject triggerOwner)
    {
        ApplyOnDestroyTriggeredAbility(source, source, triggerOwner);
    }

    public void ApplyOnDestroyTriggeredAbility(GameObject target, GameObject source, GameObject triggerOwner)
    {
        if (onHitEffects == null)
            return;

        bool hasDestroyTimedTrigger = onHitEffects.HasTriggeredAbilitiesForTiming(TriggeredAbilityTriggerTiming.OnDestroy);
        if (!triggerOneTriggeredAbilityOnDestroy && !hasDestroyTimedTrigger)
            return;

        onHitEffects.TryTriggerAbilities(
            TriggeredAbilityTriggerTiming.OnDestroy,
            target,
            source,
            triggerOwner,
            singleRandom: true);
    }

    /// <summary>Apply configured positive buff/support effects to a target.</summary>
    public void ApplyBuffEffects(GameObject target, GameObject source, GameObject triggerOwner)
    {
        onHitBuffEffects?.ApplyEffects(target, source, triggerOwner);
    }

    /// <summary>True if the target is in negative-effect hit layers.</summary>
    public bool IsNegativeTarget(GameObject target)
    {
        if (target == null)
            return false;
        return ((1 << target.layer) & hitLayers.value) != 0;
    }

    /// <summary>True if the target is in positive-effect hit layers.</summary>
    public bool IsPositiveTarget(GameObject target)
    {
        if (target == null)
            return false;
        return ((1 << target.layer) & positiveHitLayers.value) != 0;
    }

    /// <summary>Combined target mask used when an ability wants both negative and positive targets.</summary>
    public LayerMask GetCombinedHitLayers()
    {
        return hitLayers | positiveHitLayers;
    }

    /// <summary>
    /// Apply knockback to a target, blending between "away from attacker" and the supplied
    /// preferred direction based on <see cref="KnockbackModule.directionality"/>.
    /// </summary>
    public void ApplyKnockback(Collider2D target,  GameObject attacker, Vector2 abilityPosition)
    {
        
        Vector2 preferredDirection = Vector2.zero;
        if (knockback == null || !knockback.enabled || target == null)
            return;

        if (forcedMovementPreference == ForcedMovementPreference.fromAbilityPosition)
            preferredDirection = ((Vector2)target.transform.position - abilityPosition).normalized;
        else if (forcedMovementPreference == ForcedMovementPreference.fromPlayerPosition)
            preferredDirection = ((Vector2)target.transform.position - (Vector2)attacker.transform.position).normalized;

        Vector2 knockbackDir = preferredDirection;
        if (attacker != null)
        {
            Vector2 toTarget = ((Vector2)target.transform.position - (Vector2)attacker.transform.position).normalized;
            knockbackDir = Vector2.Lerp(toTarget, preferredDirection, knockback.directionality);
        }
        Enemy enemy = target.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.ApplyKnockback(knockbackDir * knockback.force);
            return;
        }

        Rigidbody2D targetRb = target.attachedRigidbody;
        if (targetRb == null)
            targetRb = target.GetComponentInParent<Rigidbody2D>();
        if (targetRb != null && targetRb.bodyType == RigidbodyType2D.Dynamic)
            targetRb.AddForce(knockbackDir * knockback.force, ForceMode2D.Impulse);
    }

    /// <summary>Pull a target toward the supplied origin.</summary>
    public void ApplyPull(Collider2D target, Vector3 origin)
    {
        if (pull == null || !pull.enabled || target == null)
            return;

        Rigidbody2D targetRb = target.attachedRigidbody;
        if (targetRb == null)
            targetRb = target.GetComponentInParent<Rigidbody2D>();
        if (targetRb == null || targetRb.bodyType != RigidbodyType2D.Dynamic)
            return;

        Vector2 dir = (Vector2)origin - (Vector2)target.transform.position;
        if (dir.sqrMagnitude <= 0.0001f)
            return;
        targetRb.AddForce(dir.normalized * pull.force, ForceMode2D.Impulse);
    }

    /// <summary>Spawn on-hit visual/audio feedback plus the centralized ability hit visual.</summary>
    public void SpawnHitFeedback(Vector3 position, AbilityDataConfig parentConfig, Collider2D hit = null)
    {
        if (effects != null && effects.hitEffectPrefab != null)
        {
            GameObject fx = Object.Instantiate(effects.hitEffectPrefab, position, Quaternion.identity);
            Object.Destroy(fx, 2f);
        }
        if (effects != null && effects.hitSound != null)
            AudioManager.Instance.PlaySpatialSound(effects.hitSound, position, 1f, Random.Range(0.9f, 1.1f));

        if (hit != null)
            HitVisualHelper.SpawnHitVisual(parentConfig, position, hit);
        else
            HitVisualHelper.SpawnHitVisual(parentConfig, position);
    }

    public enum ForcedMovementPreference
    {
        fromAbilityPosition,
        fromPlayerPosition
    }
}
