using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
/// <summary>
/// ScriptableObject that defines a weapon's configuration.
/// Contains the weapon prefab and all positioning/sorting/behavior settings.
/// </summary>
[CreateAssetMenu(fileName = "Weapon_", menuName = "Items/Weapons/Weapon Config")]
public class WeaponConfig : ScriptableObject
{
    [FormerlySerializedAs("baseTierAvailable")]
    [Tooltip("Advancement level for this weapon (1-6). Used as the minimum rolled gear tier.")]
    [Range(1, 6)]
    public int advancementLevel = 1;

    [Tooltip("Display name of the weapon")]
    public string weaponName = "New Weapon";

    [Tooltip("Weapon type category for ability requirements (from WeaponTypeList)")]
    [WeaponTypeDropdown]
    public string weaponType = "Pistol";

    [Header("Trait Grant")]
    [Tooltip("Trait granted when this weapon is equipped (optional)")]
    public TraitData grantedTrait;

    // craftingCost and researchPointCost are inherited from CraftableConfig

    [Header("Weapon Prefab")]
    [Tooltip("Weapon prefab to spawn")]
    public GameObject weaponPrefab;

    [Header("Weapon Damage")]
    [Tooltip("Minimum damage for Tier I (base tier) - will be scaled by tier multiplier")]
    public int weaponDamageMin;
    [Tooltip("Maximum damage for Tier I (base tier) - will be scaled by tier multiplier")]
    public int weaponDamageMax;

    [Tooltip("Damage type for this weapon (affects resistances/weaknesses)")]
    [DamageTypeDropdown]
    public string weaponDamageType = "Physical";

    [Tooltip("Optional: Offhand weapon config for dual-wielding systems (e.g., left glove for boxing gloves). Leave empty for single weapons.")]
    public WeaponConfig offhandWeaponConfig;

    [Tooltip("Sprite shown in inventory when picked up")]
    public Sprite inventorySprite;

    [Tooltip("Sprite shown on ground as world item")]
    public Sprite worldSprite;

    // treeSprite, treeSpriteColorTag, craftingCost, and researchPointCost are inherited from CraftableConfig

    [Header("Positioning")]
    [Tooltip("Override the weapon type's default positioning with custom values for this weapon")]
    public bool overridePositioning = false;

    [Tooltip("Custom positioning data (only used when overridePositioning is true)")]
    public WeaponPositioningData positioningOverride = new WeaponPositioningData();

    /// <summary>
    /// Resolved positioning: returns override if enabled, otherwise falls back to the WeaponTypeConfig default.
    /// If no WeaponTypeConfig exists for this weapon type, returns the local override data.
    /// </summary>
    public WeaponPositioningData Positioning
    {
        get
        {
            if (overridePositioning)
                return positioningOverride;

            WeaponTypeConfig typeConfig = null;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                typeConfig = WeaponTypeConfig.EditorGetConfigForType(weaponType);
            else
#endif
                typeConfig = WeaponTypeConfig.GetConfigForType(weaponType);

            if (typeConfig != null)
                return typeConfig.defaultPositioning;

            return positioningOverride;
        }
    }

    // Convenience accessors so existing code (CanEquipToSlot, inventory, etc.) still compiles
    public float aimingRadius => Positioning.aimingRadius;
    public bool isMainHand => Positioning.isMainHand;
    public bool is2Handed => Positioning.is2Handed;
    public bool isOffhand => Positioning.isOffhand;

    public Vector2 northEastOffset => Positioning.northEastOffset;
    public Vector2 northWestOffset => Positioning.northWestOffset;
    public Vector2 southEastOffset => Positioning.southEastOffset;
    public Vector2 southWestOffset => Positioning.southWestOffset;
    public bool weaponBehindOnNE => Positioning.weaponBehindOnNE;
    public bool weaponBehindOnNW => Positioning.weaponBehindOnNW;
    public bool weaponBehindOnSE => Positioning.weaponBehindOnSE;
    public bool weaponBehindOnSW => Positioning.weaponBehindOnSW;

    // Offhand-specific accessors
    public Vector2 offhandNorthEastOffset => Positioning.offhandNorthEastOffset;
    public Vector2 offhandNorthWestOffset => Positioning.offhandNorthWestOffset;
    public Vector2 offhandSouthEastOffset => Positioning.offhandSouthEastOffset;
    public Vector2 offhandSouthWestOffset => Positioning.offhandSouthWestOffset;
    public bool offhandWeaponBehindOnNE => Positioning.offhandWeaponBehindOnNE;
    public bool offhandWeaponBehindOnNW => Positioning.offhandWeaponBehindOnNW;
    public bool offhandWeaponBehindOnSE => Positioning.offhandWeaponBehindOnSE;
    public bool offhandWeaponBehindOnSW => Positioning.offhandWeaponBehindOnSW;


    public bool lockTo2Directions => Positioning.lockTo2Directions;
    public bool flipWeaponOnTurn => Positioning.flipWeaponOnTurn;
    public bool flipWeaponOnYAxis => Positioning.flipWeaponOnYAxis;
    public bool flipWeaponOnXAxis => Positioning.flipWeaponOnXAxis;
    
    
    public bool handBehindOnNE => Positioning.handBehindOnNE;
    public bool handBehindOnNW => Positioning.handBehindOnNW;
    public bool handBehindOnSE => Positioning.handBehindOnSE;
    public bool handBehindOnSW => Positioning.handBehindOnSW;
    public float handRotationOffset => Positioning.handRotationOffset;
    [Tooltip("Ability that this weapon grants when equipped (e.g., Plasma Beam for Staff). Leave empty if weapon grants no ability.")]
    public AbilityConfig grantedPrimaryAbility;
    public AbilityConfig grantedPassiveAbility;

    [Header("Projectile System")]
    [Tooltip("Optional: Override projectile prefab for all abilities when this weapon is equipped. LaunchZone-specific overrides take priority. Leave empty to use ability's projectile.")]
    public GameObject projectilePrefabOverride;

    [Header("Ammo System")]
    [Tooltip("Does this weapon use an ammo/magazine system?")]
    public bool usesAmmo = false;
    [Tooltip("Ammo configuration for this weapon (only used if usesAmmo is true)")]
    public AmmoConfig ammoConfig = new AmmoConfig();


    [Tooltip("Override muzzle flash particle effect for projectile abilities when this weapon is equipped")]
    public ParticleSystem muzzleFlashOverride;

    [Tooltip("Override muzzle flash light settings")]
    public bool overrideMuzzleLight = false;
    public Color muzzleLightColorOverride = Color.yellow;
    public float muzzleLightIntensityOverride = 3f;
    public float muzzleLightRangeOverride = 2f;
    public float muzzleLightDurationOverride = 0.1f;

    [Tooltip("Override hit effect (visual effects when projectile hits an enemy)")]
    public bool overrideHitEffects = false;
    public GameObject hitVisualPrefabOverride;
    public AudioClip hitSoundOverride;
    public Color hitFlashColorOverride = Color.white;

    [Tooltip("Override status effects applied by projectiles from this weapon")]
    public bool overrideStatusEffects = false;
    public EffectData onHitEffectsOverride = new EffectData();



    /// <summary>
    /// Returns true when this weapon is allowed in the given gear slot.
    /// Rules:
    ///   isMainHand only  → Weapon slot only
    ///   isOffhand  only  → OffHandWeapon slot only
    ///   both flags       → either Weapon or OffHandWeapon (pistols, daggers, etc.)
    ///   is2Handed        → Weapon slot (offhand is auto-populated as a ghost)
    /// </summary>
    // public bool CanEquipToSlot(GearSlot slot)
    // {
    //     if (is2Handed)
    //         return slot == GearSlot.Weapon;

    //     bool allowMain = isMainHand;
    //     bool allowOff = isOffhand;

    //     if (allowMain && allowOff)
    //         return slot == GearSlot.Weapon || slot == GearSlot.OffHandWeapon;
    //     if (allowOff)
    //         return slot == GearSlot.OffHandWeapon;
    //     // Default: mainhand-only (or neither flag set → treat as mainhand)
    //     return slot == GearSlot.Weapon;
    // }

    /// <summary>
    /// Convert this WeaponConfig to WeaponSettings for runtime use (main-hand offsets)
    /// </summary>
    public WeaponSettings ToWeaponSettings()
    {
        return new WeaponSettings
        {
            weaponPrefab = weaponPrefab,
            aimingRadius = aimingRadius,
            northEastOffset = northEastOffset,
            northWestOffset = northWestOffset,
            southEastOffset = southEastOffset,
            southWestOffset = southWestOffset,
            lockTo2Directions = lockTo2Directions,
            flipWeaponOnTurn = flipWeaponOnTurn,
            flipWeaponOnYAxis = flipWeaponOnYAxis,
            flipWeaponOnXAxis = flipWeaponOnXAxis,
            weaponBehindOnNE = weaponBehindOnNE,
            weaponBehindOnNW = weaponBehindOnNW,
            weaponBehindOnSE = weaponBehindOnSE,
            weaponBehindOnSW = weaponBehindOnSW,
            handBehindOnNE = handBehindOnNE,
            handBehindOnNW = handBehindOnNW,
            handBehindOnSE = handBehindOnSE,
            handBehindOnSW = handBehindOnSW,
            handRotationOffset = handRotationOffset,
            weaponDamageMin = weaponDamageMin,
            weaponDamageMax = weaponDamageMax,
            weaponDamageType = weaponDamageType
        };
    }

    /// <summary>
    /// Convert this WeaponConfig to WeaponSettings using the offhand-specific offsets.
    /// Called when this weapon is equipped in the OffHandWeapon slot.
    /// Falls back to main-hand offsets if offhand offsets are all zero.
    /// </summary>
    public WeaponSettings ToOffhandWeaponSettings()
    {
        // Use offhand offsets if any are non-zero; otherwise fall back to main-hand
        bool hasOffhandOffsets = offhandNorthEastOffset != Vector2.zero
                            || offhandNorthWestOffset != Vector2.zero
                            || offhandSouthEastOffset != Vector2.zero
                            || offhandSouthWestOffset != Vector2.zero;
        bool hasOffhandSorting = offhandWeaponBehindOnNE || offhandWeaponBehindOnNW || offhandWeaponBehindOnSE || offhandWeaponBehindOnSW;
        return new WeaponSettings
        {
            weaponPrefab = weaponPrefab,
            aimingRadius = aimingRadius,
            northEastOffset = hasOffhandOffsets ? offhandNorthEastOffset : northEastOffset,
            northWestOffset = hasOffhandOffsets ? offhandNorthWestOffset : northWestOffset,
            southEastOffset = hasOffhandOffsets ? offhandSouthEastOffset : southEastOffset,
            southWestOffset = hasOffhandOffsets ? offhandSouthWestOffset : southWestOffset,
            lockTo2Directions = lockTo2Directions,
            flipWeaponOnTurn = flipWeaponOnTurn,
            flipWeaponOnYAxis = flipWeaponOnYAxis,
            flipWeaponOnXAxis = flipWeaponOnXAxis,
            weaponBehindOnNE = hasOffhandSorting ? offhandWeaponBehindOnNE : weaponBehindOnNE,
            weaponBehindOnNW = hasOffhandSorting ? offhandWeaponBehindOnNW : weaponBehindOnNW,
            weaponBehindOnSE = hasOffhandSorting ? offhandWeaponBehindOnSE : weaponBehindOnSE,
            weaponBehindOnSW = hasOffhandSorting ? offhandWeaponBehindOnSW : weaponBehindOnSW,
            handBehindOnNE = handBehindOnNE,
            handBehindOnNW = handBehindOnNW,
            handBehindOnSE = handBehindOnSE,
            handBehindOnSW = handBehindOnSW,
            weaponDamageMin = weaponDamageMin,
            weaponDamageMax = weaponDamageMax,
            weaponDamageType = weaponDamageType
        };
    }
}

/// <summary>
/// Configuration for a weapon's ammo/magazine system.
/// Attached to WeaponConfig when usesAmmo is true.
/// </summary>
[Serializable]
public class AmmoConfig
{
    [Tooltip("Ability requires ammo to execute. Won't trigger if out of ammo.")]
    public bool dependsOnAmmo = true;
    [Tooltip("Maximum ammo in magazine before reload required")]
    public int magazineSize = 10;
    [Tooltip("Time in seconds to reload")]
    public float reloadTime = 2f;
    [Tooltip("Icon sprite for this ammo type (bullet, shell, energy, etc.)")]
    public Sprite ammoIcon;
}
