using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration for hold-to-charge mechanics with overcharge bars and per-bar field modifiers.
/// 
/// Attach this to an AbilityDataConfig that has activateOnButtonRelease enabled.
///
/// How it works:
///   1. When the ability fires (after precast), the charge bar begins filling.
///   2. Each bar takes barDuration seconds.
///   3. The player holds the button to fill additional bars (up to maxBars).
///   4. On release, the charge level (1.0 = 1 bar, 2.0 = 2 bars, etc.) is stored.
///   5. Each ChargeBarModifier scales its valuePerBar by chargeLevel and applies
///      to the target field using the same dot-notation path as AbilityConfigModifier
///      (e.g. "projectileConfig.percentWeaponDamage", "meleeConfig.damage").
///   6. Only OverrideMode.Flat and OverrideMode.Percent are supported (not Set).
/// </summary>
[Serializable]
public class HoldChargeConfig
{
    [Tooltip("Duration of each charge bar in seconds. Overrides the precast animation length as the charge timer (animation still plays visually). Each overcharge bar also takes this long.")]
    [Min(0.05f)]
    public float barDuration = 1f;

    [Tooltip("Total number of charge bars that can be filled. 1 = standard single-bar charge. Each bar beyond the first fills during the hold-animation phase.")]
    [Min(1)]
    public int maxBars = 1;

    [NonReorderable]
    [Tooltip("Field modifiers applied at fire time, scaled by the number of bars charged at release. Uses the same dot-notation paths as trait ability modifiers (e.g. 'projectileConfig.percentWeaponDamage', 'projectileConfig.projectileCount', 'meleeConfig.damage').")]
    public List<ChargeBarModifier> modifiers = new List<ChargeBarModifier>();
}

/// <summary>
/// A single field modification driven by charge level (0.0 to HoldChargeConfig.maxBars).
///
/// Examples:
///   • Flat +pierce per bar:  propertyPath="projectileConfig.pierceCount",  overrideMode=Flat,    valuePerBar=1
///   • +50% damage per bar:   propertyPath="projectileConfig.percentWeaponDamage", overrideMode=Percent, valuePerBar=50
///   • +1 projectile per bar: propertyPath="projectileConfig.projectileCount", overrideMode=Flat, valuePerBar=1
/// </summary>
[Serializable]
public class ChargeBarModifier
{
    public enum AbilityType
    {
        Melee,
        Projectile,
    }

    [Tooltip("Dot-notation field path — same format as trait ability modifiers.\nExamples: 'projectileConfig.hitbox.damage', 'projectileConfig.hitbox.percentWeaponDamage', 'projectileConfig.pierceCount', 'meleeConfig.hitbox.damage', 'meleeConfig.hitbox.percentWeaponDamage'")]
    public string propertyPath = "";

    public AbilityType abilityType = AbilityType.Projectile;
    //public AbilityPropertyOverride abilityPropertyOverride = new AbilityPropertyOverride();

    [Tooltip("Flat: adds (chargeLevel × valuePerBar) directly to the field.\nPercent: multiplies field by (1 + chargeLevel × valuePerBar / 100).")]
    public OverrideMode overrideMode = OverrideMode.Flat;

    [Tooltip("Amount applied per fully-charged bar. E.g. 50 with Percent mode = +50% per bar. Scales linearly with chargeLevel.")]
    public float valuePerBar = 50f;

    [Tooltip("If true, partial bar progress contributes proportionally (e.g. half a bar = half valuePerBar). If false, only fully completed bars count.")]
    public bool allowFractional = true;
}

