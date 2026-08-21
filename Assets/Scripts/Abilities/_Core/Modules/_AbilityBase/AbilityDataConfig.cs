using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Defines when to spawn a particle effect during an ability
/// </summary>
[Serializable]
public class TimedParticleSpawn
{
    [Tooltip("Particle system prefab to spawn")]
    public ParticleSystem particlePrefab;

    [Tooltip("Time in seconds after ability activation to spawn particle")]
    public float spawnTime = 0f;

    [Tooltip("Spawn at character position")]
    public bool spawnAtCharacter = true;

    [Tooltip("Spawn at weapon/aim position (for projectile abilities)")]
    public bool spawnAtWeapon = false;

    [Tooltip("Position offset from spawn point")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Should the particle follow the character/weapon?")]
    public bool attachToSource = false;

    [Tooltip("Rotation mode for the particle")]
    public ParticleRotationMode rotationMode = ParticleRotationMode.Default;

    [Tooltip("Custom rotation (only used if rotationMode is Custom)")]
    public Vector3 customRotation = Vector3.zero;
}

public enum ParticleRotationMode
{
    Default,        // Use prefab's rotation
    FaceAimDirection, // Rotate towards aim direction
    Custom          // Use customRotation value
}

/// <summary>
/// Generic, fully configurable ability system.
/// Contains all mechanical properties (cooldowns, energy, charges) and ability type configurations.
/// Replaces specific configs like HunterPrimaryConfig, EngineerPrimaryConfig, etc.
/// Uses boolean flags to enable/disable different ability components.
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "Abilities/New Ability")]
public class AbilityDataConfig : AbilityConfig
{
    [Header("Mechanical Properties")]
    [Tooltip("Prevents this ability from being manually cast. System-driven behavior such as permanent auras can still initialize it.")]
    public bool disableCast = false;
    [Tooltip("Is this an attack (affected by attack speed) or a spell (cooldown only)?")]
    public bool isAttack = false;
    public bool isInstant = false;
    [Header("Weapon Requirements")]
    [Tooltip("Weapon types required to use this ability. Empty list = can use with any weapon or no weapon. Use 'Any' to require any weapon type.")]
    [NonReorderable]
    [WeaponTypeDropdown]
    public List<string> requiredWeaponTypes = new List<string>();
    [Tooltip("Attacks per second (for attacks). Modified by player attack speed %.")]
    public float attackSpeed = 1f;
    [Tooltip("Additional cooldown after attack (optional). Also applies to spells.")]
    public float cooldownTime = 0f;
    public float energyCost = 0f;

    [Header("Crit")]
    [Tooltip("Base crit chance for this ability (fraction: 0.05 = 5%). Added to the character's CritChance stat and any trait CritChance modifiers.")]
    public float baseCritChance = 0f;
    [Tooltip("Bonus crit damage multiplier for this ability (e.g. 0.5 = +50% crit damage). Added on top of the character's CritDamage stat when this ability crits.")]
    public float baseCritDamageMultiplier = 0f;
    
    [Header("Autocast")]
    [Tooltip("Automatically cast this ability on valid enemies in range. Uses enemy position instead of mouse position.")]
    public bool autocast = false;
    [Tooltip("Cast this ability at whoever just attacked the owner when they take damage — no keybind assigned.")]
    public bool retaliationCast = false;
    [Tooltip("When autocasting, cast at the player's feet instead of at enemy position. Useful for auras and self-buffs.")]
    public bool castAtFeet = false;
    public bool castAtTargets = false;
    public bool castAtFriendlyTargets = false;
    [Tooltip("Range to search for enemies when autocasting. Uses projectile maxRange if 0.")]
    public float autocastRange = 0f;
    [Tooltip("How many unique enemies to target per autocast cycle. Each target receives one cast. Minimum 1.")]
    public int autocastTargets = 1;
    
    [Header("Multicast")]
    [Tooltip("Can this ability benefit from the Multicast stat? If true, ability casts multiple times based on player's Multicast stat value.")]
    public bool canMulticast = false;
    
    public bool disablesMovementDuringCast = false;
    [Tooltip("How long to block player movement (in seconds). Only used if disablesMovementDuringCast is true.")]
    public float movementBlockDuration = 0.5f;
    [Tooltip("Optional movement speed multiplier while casting (1 = no change, 0 = cannot move). If < 1, this is used instead of hard control lock.")]
    [Range(0f, 1f)]
    public float movementSpeedMultiplierDuringCast = 1f;

    [Header("Charge System")]
    [Tooltip("Does this ability use a charge system instead of cooldown?")]
    public bool hasCharges = false;
    public int maxCharges = 1;
    public float chargeRechargeTime = 1f;

    [Header("Combo System")]
    [Tooltip("Use this ability as a shell that sequentially casts the abilities in Combo Abilities.")]
    // Assets authored before the rename still serialize "hasCombo"; without this they silently load as false.
    [UnityEngine.Serialization.FormerlySerializedAs("hasCombo")]
    public bool isCombo = false;
    [Tooltip("Abilities to execute in order when this shell ability is cast.")]
    [NonReorderable]
    public AbilityDataConfig[] comboAbilities;
    [Tooltip("Time to wait after each combo step's animation completes before advancing to the next step (in seconds). Array length should match comboAbilities length.")]

    [NonReorderable]
    public float[] comboStepDelays = new float[] { 0.3f };
    [Tooltip("How long the player has to trigger the next combo step after a step completes (seconds).")]
    public float comboInputWindow = 0.75f;

    [Header("Animations")]
    [Tooltip("Animation to play on character when ability is activated/channeled")]
    public string characterAnimationName = "";
    [Tooltip("Animation to play on character when facing up during ability")]
    public string characterAnimationUp = "";
    [Tooltip("Animation to play on the character before firing. Used by enemies and other casters without weapon animators.")]
    public string characterPrecastAnimationName = "";
    [Tooltip("Animation to play on mainhand weapon when ability is activated/fired")]
    public string mainhandAnimationName = "";
    [Tooltip("Animation to play on offhand weapon when ability is activated/fired")]
    public string offhandAnimationName = "";
    [Tooltip("Play character and/or weapon pre-cast animations before firing? Delay is calculated from the longest configured clip.")]
    public bool hasPrecast = false;
    [Tooltip("Animation to play on weapon before firing (pre-cast for spells, draw for attacks). Delay calculated from clip length.")]
    public string preAnimationName = "";
    [Tooltip("Activate the ability on button release instead of press. Flow: precast -> hold animation (looping while held) -> release -> cast animation.")]
    public bool activateOnButtonRelease = false;
    [Tooltip("Looping animation to play on weapon while the button is held (between precast and cast). Requires activateOnButtonRelease.")]
    public string holdAnimationName = "";
    [Tooltip("Hold-to-charge configuration: bar duration, overcharge bars, and per-bar field modifiers. Requires activateOnButtonRelease.")]
    public HoldChargeConfig holdChargeConfig;
    [Tooltip("Animation to return to after weapon animation completes (e.g., 'Idle'). Leave empty to not reset.")]
    public string weaponIdleAnimationName = "Idle";
    [Tooltip("Temporarily unlock weapon from 2-direction lock during this ability, allowing free aim at cursor")]
    public bool unlockWeaponDirections = false;
    [Tooltip("Duration to keep weapon unlocked after firing (only works when unlockWeaponDirections is enabled). Set to 0 to lock immediately.")]
    public float rotationLockDuration = 0f;
    [Tooltip("When weapon directions are unlocked, keep following live aim instead of freezing at the initial unlocked angle.")]
    public bool continueRotatingDuringUnlock = false;
    [Tooltip("Flip the Y-axis of the weapon sprite when facing left (for 2D sprite flipping based on facing direction)")]
    public bool flipYOnLeftFacing = false;
    public bool flipXOnLeftFacing = false;

    [Header("Timed Particle Effects")]
    [Tooltip("Particle effects to spawn at specific times during the ability (e.g., slash effects during melee animation)")]
    [NonReorderable]
    public List<TimedParticleSpawn> timedParticles = new List<TimedParticleSpawn>();

    [Header("Ability Type Flags")]
    [Tooltip("Does this ability use a weapon (gun, bow, melee)?")]
    public bool isProjectileAbility = false;
    [Tooltip("Does this ability create an area effect?")]
    public bool isAreaAbility = false;
    [Tooltip("Does this ability spawn constructs (pylons, turrets, totems)?")]
    public bool isConstructAbility = false;
    [Tooltip("Does this ability place traps that trigger when enemies enter range?")]
    public bool isTrapAbility = false;
    [Tooltip("Is this ability focused on movement (dash, teleport, blink)?")]
    public bool isMovementAbility = false;
    [Tooltip("Does the area follow a projectile?")]
    public bool areaFollowsProjectile = false;
    [Tooltip("Is this a channeled ability?")]
    public bool isChanneled = false;
    [Tooltip("Is this a beam ability (laser, lightning, etc.)?")]
    public bool isBeamAbility = false;
    [Tooltip("Is this a melee ability (hitbox-based close-range attack)?")]
    public bool isMeleeAbility = false;
    [Tooltip("Is this an explosion ability (instant AOE damage with knockback)?")]
    public bool isExplosionAbility = false;
    [Tooltip("Is this a summon ability (spawns a pet that follows and fights)?")] 
    public bool isSummonAbility = false;
    [Tooltip("Is this a passive ability (no activation, attaches a MonoBehaviour script to the player for the duration)?")]
    public bool isPassiveAbility = false;
    [Tooltip("Shown if isPassiveAbility = true. The fully-qualified class name of the MonoBehaviour to attach (e.g. 'FireBurnPassive').")]
    public string passiveTypeName;
    [Tooltip("Shown if isPassiveAbility = true. ScriptableObject-backed passive settings and runtime mapping.")]
    public PassiveConfig passiveConfig = new PassiveConfig();

#if UNITY_EDITOR
    [Tooltip("Drag the C# script asset here — the class name is copied to passiveTypeName automatically.")]
    public UnityEditor.MonoScript passiveScript;

    private void OnValidate()
    {
        if (passiveScript != null)
        {
            System.Type t = passiveScript.GetClass();
            if (t != null && typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(t))
                passiveTypeName = t.Name;
            else
                UnityEngine.Debug.LogWarning($"[AbilityDataConfig] '{passiveScript.name}' is not a MonoBehaviour — passiveTypeName not set.");
        }
    }
#endif

    [Tooltip("Does this ability use an ammo system?")]
    public bool usesAmmo = false;

    [Header("Ability Configurations")]
    [Tooltip("Shown if isBeamAbility = true")]
    public BeamAbilityConfig beamConfig;
    [Tooltip("Shown if isChanneled = true")]
    public ChannelAbilityConfig channelConfig;
    [Tooltip("Shown if isMeleeAbility = true")]
    public MeleeConfig meleeConfig;
    public WeaponAbilityData weaponData = new WeaponAbilityData();
    [Tooltip("Shown if isProjectileAbility = true")]
    public ProjectileConfig projectileConfig;
    public StatusEffectData onHitEffects = new StatusEffectData();
    public StatusEffectData onKillEffects = new StatusEffectData();
    [Tooltip("Shown if isAreaAbility = true")]
    public AreaConfig areaConfig;
    public AreaEffectData onEnterEffects = new AreaEffectData();
    public AreaEffectData lingeringEffects = new AreaEffectData();
    public AreaEffectData onExitEffects = new AreaEffectData();
    [Tooltip("Shown if isConstructAbility = true")]
    public ConstructConfig constructConfig = new ConstructConfig();
    [Tooltip("Shown if isTrapAbility = true")]
    public TrapAbilityConfig trapConfig = new TrapAbilityConfig();
    [Tooltip("Shown if isExplosionAbility = true")]
    public ExplosionConfig explosionConfig = new ExplosionConfig();
    [Tooltip("Shown if isSummonAbility = true")]
    public SummonConfig summonConfig = new SummonConfig();
    [Tooltip("Shown if isMovementAbility = true")]
    public MovementConfig movementConfig = new MovementConfig();
    [Tooltip("Effects applied when ability is cast (buffs/debuffs on caster)")]
    public AbilityCastEffects castEffects = new AbilityCastEffects();

    [Header("Hit Visuals")]
    [Tooltip("Prefab spawned at the hit position whenever this ability damages a target. Shared across all ability types.")]
    public GameObject hitVisualPrefab;
    [Tooltip("Sound played at the hit position whenever this ability damages a target.")]
    public AudioClip hitVisualSound;
    [Tooltip("Flash color applied to the target sprite when hit by this ability.")]
    public Color hitFlashColor = Color.white;

    /// <summary>
    /// Returns true if this ability requires manual activation via keybind.
    /// Passive auras and autocast abilities do not require keybinds.
    /// </summary>
    public bool RequiresKeybind => !disableCast && !(areaConfig?.isAura ?? false) && !isPassiveAbility && !autocast && !retaliationCast;

    [Header("Triggered Ability")]
    [Tooltip("This ability is never cast directly by the player. It only fires via on-hit EffectData triggers. " +
             "Abilities with this flag are stored in the character's triggered ability loadout slot (hidden from the UI) " +
             "so their modifiers still resolve correctly per-character.")]
    public bool isTriggeredOnly = false;
}

// ===========================
// WEAPON DATA
// ===========================

[Serializable]
public class WeaponAbilityData
{
    public WeaponType weaponType = WeaponType.Gun;

    [Tooltip("The weapon GameObject prefab")]
    public GameObject weaponPrefab;

    [Tooltip("Delay between ability activation and weapon fire/swing")]
    public float activationDelay = 0f;

    [Header("Bow-Specific Settings")]
    [Tooltip("Time to fully draw the bow (bow weapons only)")]
    public float drawTime = 0.5f;

    [Tooltip("Can hold button to continuously fire")]
    public bool canHoldToFire = false;
}

public enum WeaponType
{
    Gun,
    Bow,
    Melee
    //Thrown
}

// ===========================
// STATUS EFFECTS
// ===========================

[Serializable]
public class StatusEffectData
{
    public bool canRoot = false;
    public RootEffectConfig rootConfig = new RootEffectConfig();

    public bool canSlow = false;
    public SlowEffectConfig slowConfig = new SlowEffectConfig();

    public bool canStun = false;
    public StunEffectConfig stunConfig = new StunEffectConfig();

    public bool canBleed = false;
    public BleedEffectConfig bleedConfig = new BleedEffectConfig();

    public bool canBurn = false;
    public BurnEffectConfig burnConfig = new BurnEffectConfig();

    public bool canPoison = false;
    public PoisonEffectConfig poisonConfig = new PoisonEffectConfig();

    public bool canKnockback = false;
    public KnockbackEffectConfig knockbackConfig = new KnockbackEffectConfig();

    public bool canExplode = false;
    public ExplosionEffectConfig explosionConfig = new ExplosionEffectConfig();
}

// ===========================
// AREA EFFECTS
// ===========================

[Serializable]
public class AreaEffectData
{
    public bool canRoot = false;
    public RootEffectConfig rootConfig = new RootEffectConfig();

    public bool canSlow = false;
    public SlowEffectConfig slowConfig = new SlowEffectConfig();

    public bool canStun = false;
    public StunEffectConfig stunConfig = new StunEffectConfig();


    public bool canBurn = false;
    public BurnEffectConfig burnConfig = new BurnEffectConfig();

    public bool canPoison = false;
    public PoisonEffectConfig poisonConfig = new PoisonEffectConfig();

    public bool canHeal = false;
    public HealEffectConfig healConfig = new HealEffectConfig();

    public bool canKnockback = false;
    public KnockbackEffectConfig knockbackConfig = new KnockbackEffectConfig();

    public bool canCleanse = false;
    public CleanseEffectConfig cleanseConfig = new CleanseEffectConfig();
}

// ===========================
// ABILITY CAST EFFECTS
// ===========================

[Serializable]
public class AbilityCastEffects
{
    public bool grantsBuff = false;
    [Tooltip("Custom buff script for special behaviors")]
    public EffectConfig customBuffScript;

    public bool consumesHealth = false;
    public float healthCost = 0f;

    public bool appliesSelfDebuff = false;
    [Tooltip("Custom debuff script for special behaviors")]
    public EffectConfig customDebuffScript;
}

// ===========================
// EFFECT CONFIGURATIONS
// ===========================

[Serializable]
public class RootEffectConfig
{
    [Range(0f, 1f)] public float applicationChance = 1f;
    public float duration = 2f;
}

[Serializable]
public class SlowEffectConfig
{
    [Range(0f, 1f)] public float applicationChance = 1f;
    public float duration = 2f;
    [Range(0f, 1f)] public float slowAmount = 0.5f; // 50% slow
}

[Serializable]
public class StunEffectConfig
{
    [Range(0f, 1f)] public float applicationChance = 1f;
    public float duration = 1f;
}


[Serializable]
public class BleedEffectConfig
{
    [Range(0f, 1f)] public float applicationChance = 1f;
    public float damagePerTick = 5f;
    public float tickInterval = 1f;
    public float duration = 3f;
    public GameObject bloodParticlePrefab;
}

[Serializable]
public class BurnEffectConfig
{
    [Range(0f, 1f)] public float applicationChance = 1f;
    public float damagePerTick = 10f;
    public float tickInterval = 0.5f;
    public float duration = 3f;
    public GameObject fireParticlePrefab;
}

[Serializable]
public class PoisonEffectConfig
{
    [Range(0f, 1f)] public float applicationChance = 1f;
    public float damagePerTick = 3f;
    public float tickInterval = 1f;
    public float duration = 5f;
    public GameObject poisonParticlePrefab;
}

[Serializable]
public class KnockbackEffectConfig
{
    [Range(0f, 1f)] public float applicationChance = 1f;
    public float force = 10f;
}

[Serializable]
public class ExplosionEffectConfig
{
    [Range(0f, 1f)] public float applicationChance = 1f;
    public float radius = 3f;
    public float damage = 50f;
    public GameObject explosionPrefab;
}

[Serializable]
public class HealEffectConfig
{
    public float healPerTick = 5f;
    public bool healsAllies = true;
    public bool healsSelf = true;
}

[Serializable]
public class CleanseEffectConfig
{
    public bool cleansesDebuffs = true;
    public bool cleansesDoTs = true;
}


