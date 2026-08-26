using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Component.Animating;
using JoeConticello.VisualEffects;

/// <summary>
/// Universal data-driven ability class that handles all ability types based on AbilityDataConfig.
/// Handles all mechanical logic (cooldowns, energy, charges) and delegates to specific ability implementations.
/// No need for character-specific ability scripts - behavior is entirely config-driven.
/// Supports both local (single-player) and networked (multiplayer) spawning.
/// Uses ServerRpc for clients to request ability execution, ensuring all clients see projectiles/effects.
/// </summary>
public class DataDrivenAbility : Ability
{
    private const string AbilityPipelineTag = "[Ability pipeline]";

    private Organism ownerOrganism; // Owner of this ability (PlayerController or Enemy)
    private PlayerController ownerAsPlayer; // Null if owner is not a player
    private Rigidbody2D rb;
    private EffectManager ownerEffectManager;

    // Mechanical state (moved from base Ability class)
    private float lastUsedTime = -999f;
    private int currentCharges;

    // Ammo tracking for abilities that use ammo
    private int currentAmmo;
    private bool isReloading = false;
    private float reloadStartTime;
    private ReloadBar reloadBar;
    private ChargeBar chargeBar;

    // Runtime ammo modifiers accumulated from active weapon traits
    private int _ammoMagazineBonus = 0;
    private float _ammoReloadDelta = 0f;
    private AmmoConfig _effectiveAmmoConfig;

    // Accumulated ability config overrides from traits (Property Path System)
    private Dictionary<string, AbilityModifierRuntime.AccumulatedValue> _accumulatedOverrides;

    // Cached effective sub-config copies (null = no mods, use base config directly)
    private ProjectileConfig _effectiveProjectileConfig;
    private AreaConfig _effectiveAreaConfig;
    private BeamAbilityConfig _effectiveBeamConfig;
    private MeleeConfig _effectiveMeleeConfig;
    private ExplosionConfig _effectiveExplosionConfig;
    private SummonConfig _effectiveSummonConfig;
    private ConstructConfig _effectiveConstructConfig;
    private HoldChargeConfig _effectiveHoldChargeConfig;
    private AbilityDataConfig _effectiveAbilityConfig;

    // Icon override from trait ability config modifiers
    private Sprite _effectiveAbilityIcon;
    public Sprite EffectiveAbilityIcon => _effectiveAbilityIcon ?? config?.abilityIcon;

    // Public accessors for tooltip description builder
    public Dictionary<string, AbilityModifierRuntime.AccumulatedValue> AccumulatedOverrides => _accumulatedOverrides;
    public ProjectileConfig EffectiveProjectileConfig => _effectiveProjectileConfig ?? config?.projectileConfig;
    public AreaConfig EffectiveAreaConfig => _effectiveAreaConfig ?? config?.areaConfig;
    public BeamAbilityConfig EffectiveBeamConfig => _effectiveBeamConfig ?? config?.beamConfig;
    public MeleeConfig EffectiveMeleeConfig => _effectiveMeleeConfig ?? config?.meleeConfig;
    public ExplosionConfig EffectiveExplosionConfig => _effectiveExplosionConfig ?? config?.explosionConfig;
    public SummonConfig EffectiveSummonConfig => _effectiveSummonConfig ?? config?.summonConfig;
    public ConstructConfig EffectiveConstructConfig => _effectiveConstructConfig ?? config?.constructConfig;
    public MovementConfig EffectiveMovementConfig => EffectiveAbilityConfig?.movementConfig;
    public AbilityDataConfig EffectiveAbilityConfig => _effectiveAbilityConfig ?? config;

    private bool isHoldingFire = false;
    private float lastFireTime = -999f;
    private bool isMovementPrecastPending = false;

    // Per-recharge progress tracking (for Stamina HUD pips)
    private float rechargeStartTime = -999f;

    // Charging state for projectile launch delay
    private bool isCharging = false;
    private float chargeStartTime;
    private Coroutine chargingCoroutine;
    private bool _lastCastSequenceSucceeded = false;

    // Hold charge: 0..maxBars value recorded at button release (0 if no holdChargeConfig)
    private float lastChargeValue = 0f;
    public float LastChargeValue => lastChargeValue;

    // Hold-to-release state (activateOnButtonRelease)
    private bool isHoldingForRelease = false;

    // Beam Ability components
    private BeamAbility beamAbility;
    // Channel Ability component
    private ChannelAbility channelAbility;
    // Movement Ability component
    private MovementAbility movementAbility;
    // Weapon activation delay state
    private bool isActivatingWeapon = false;
    private Coroutine weaponActivationCoroutine;
    // Player control state
    private bool playerControl = true; // When false, ability has full control of character movement
    // Construct tracking
    private List<GameObject> activeConstructs = new List<GameObject>();
    // Summon tracking
    private List<GameObject> activeSummons = new List<GameObject>();
    private SummonAbility _summonAbility; // persistent group manager, created once per ability
    // Construct placement preview state
    private bool isPlacingConstruct = false;
    private Coroutine placementCoroutine = null;
    private GameObject constructPlacementGhost = null;
    [Header("Placement Debug")]
    [Tooltip("Log placement lifecycle events (ghost spawn, cursor tracking, confirm). Disable in production.")]
    [SerializeField] private bool logPlacement = false;
    // Trap tracking
    private List<GameObject> activeTraps = new List<GameObject>();
    // Weapon animation idle return tracking

    private Coroutine weaponIdleReturnCoroutine;
    private Coroutine movementControlReleaseCoroutine;
    private int movementControlLockToken = 0;
    private Coroutine castMoveSpeedReleaseCoroutine;
    private float castMoveSpeedAppliedDelta = 0f;
    private bool hasCastMoveSpeedModifier = false;
    private bool _lastMovementControlLoggedPlayerControl = true;
    private bool _lastMovementControlLoggedMovementExecuting = false;
    private bool _lastMovementControlLoggedPrecastPending = false;
    private int _lastMovementControlLoggedToken = -1;
    // Weapon direction locking - locks weapon to a specific angle during ability
    private bool isWeaponDirectionLocked = false;
    private float lockedWeaponAngle = 0f;
    private float rotationLockEndTime = 0f; // Time when rotation lock expires
    private bool isMainhandLocked = false; // True if mainhand weapon is locked
    private bool isOffhandLocked = false; // True if offhand weapon is locked
    // Ability slot tracking - which ability slot (0=primary, 1=secondary, etc.) this ability is bound to
    private int abilitySlotIndex = 0;
    private bool isTriggeredProjectileOnly = false;
    // Autocast target — set each frame by the autocast tick, cleared after GetTargetWorldPosition consumes it
    private Vector3? _autocastTarget = null;
    private float _lastAutocastAttempt = -999f;
    // True while the autocast burst loop is iterating multi-target casts so the cooldown
    // check in CanUseAbility is skipped for the 2nd+ cast in the same burst.
    private bool _autocastBurstActive = false;


    /// <summary>
    /// Casts once using an explicit world-space target, then clears the override.
    /// </summary>
    public bool TryUseAbilityAt(Vector3 targetPosition)
    {
        _autocastTarget = targetPosition;
        bool succeeded = TryUseAbility();
        _autocastTarget = null;
        return succeeded;
    }

    public bool TryUseAbilityManually()
    {
        if (config != null && config.disableCast)
        {
            Debug.Log($"{AbilityPipelineTag} Manual cast blocked: ability={config.abilityName}, reason=disableCast");
            return false;
        }

        return TryUseAbility();
    }

    /// <summary>
    /// Fired by the owner Organism when they take damage.
    /// If retaliationCast is enabled and the ability is off cooldown, cast at the attacker.
    /// </summary>
    private void HandleRetaliationHit(IDamageable victim, float damage, string damageType, Vector3 attackerPosition, GameObject attackerObject)
    {
        if (attackerObject == null) return;
        if (isOnCooldown) return;

        _autocastTarget = attackerObject.transform.position;
        _lastAutocastAttempt = Time.time;
        TryUseAbility();
        _autocastTarget = null;
    }

    // Combo tracking
    private int currentComboIndex = 0;
    private float comboWindowExpiresAt = -999f;
    private bool isExecutingCombo = false;
    private Coroutine comboChainCoroutine = null;
    // One fully-initialized runner per combo step; the shell only sequences them.
    private DataDrivenAbility[] comboSteps;
    private bool isComboStep = false;
    private Coroutine comboIdleWatcher = null;

    // Alternating animation tracking for dual-wielding
    private bool lastAnimationWasMainhand = false; // Tracks which hand animated last for alternating system

    // Public properties for UI
    public bool IsPerformingAbility => (movementAbility != null && movementAbility.IsExecuting) || (channelAbility != null && channelAbility.IsChanneling) || isActivatingWeapon || isWeaponDirectionLocked || isMovementPrecastPending || !playerControl;
    public bool HasPlayerControl => playerControl; // Simple flag: false = ability controls movement, true = player controls movement
    public bool IsMovementAbilityExecuting => movementAbility != null && movementAbility.IsExecuting;
    public float CooldownTime => GetEffectiveCooldown();
    public float EnergyCost => GetEffectiveEnergyCost();
    public int MaxCharges => GetEffectiveMaxCharges();
    public int CurrentCharges => currentCharges;
    /// <summary>Fraction [0,1] representing how far the current charge recharge has progressed. 1 = fully ready.</summary>
    public float ChargeRechargeProgress
    {
        get
        {
            if (config == null || !config.hasCharges || currentCharges >= GetEffectiveMaxCharges()) return 1f;
            if (rechargeStartTime <= 0f) return 0f;
            float rechargeTime = GetEffectiveRechargeTime();
            if (rechargeTime <= 0f) return 1f;
            return Mathf.Clamp01((Time.time - rechargeStartTime) / rechargeTime);
        }
    }
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => GetActiveAmmoConfig()?.magazineSize ?? 0;
    public float GetRemainingCooldown() => Mathf.Max(0f, (lastUsedTime + GetEffectiveCooldown()) - Time.time);
    public float GetCooldownPercentage() => 1f - (GetRemainingCooldown() / GetEffectiveCooldown());
    public bool IsWeaponDirectionLocked => isWeaponDirectionLocked;
    public float LockedWeaponAngle => lockedWeaponAngle;
    public bool IsMainhandLocked => isMainhandLocked;
    public bool IsOffhandLocked => isOffhandLocked;
    public bool FlipYOnLeftFacingDuringLock => isWeaponDirectionLocked && config != null && config.flipYOnLeftFacing;
    public bool FlipXOnLeftFacingDuringLock => isWeaponDirectionLocked && config != null && config.flipXOnLeftFacing;
    public bool ContinueRotatingDuringUnlock => isWeaponDirectionLocked && config != null && config.continueRotatingDuringUnlock;
    /// <summary>
    /// Returns true if weapon flipping should still apply during rotation lock (for timed rotation locks)
    /// </summary>
    public bool AllowFlipDuringLock => isWeaponDirectionLocked && rotationLockEndTime > 0f;

    /// <summary>
    /// Set which ability slot this ability is bound to (0=primary, 1=secondary, etc.)
    /// This is used to determine which input button to check for hold-to-fire
    /// </summary>
    public void SetAbilitySlot(int slotIndex)
    {
        abilitySlotIndex = slotIndex;
    }

    public void ConfigureAsTriggeredProjectile()
    {
        isTriggeredProjectileOnly = true;
        abilitySlotIndex = -2;
    }

    /// <summary>
    /// Marks this runner as a step inside a combo shell: it never polls input or autocasts,
    /// it is only cast by its owning shell.
    /// </summary>
    public void ConfigureAsComboStep(int ownerSlotIndex)
    {
        isComboStep = true;
        abilitySlotIndex = ownerSlotIndex;
    }

    public bool FireTriggeredProjectile(float damageMultiplier = 1f)
    {
        ProjectileConfig projectileConfig = GetEffectiveProjectileConfig();
        if (!isTriggeredProjectileOnly || config == null || !config.isProjectileAbility ||
            projectileConfig == null || projectileConfig.hitbox?.prefab == null)
        {
            Debug.LogWarning($"[DataDrivenAbility] Cannot fire triggered projectile for '{config?.abilityName ?? "null"}'.");
            return false;
        }

        PerformProjectileShoot(damageMultiplier, false);
        return true;
    }

    /// <summary>
    /// Returns the angle from the weapon's LaunchZone to the mouse (for accurate barrel aiming).
    /// Falls back to transform.position if no weapon or LaunchZone is found.
    /// </summary>
    private float GetAngleToMouseFromLaunchZone(string weaponHolderPath)
    {
        Transform weaponTransform = transform.Find(weaponHolderPath);
        if (weaponTransform != null)
        {
            Transform launchZone = WeaponLaunchPoint.FindLaunchZone(weaponTransform);
            if (launchZone != null)
                return InputUtility.GetAngleToMouse(launchZone.position);
        }
        return InputUtility.GetAngleToMouse(transform.position);
    }

    // Dynamic cooldown calculation (uses Property Path overrides)
    private float GetEffectiveCooldown()
    {
        if (config == null) return 0f;

        float baseCooldown = 0f;

        // Get effective cooldown time from overrides
        float effectiveCooldownTime = config.cooldownTime;
        if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("cooldownTime", out var cdAccum))
        {
            effectiveCooldownTime = ApplyRateDurationModifiers(effectiveCooldownTime, cdAccum);
            if (cdAccum.hasSetOverride) effectiveCooldownTime = cdAccum.setNumeric;
        }

        // If it's an attack, calculate cooldown from attack speed
        if (config.isAttack)
        {

            float effectiveAttackSpeed = GetEffectiveAttackSpeed();
            // Convert to cooldown (1 / attacks per second) + any additional cooldown
            baseCooldown = (1f / effectiveAttackSpeed) + effectiveCooldownTime;
        }
        else
        {
            // For spells, just use cooldown time
            baseCooldown = effectiveCooldownTime;
        }

        // Ensure cooldown doesn't go below 0.1 seconds
        return Mathf.Max(0.1f, baseCooldown);
    }

    private float GetEffectiveAttackSpeed()
    {
        float effectiveAttackSpeed = config.attackSpeed;
        if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("attackSpeed", out var asAccum))
        {
            effectiveAttackSpeed = (effectiveAttackSpeed + asAccum.flatDelta) * (1f + asAccum.percentDelta / 100f);
            if (asAccum.hasSetOverride) effectiveAttackSpeed = asAccum.setNumeric;
        }
        if (effectiveAttackSpeed <= 0f) effectiveAttackSpeed = 0.001f;

        // Get owner's attack speed bonus
        float attackSpeedBonus = 0f;
        if (ownerOrganism != null && ownerOrganism.AllStats != null)
        {
            attackSpeedBonus = ownerOrganism.AllStats.GetStat("AttackSpeed");
        }

        // Convert to multiplier (0.03 = 1.03x, 0.5 = 1.5x, 2.5 = 3.5x)
        float attackSpeedMultiplier = 1f + attackSpeedBonus;

        // Calculate effective attack speed: baseAttackSpeed * multiplier
        effectiveAttackSpeed *= attackSpeedMultiplier;
        return effectiveAttackSpeed;
    }

    private float GetEffectiveEnergyCost()
    {
        if (config == null) return 0f;
        float baseCost = config.energyCost;
        if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("energyCost", out var accum))
        {
            baseCost = (baseCost + accum.flatDelta) * (1f + accum.percentDelta / 100f);
            if (accum.hasSetOverride) baseCost = accum.setNumeric;
        }
        return Mathf.Max(0f, baseCost);
    }

    private bool isOnCooldown => Time.time < lastUsedTime + GetEffectiveCooldown();

    private void Awake()
    {
        // DataDrivenAbility is self-contained - only use owner's components
        ownerOrganism = GetComponent<Organism>();
        ownerAsPlayer = GetComponent<PlayerController>(); // Null if owner is enemy
        rb = GetComponent<Rigidbody2D>();
        ownerEffectManager = GetComponent<EffectManager>();

        if (ownerEffectManager == null)
        {
            ownerEffectManager = GetComponentInChildren<EffectManager>();
        }

        if (ownerOrganism == null)
        {
            Debug.LogError($"[DataDrivenAbility] No Organism component found on {gameObject.name}! Ability requires PlayerController or Enemy.");
        }
    }

    /// <summary>
    /// Flag to track if ability has been initialized
    /// </summary>
    private bool isInitialized = false;

    /// <summary>
    /// Initialize the ability with its config. Call this immediately after SetAbilityReference().
    /// This ensures proper initialization order regardless of when Unity calls Start().
    /// </summary>
    public void InitializeAbility()
    {
        if (isInitialized) return; // Already initialized

        config = GetConfig<AbilityDataConfig>();
        if (config == null)
        {
            // Silently skip - this is likely a DataDrivenAbility component on a prefab
            // that's unused (enemies add new components dynamically)
            return;
        }

        // Initialize charge system
        if (config.hasCharges)
        {
            currentCharges = GetEffectiveMaxCharges();
        }

        if (config.isCombo)
        {
            BuildComboSteps();
        }

        // Initialize ammo system
        if (config.usesAmmo)
        {
            currentAmmo = GetActiveAmmoConfig()?.magazineSize ?? 0;

            // Find reload bar ONLY if owner is a player
            if (ownerAsPlayer != null)
            {
                reloadBar = ownerAsPlayer.GetComponentInChildren<ReloadBar>();
                if (reloadBar == null)
                {
                    Debug.LogWarning($"[DataDrivenAbility] No ReloadBar found for {config.abilityName} - reload progress won't be visible");
                }
            }
        }

        // Find charge bar for abilities with precast (hold-to-charge)
        if (ownerAsPlayer != null)
        {
            chargeBar = ownerAsPlayer.GetComponentInChildren<ChargeBar>();
            if (chargeBar == null)
            {
                Debug.LogWarning($"[DataDrivenAbility] No ChargeBar found for {config.abilityName} - charge progress won't be visible");
            }
        }
        if (config.isBeamAbility)
        {
            Debug.Log($"[DataDrivenAbility] Initializing beam ability: {config.abilityName}");
            InitializeBeamAbility();
        }

        // Initialize channel ability
        if (config.isChanneled)
        {
            Debug.Log($"[DataDrivenAbility] Initializing channel ability: {config.abilityName}");
            InitializeChannelAbility();
        }

        // Initialize movement ability
        if (config.isMovementAbility)
        {
            Debug.Log($"[DataDrivenAbility] InitializeAbility - Detected movement ability: {config.abilityName}, scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            // Each DataDrivenAbility gets its own MovementAbility instance so that
            // multiple movement abilities on the same GameObject (e.g. Dash + Snipe) never
            // overwrite each other's config on the shared component.
            movementAbility = gameObject.AddComponent<MovementAbility>();
            movementAbility.Initialize(config);
            Debug.Log($"[DataDrivenAbility] MovementAbility initialized for {config.abilityName}");
        }

        // Initialize passive ability — adds a passive ability to the player
        if (config.isPassiveAbility)
        {
            InitializePassiveAbility();
        }

        // Subscribe to the owner's damage-taken event for retaliation casting
        if (config.retaliationCast && ownerOrganism != null)
        {
            ownerOrganism.OnBlock += HandleRetaliationHit;
            ownerOrganism.OnDamageTaken += HandleRetaliationHit;
        }

        isInitialized = true;
    }

    private void Start()
    {
        // Initialize if not already done (fallback for player abilities)
        InitializeAbility();
    }

    private void InitializePassiveAbility()
    {
        PassiveAbility passiveAbility = null;
        PassiveConfig runtimePassiveConfig = config.passiveConfig;
        PassiveAbilityConfigBase passiveAsset = runtimePassiveConfig != null ? runtimePassiveConfig.PassiveAbility : null;

        // Preferred path: ScriptableObject-backed passive config resolves and creates runtime component.
        if (passiveAsset != null)
        {
            passiveAbility = passiveAsset.CreateRuntime(gameObject);
            if (passiveAbility == null)
            {
                Debug.LogError($"[DataDrivenAbility] Passive config '{passiveAsset.name}' failed to create runtime on '{gameObject.name}'.");
            }
        }

        // Backward compatibility: fall back to passiveTypeName if no passive asset was assigned.
        if (passiveAbility == null)
        {
            if (string.IsNullOrEmpty(config.passiveTypeName))
            {
                Debug.LogWarning($"[DataDrivenAbility] '{config.abilityName}' is marked isPassiveAbility but no passive config or passiveTypeName is set.");
                return;
            }

            System.Type passiveType = System.Type.GetType(config.passiveTypeName);
            if (passiveType == null)
            {
                Debug.LogError($"[DataDrivenAbility] Could not find passive type '{config.passiveTypeName}'");
                return;
            }

            passiveAbility = gameObject.AddComponent(passiveType) as PassiveAbility;
        }

        if (passiveAbility == null)
        {
            Debug.LogError($"[DataDrivenAbility] Failed to initialize passive runtime for '{config.abilityName}'.");
            return;
        }

        passiveAbility.Initialize(config, this, runtimePassiveConfig, passiveAsset);

    }
    /// <summary>
    /// Called when ability is activated - plays animations
    /// </summary>
    private void OnAbilityActivated()
    {
        // Take control if ability disables movement
        if (config != null && config.disablesMovementDuringCast)
        {
            float castSpeedMultiplier = Mathf.Clamp01(config.movementSpeedMultiplierDuringCast);
            if (castSpeedMultiplier < 1f)
                ApplyCastMoveSpeedModifier(castSpeedMultiplier, config.movementBlockDuration);
            else
                AcquireMovementControl(config.movementBlockDuration, $"{config.abilityName} cast lock");
        }

        PlayAbilityAnimations();
    }

    private void ApplyCastMoveSpeedModifier(float speedMultiplier, float duration)
    {
        if (ownerOrganism == null || ownerOrganism.AllStats == null || !ownerOrganism.AllStats.HasStat("MoveSpeed"))
        {
            Debug.LogWarning($"[DataDrivenAbility] Cannot apply cast move speed modifier: ownerOrganism or MoveSpeed stat is null. Ability={config?.abilityName}");
            return;
        }

        ClearCastMoveSpeedModifier("Reapply cast move speed modifier");

        float currentMoveSpeed = ownerOrganism.AllStats.GetStat("MoveSpeed");
        float targetMoveSpeed = currentMoveSpeed * speedMultiplier;
        castMoveSpeedAppliedDelta = targetMoveSpeed - currentMoveSpeed;

        if (Mathf.Abs(castMoveSpeedAppliedDelta) <= 0.0001f)
            return;
        Debug.Log($"[DataDrivenAbility] Applying cast move speed modifier: ability={config?.abilityName}, currentMoveSpeed={currentMoveSpeed:F3}, targetMoveSpeed={targetMoveSpeed:F3}, delta={castMoveSpeedAppliedDelta:F3}, duration={duration:F3}s");
        ownerOrganism.AllStats.ModifyStat("MoveSpeed", castMoveSpeedAppliedDelta);
        ownerOrganism.RefreshMoveSpeedFromStats();
        Debug.Log($"[DataDrivenAbility] Move speed after applying modifier: ability={config?.abilityName}, newMoveSpeed={ownerOrganism.AllStats.GetStat("MoveSpeed"):F3}");
        hasCastMoveSpeedModifier = true;

        if (castMoveSpeedReleaseCoroutine != null)
        {
            StopCoroutine(castMoveSpeedReleaseCoroutine);
            castMoveSpeedReleaseCoroutine = null;
        }

        if (duration > 0f)
            castMoveSpeedReleaseCoroutine = StartCoroutine(ClearCastMoveSpeedModifierAfterDelay(duration));

        Debug.Log($"[DataDrivenAbility] Applied cast move speed modifier: ability={config?.abilityName}, multiplier={speedMultiplier:F3}, delta={castMoveSpeedAppliedDelta:F3}, duration={duration:F3}s");
    }

    private IEnumerator ClearCastMoveSpeedModifierAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        ClearCastMoveSpeedModifier($"Cast speed modifier elapsed ({duration:F3}s)");
    }

    private void ClearCastMoveSpeedModifier(string reason)
    {
        if (castMoveSpeedReleaseCoroutine != null)
        {
            StopCoroutine(castMoveSpeedReleaseCoroutine);
            castMoveSpeedReleaseCoroutine = null;
        }

        if (!hasCastMoveSpeedModifier)
            return;

        if (ownerOrganism != null && ownerOrganism.AllStats != null)
        {
            Debug.Log($"[DataDrivenAbility] Move speed modifier before clearing: ability={config?.abilityName}, currentMoveSpeed={ownerOrganism.AllStats.GetStat("MoveSpeed"):F3}, delta={castMoveSpeedAppliedDelta:F3}");
            ownerOrganism.AllStats.ModifyStat("MoveSpeed", -castMoveSpeedAppliedDelta);
            Debug.Log($"[DataDrivenAbility] Cleared cast move speed modifier: ability={config?.abilityName}, delta={-castMoveSpeedAppliedDelta:F3}");
            ownerOrganism.RefreshMoveSpeedFromStats();
        }

        hasCastMoveSpeedModifier = false;
        castMoveSpeedAppliedDelta = 0f;
        Debug.Log($"[DataDrivenAbility] Cleared cast move speed modifier: reason={reason}, ability={config?.abilityName}");
    }

    private void AcquireMovementControl(float autoReleaseAfterSeconds, string reason)
    {
        Debug.Log("Enter Acquire movement control");
        movementControlLockToken++;
        int token = movementControlLockToken;
        playerControl = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (movementControlReleaseCoroutine != null)
        {
            StopCoroutine(movementControlReleaseCoroutine);
            movementControlReleaseCoroutine = null;
        }

        if (autoReleaseAfterSeconds > 0f)
            movementControlReleaseCoroutine = StartCoroutine(AutoReleaseMovementControl(token, autoReleaseAfterSeconds));

        Debug.Log($"[DataDrivenAbility] Movement control acquired: reason={reason}, token={token}, autoRelease={autoReleaseAfterSeconds:F3}s");
    }

    private IEnumerator AutoReleaseMovementControl(int token, float duration)
    {
        yield return new WaitForSeconds(duration);
        ReleaseMovementControl($"Timed lock elapsed ({duration:F3}s)", token);
    }

    private void ReleaseMovementControl(string reason, int expectedToken = -1, bool force = false)
    {
        if (!force && expectedToken >= 0 && expectedToken != movementControlLockToken)
            return;

        if (movementControlReleaseCoroutine != null)
        {
            StopCoroutine(movementControlReleaseCoroutine);
            movementControlReleaseCoroutine = null;
        }

        if (!playerControl)
            Debug.Log($"[DataDrivenAbility] Movement control released: reason={reason}");

        playerControl = true;
    }

    /// <summary>
    /// Play animations on character and weapon when ability is activated
    /// </summary>
    private void PlayAbilityAnimations()
    {
        if (config == null) return;

        // Calculate animation speed for attack abilities
        float animationSpeed = GetAnimationSpeed();

        // Play character animation if configured
        if (!string.IsNullOrEmpty(config.characterAnimationName))
        {
            Animator characterAnimator = GetComponent<Animator>();
            Debug.Log($"[DataDrivenAbility] Character animation check - config name: '{config.characterAnimationName}', animator found: {characterAnimator != null}");

            if (characterAnimator != null)
            {
                // Get aim direction to determine which animation to use
                Vector3 aimDir = GetAimDirection();
                float aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                if (aimAngle < 0) aimAngle += 360;

                // Use characterAnimationUp if aiming upward (15° to 165° range)
                string animationToPlay = config.characterAnimationName;

                if (aimAngle >= 15f && aimAngle < 165f && !string.IsNullOrEmpty(config.characterAnimationUp))
                {
                    animationToPlay = config.characterAnimationUp;
                }
                float attackSpeed = GetEffectiveAttackSpeed();
                PlayCharacterAnimationState(characterAnimator, animationToPlay, attackSpeed);
            }
        }

        // Spawn timed particles
        if (config.timedParticles != null && config.timedParticles.Count > 0)
        {
            StartCoroutine(SpawnTimedParticles());
        }
        bool useAlternating = ShouldUseAlternatingAnimations(out bool isSameWeaponAsset);

        // Determine which weapon holder to use and which animation name
        bool playOnMainhand = false;
        bool playOnOffhand = false;
        string animationNameToPlay = null;

        if (useAlternating)
        {
            if (lastAnimationWasMainhand)
            {
                playOnMainhand = false;
                playOnOffhand = true;
                animationNameToPlay = (isSameWeaponAsset || string.IsNullOrEmpty(config.offhandAnimationName))
                    ? config.mainhandAnimationName
                    : config.offhandAnimationName;
            }
            else
            {
                playOnMainhand = true;
                playOnOffhand = false;
                animationNameToPlay = config.mainhandAnimationName;
            }
        }
        else if (!string.IsNullOrEmpty(config.mainhandAnimationName))
        {
            playOnMainhand = true;
            playOnOffhand = false;
            animationNameToPlay = config.mainhandAnimationName;
            if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("mainhandAnimationName", out var cafAccum) && cafAccum.hasSetOverride)
            {
                animationNameToPlay = cafAccum.setString;
            }
        }

        if (playOnMainhand)
        {
            Debug.Log($"[DataDrivenAbility] Playing on MAINHAND weapon: {animationNameToPlay}");
            Animator mainhandAnimator = null;

            // Look for "WeaponHolder/Weapon" child transform
            Transform weaponTransform = transform.Find("WeaponHolder/Weapon");
            if (weaponTransform != null)
            {
                mainhandAnimator = GetWeaponAnimator(weaponTransform);
            }

            // Play the animation
            if (mainhandAnimator != null)
            {
                PlayerController player = GetComponent<PlayerController>();
                // Lock weapon to aimed direction BEFORE playing animation
                // This ensures the animation's position offsets are calculated from the unlocked angle
                if (config.unlockWeaponDirections)
                {
                    // Get RAW mouse angle from LaunchZone so barrel aims precisely at cursor
                    lockedWeaponAngle = GetAngleToMouseFromLaunchZone("WeaponHolder/Weapon");
                    isWeaponDirectionLocked = true;
                    isMainhandLocked = true;
                    isOffhandLocked = false;


                    // Force PlayerController to update weapon position immediately with the unlocked angle
                    // This ensures the weapon is positioned correctly BEFORE the animation starts
                    if (player != null)
                    {
                        player.ForceAnimationUpdate();
                    }

                    // Optionally lock weapon rotation for a duration (extends unlock period after ability finishes)
                    if (config.rotationLockDuration > 0f)
                    {
                        rotationLockEndTime = Time.time + config.rotationLockDuration;
                    }
                }
                else if (config.lockWeaponDirections)

                {
                    // A. Get the raw angle from the player pivot to the mouse cursor
                    float rawAngle = GetAngleToMouseFromLaunchZone("WeaponHolder/Weapon");

                    // B. Replicate your exact SnapToCardinalDirection logic here on frame zero
                    float absoluteAngle = Mathf.Abs(rawAngle);
                    if (absoluteAngle >= 0f && absoluteAngle <= 90f)
                    {
                        lockedWeaponAngle = 0f; // East (Right)
                    }
                    else
                    {
                        lockedWeaponAngle = -180f; // West (Left)
                    }

                    // C. Engage the direction lock using the snapped angle
                    isWeaponDirectionLocked = true;
                    isMainhandLocked = !playOnOffhand;
                    isOffhandLocked = playOnOffhand;

                    Debug.Log($"<color=magenta>[LockWeaponDirections] Snapped 360° weapon to 2-Directions! Snapped Angle: {lockedWeaponAngle}°</color>");

                    if (player != null)
                    {
                        player.ForceAnimationUpdate();
                    }
                    rotationLockEndTime = Time.time + (config.rotationLockDuration > 0f ? config.rotationLockDuration : 0.35f);
                }

                // NOW play the animation with the weapon at the correct angle
                PlayWeaponAnimationState(weaponTransform, animationNameToPlay, animationSpeed);

                if (useAlternating)
                {
                    // Mark that mainhand was used this time
                    lastAnimationWasMainhand = true;
                }

                // Cancel any existing idle return coroutine to prevent conflicts
                if (weaponIdleReturnCoroutine != null)
                {
                    StopCoroutine(weaponIdleReturnCoroutine);
                }

                // Schedule return to idle animation after shoot animation completes
                if (!string.IsNullOrEmpty(config.weaponIdleAnimationName))
                {
                    weaponIdleReturnCoroutine = StartCoroutine(ReturnWeaponToIdle(mainhandAnimator, animationNameToPlay, config.weaponIdleAnimationName, animationSpeed));
                }
            }
        }

        if (playOnOffhand)
        {
            Debug.Log($"[DataDrivenAbility] Playing on OFFHAND weapon: {animationNameToPlay}");
            Animator offhandAnimator = null;

            // Look for "OffHandWeaponHolder/OffHandWeapon" child transform
            Transform offhandWeaponTransform = transform.Find("OffHandWeaponHolder/OffHandWeapon");
            if (offhandWeaponTransform != null)
            {
                offhandAnimator = GetWeaponAnimator(offhandWeaponTransform);
            }

            // Play the animation
            if (offhandAnimator != null)
            {
                // Lock weapon to aimed direction BEFORE playing animation
                if (config.unlockWeaponDirections)
                {
                    // Get RAW mouse angle from LaunchZone so barrel aims precisely at cursor
                    lockedWeaponAngle = GetAngleToMouseFromLaunchZone("OffHandWeaponHolder/OffHandWeapon");
                    isWeaponDirectionLocked = true;
                    isMainhandLocked = false;
                    isOffhandLocked = true;

                    // Force PlayerController to update weapon position immediately with the unlocked angle
                    PlayerController player = GetComponent<PlayerController>();
                    if (player != null)
                    {
                        player.ForceAnimationUpdate();
                    }

                    // Optionally lock weapon rotation for a duration
                    if (config.rotationLockDuration > 0f)
                    {
                        rotationLockEndTime = Time.time + config.rotationLockDuration;
                    }
                }

                // NOW play the animation with the weapon at the correct angle
                PlayWeaponAnimationState(offhandWeaponTransform, animationNameToPlay, animationSpeed);

                if (useAlternating)
                {
                    // Mark that offhand was used this time
                    lastAnimationWasMainhand = false;
                }

                // Schedule return to idle animation after animation completes
                if (!string.IsNullOrEmpty(config.weaponIdleAnimationName))
                {
                    StartCoroutine(ReturnWeaponToIdle(offhandAnimator, animationNameToPlay, config.weaponIdleAnimationName, animationSpeed));
                }
            }
        }
        if (ownerAsPlayer)
        {
            ownerAsPlayer.CurrentAbilityState = PlayerController.AbilityState.Idle;
        }
    }


    /// <summary>
    /// Spawn particle effects at specified times during the ability
    /// </summary>
    private System.Collections.IEnumerator SpawnTimedParticles()
    {
        Vector3 aimDirection = GetAimDirection();

        foreach (var timedParticle in config.timedParticles)
        {
            if (timedParticle.particlePrefab == null) continue;

            // Wait for the specified time
            if (timedParticle.spawnTime > 0)
            {
                yield return new WaitForSeconds(timedParticle.spawnTime);
            }

            // Determine spawn position
            Vector3 spawnPosition = transform.position;
            Transform parentTransform = null;

            if (timedParticle.spawnAtWeapon)
            {
                // Spawn at weapon position
                Transform weaponTransform = transform.Find("WeaponHolder/Weapon");
                if (weaponTransform != null)
                {
                    spawnPosition = weaponTransform.position;
                    if (timedParticle.attachToSource)
                    {
                        parentTransform = weaponTransform;
                    }
                }
            }
            else if (timedParticle.spawnAtCharacter)
            {
                // Spawn at character position
                spawnPosition = transform.position;
                if (timedParticle.attachToSource)
                {
                    parentTransform = transform;
                }
            }

            // Apply offset
            spawnPosition += timedParticle.offset;

            // Determine rotation
            Quaternion rotation = Quaternion.identity;
            switch (timedParticle.rotationMode)
            {
                case ParticleRotationMode.Default:
                    rotation = timedParticle.particlePrefab.transform.rotation;
                    break;
                case ParticleRotationMode.FaceAimDirection:
                    float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                    rotation = Quaternion.Euler(0, 0, angle);
                    break;
                case ParticleRotationMode.Custom:
                    rotation = Quaternion.Euler(timedParticle.customRotation);
                    break;
            }

            // Instantiate particle
            ParticleSystem particleInstance = Instantiate(timedParticle.particlePrefab, spawnPosition, rotation);

            // Attach to parent if specified
            if (parentTransform != null)
            {
                particleInstance.transform.SetParent(parentTransform, true);
            }

            // Play the particle system
            particleInstance.Play();

            // Destroy after main module duration
            var main = particleInstance.main;
            Destroy(particleInstance.gameObject, main.duration + main.startLifetime.constantMax);
        }
    }

    /// <summary>
    /// Play pre-cast/pre-attack animation on weapon before ability fires
    /// </summary>
    private void PlayPreAnimation()
    {
        if (config == null) return;

        // Calculate animation speed (attack speed for attacks, cast speed for spells)
        float animationSpeed = GetAnimationSpeed();

        if (!string.IsNullOrEmpty(config.characterPrecastAnimationName))
        {
            Animator characterAnimator = GetComponent<Animator>();
            if (characterAnimator != null)
            {
                if (config.isAttack)
                {
                    float attackSpeed = GetEffectiveAttackSpeed();
                    PlayCharacterAnimationState(characterAnimator, config.characterPrecastAnimationName, attackSpeed);

                }
                else
                {
                    PlayCharacterAnimationState(characterAnimator, config.characterPrecastAnimationName);

                }

            }
        }
        if (string.IsNullOrEmpty(config.preAnimationName)) return;

        bool useAlternating = ShouldUseAlternatingAnimations(out _);
        bool playOnOffhand = useAlternating && lastAnimationWasMainhand;
        string weaponHolderPath = playOnOffhand ? "OffHandWeaponHolder/OffHandWeapon" : "WeaponHolder/Weapon";

        Animator weaponAnimator = null;
        Transform weaponTransform = transform.Find(weaponHolderPath);
        if (weaponTransform != null)
        {
            weaponAnimator = GetWeaponAnimator(weaponTransform);
        }

        // Play the pre animation
        if (weaponAnimator != null)
        {
            // Lock weapon to aimed direction BEFORE playing animation (for pre-animation)
            if (config.unlockWeaponDirections)
            {
                // Get RAW mouse angle from LaunchZone so barrel aims precisely at cursor
                lockedWeaponAngle = GetAngleToMouseFromLaunchZone(weaponHolderPath);
                isWeaponDirectionLocked = true;
                isMainhandLocked = !playOnOffhand;
                isOffhandLocked = playOnOffhand;

                // Force PlayerController to update weapon position immediately with the unlocked angle
                PlayerController player = GetComponent<PlayerController>();
                if (player != null)
                {
                    player.ForceAnimationUpdate();
                }
            }
            else if (config.lockWeaponDirections)
            {
                isWeaponDirectionLocked = true;
                isMainhandLocked = !playOnOffhand;
                isOffhandLocked = playOnOffhand;
                // Force PlayerController to update weapon position immediately with the locked angle
                PlayerController player = GetComponent<PlayerController>();
                if (player != null)
                {
                    player.ForceAnimationUpdate();
                }
            }
            if (ownerAsPlayer)
                ownerAsPlayer.CurrentAbilityState = PlayerController.AbilityState.Precast;
            // NOW play the pre-animation with the weapon at the correct angle
            PlayWeaponAnimationState(weaponTransform, config.preAnimationName, animationSpeed);
        }
    }

    private void PlayCharacterAnimationState(Animator characterAnimator, string stateName, float attackSpeed = 0)
    {
        if (characterAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        NetworkAnimator networkAnimator = characterAnimator.GetComponent<NetworkAnimator>();
        if (networkAnimator != null)
            networkAnimator.Play(stateName);
        else
            characterAnimator.Play(stateName, 0, 0f);
    }

    /// <summary>
    /// Play the hold animation on the weapon (looping while button is held).
    /// Called after precast completes when activateOnButtonRelease is enabled.
    /// </summary>
    private void PlayHoldAnimation()
    {
        if (config == null || string.IsNullOrEmpty(config.holdAnimationName)) return;

        Transform weaponTransform = transform.Find("WeaponHolder/Weapon");
        if (weaponTransform != null)
        {
            if (ownerAsPlayer)
                ownerAsPlayer.CurrentAbilityState = PlayerController.AbilityState.Holding;
            PlayWeaponAnimationState(weaponTransform, config.holdAnimationName, 1);
            Debug.Log($"<color=cyan>[DataDrivenAbility] {config.abilityName} - Playing hold animation: '{config.holdAnimationName}'</color>");
        }
    }

    /// <summary>
    /// Coroutine that waits for the ability button to be released.
    /// After precast finishes, plays the hold animation and loops until button release.
    /// </summary>
    private IEnumerator WaitForButtonRelease()
    {
        isHoldingForRelease = true;

        // Play hold animation (should be a looping animation)
        PlayHoldAnimation();

        Debug.Log($"<color=cyan>[DataDrivenAbility] {config.abilityName} - Waiting for button release (hold phase)</color>");

        // Wait until the button is released
        while (IsAbilityButtonHeld())
        {
            yield return null;
        }

        isHoldingForRelease = false;
        Debug.Log($"<color=cyan>[DataDrivenAbility] {config.abilityName} - Button released, proceeding to cast</color>");
    }

    /// <summary>
    /// Coroutine to return weapon to idle animation after shoot animation completes
    /// </summary>
    private System.Collections.IEnumerator ReturnWeaponToIdle(Animator weaponAnimator, string shootAnimName, string idleAnimName, float animSpeed)
    {
        if (weaponAnimator == null) yield break;

        // Wait a frame for the animation to start
        yield return null;

        // Get the current animation clip info
        AnimatorClipInfo[] clipInfo = weaponAnimator.GetCurrentAnimatorClipInfo(0);
        if (clipInfo.Length > 0)
        {
            // Get the animation length and account for animation speed
            float animationLength = clipInfo[0].clip.length / animSpeed;

            // Wait for animation to complete
            yield return new WaitForSeconds(animationLength);
        }
        else
        {
            // Fallback: wait a default time if we can't get clip info
            yield return new WaitForSeconds(0.5f / animSpeed);
        }

        // Return weapon to idle animation via NetworkAnimator when available
        // for reliable cross-client sync, falling back to direct Animator.Play in single-player.
        // Because NetworkAnimator and Animator are on the same WeaponSprite GameObject,
        // weaponAnimator.GetComponent<NetworkAnimator>() reliably finds it.
        if (weaponAnimator != null && !string.IsNullOrEmpty(idleAnimName))
        {
            NetworkAnimator netAnim = weaponAnimator.GetComponent<NetworkAnimator>();
            if (netAnim != null)
                netAnim.Play(idleAnimName);
            else
                weaponAnimator.Play(idleAnimName, 0, 0f);
        }

        // Release weapon direction lock when animation ends
        // This allows PlayerController to resume updating character animations
        bool wasLocked = isWeaponDirectionLocked;
        isWeaponDirectionLocked = false;
        isMainhandLocked = false;
        isOffhandLocked = false;
        rotationLockEndTime = 0f;

        if (wasLocked)
        {
            Debug.Log($"<color=green>[DataDrivenAbility] {config?.abilityName} animation complete - RELEASING weapon direction lock</color>");
            // Only force animation refresh if we're not mid-movement — if a movement ability
            // is still running it will call ForceAnimationUpdate when it finishes.
            bool midMovement = movementAbility != null && movementAbility.IsExecuting;
            if (!midMovement)
                ownerAsPlayer?.ForceAnimationUpdate();
        }

        weaponIdleReturnCoroutine = null;
    }

    /// <summary>
    /// Calculate precast delay from animation clip length, adjusted by attack/cast speed
    /// </summary>
    private float GetPrecastDelay()
    {
        if (config == null)
        {
            return 0f;
        }

        float animationSpeed = GetAnimationSpeed();
        float characterDelay = GetAnimationClipLength(GetComponent<Animator>(), config.characterPrecastAnimationName) / animationSpeed;

        if (string.IsNullOrEmpty(config.preAnimationName))
            return characterDelay;

        Transform weaponTransform = transform.Find("WeaponHolder/Weapon");
        if (weaponTransform == null)
        {
            return characterDelay;
        }
        Animator weaponAnimator = GetWeaponAnimator(weaponTransform);
        if (weaponAnimator == null || weaponAnimator.runtimeAnimatorController == null)
        {
            return characterDelay;
        }
        AnimationClip precastClip = null;
        RuntimeAnimatorController controller = weaponAnimator.runtimeAnimatorController;
        if (controller is AnimatorOverrideController overrideController)
        {
            var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            foreach (var pair in overrides)
            {
                if (pair.Value != null && pair.Value.name == config.preAnimationName)
                {
                    precastClip = pair.Value;
                    break;
                }
            }
            if (precastClip == null)
            {
                foreach (var pair in overrides)
                {
                    if (pair.Key != null && pair.Key.name == config.preAnimationName)
                    {
                        precastClip = pair.Value != null ? pair.Value : pair.Key;
                        break;
                    }
                }
            }
        }
        else
        {
            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip.name == config.preAnimationName)
                {
                    precastClip = clip;
                    break;
                }
            }
        }
        if (precastClip == null)
        {
            Debug.LogWarning($"[GetPrecastDelay] Precast animation '{config.preAnimationName}' not found in animator!");
            return characterDelay;
        }

        // Get base animation length
        float baseDelay = precastClip.length;
        // Adjust by animation speed (attack speed or cast speed)
        float adjustedDelay = baseDelay / animationSpeed;
        return Mathf.Max(characterDelay, adjustedDelay);
    }

    private static float GetAnimationClipLength(Animator animator, string animationName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(animationName))
            return 0f;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == animationName)
                return clip.length;
        }

        Debug.LogWarning($"[GetPrecastDelay] Character precast animation '{animationName}' not found in animator!");
        return 0f;
    }

    private float GetPrecastDelay(AbilityDataConfig abilityConfig)
    {
        if (abilityConfig == null)
            return 0f;

        AbilityDataConfig originalConfig = config;
        config = abilityConfig;
        float delay = GetPrecastDelay();
        config = originalConfig;
        return delay;
    }

    private bool ShouldDelayMovementForPrecast(AbilityDataConfig abilityConfig)
    {
        return abilityConfig != null
            && abilityConfig.isMovementAbility
            && abilityConfig.movementConfig != null
            && abilityConfig.movementConfig.activateAfterPrecast
            && abilityConfig.hasPrecast
            && HasConfiguredPrecastAnimation(abilityConfig);
    }

    private static bool HasConfiguredPrecastAnimation(AbilityDataConfig abilityConfig)
    {
        return abilityConfig != null
            && (!string.IsNullOrEmpty(abilityConfig.preAnimationName)
                || !string.IsNullOrEmpty(abilityConfig.characterPrecastAnimationName));
    }

    private float GetMovementPrecastDelay(AbilityDataConfig abilityConfig)
    {
        return ShouldDelayMovementForPrecast(abilityConfig) ? GetPrecastDelay(abilityConfig) : 0f;
    }

    /// <summary>
    /// Get animation speed multiplier based on attack speed (for attacks) or cast speed (for spells)
    /// </summary>
    private float GetAnimationSpeed()
    {
        if (config == null) return 1f;

        if (config.isAttack)
        {
            return GetEffectiveAttackSpeed();
        }
        else
        {
            // For spells, use cast speed
            float castSpeedBonus = 0f;
            // Get cast speed from owner's stats
            if (ownerOrganism != null && ownerOrganism.AllStats != null)
            {
                castSpeedBonus = ownerOrganism.AllStats.GetStat("CastSpeed");
            }

            // Convert to multiplier (0.03 = 1.03x, 0.5 = 1.5x, 2.0 = 3.0x)
            float castSpeedMultiplier = 1f + castSpeedBonus;

            return castSpeedMultiplier;
        }
    }

    /// <summary>
    /// <summary>
    /// Get the actual duration of a character animation, factored by attack/cast speed
    /// Used for combo timing to wait for animations to complete
    /// </summary>
    private float GetCharacterAnimationDuration(AbilityDataConfig abilityConfig)
    {
        if (abilityConfig == null || ownerOrganism == null)
        {
            Debug.LogWarning("[GetCharacterAnimationDuration] Missing config or owner organism");
            return 0.5f; // Fallback
        }

        // Get the character animator
        Animator characterAnimator = ownerOrganism.GetComponent<Animator>();
        if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[GetCharacterAnimationDuration] Character animator not found!");
            return 0.5f; // Fallback
        }

        // Determine which animation to check based on aim direction
        Vector2 aimDirection = GetAimDirection();
        float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        if (aimAngle < 0) aimAngle += 360;

        // Choose character animation based on angle (same logic as ability execution)
        string animationName = (aimAngle >= 15f && aimAngle < 165f && !string.IsNullOrEmpty(abilityConfig.characterAnimationUp))
            ? abilityConfig.characterAnimationUp
            : abilityConfig.characterAnimationName;

        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning($"[GetCharacterAnimationDuration] No animation name specified for {abilityConfig.abilityName}");
            return 0.5f; // Fallback
        }

        // Find the animation clip by name
        AnimationClip animClip = null;
        foreach (AnimationClip clip in characterAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == animationName)
            {

                animClip = clip;
                break;
            }
        }

        if (animClip == null)
        {
            Debug.LogWarning($"[GetCharacterAnimationDuration] Ability '{config.abilityName}' error");
            Debug.LogWarning($"[GetCharacterAnimationDuration] Animation '{animationName}' not found in character animator!");
            return 0.5f; // Fallback
        }

        // Get base animation length
        float baseLength = animClip.length;

        // Temporarily swap config to get animation speed for this specific ability
        AbilityDataConfig originalConfig = config;
        config = abilityConfig;
        float animationSpeed = GetAnimationSpeed();
        config = originalConfig;

        // Calculate actual duration factoring in attack/cast speed
        float adjustedDuration = baseLength / animationSpeed;

        Debug.Log($"[GetCharacterAnimationDuration] Animation '{animationName}': base={baseLength}s, speed={animationSpeed}x, adjusted={adjustedDuration}s");

        return adjustedDuration;
    }

    private void StartCooldown()
    {
        lastUsedTime = Time.time;

        if (config.hasCharges)
        {
            bool wasFull = currentCharges >= GetEffectiveMaxCharges();
            currentCharges = Mathf.Max(0, currentCharges - 1);
            // Only reset the recharge clock when going from fully-charged — keeps the oldest pending time
            if (wasFull)
                rechargeStartTime = Time.time;
            StartCoroutine(RechargeAbility());
        }
    }

    private void ConsumeMana()
    {
        float effectiveEnergyCost = GetEffectiveEnergyCost();
        if (ownerOrganism != null && effectiveEnergyCost > 0)
        {
            ownerOrganism.ModifyEnergy(-effectiveEnergyCost);
        }
    }

    private IEnumerator RechargeAbility()
    {
        yield return new WaitForSeconds(GetEffectiveRechargeTime());

        int effectiveMax = GetEffectiveMaxCharges();
        if (currentCharges < effectiveMax)
        {
            currentCharges++;
            Debug.Log($"{config.abilityName} recharged. Charges: {currentCharges}/{effectiveMax}");

            if (currentCharges < effectiveMax)
            {
                rechargeStartTime = Time.time; // Next charge recharge begins now
                StartCoroutine(RechargeAbility());
            }
            else
            {
                rechargeStartTime = -999f; // All charges restored
            }
        }
    }

    private int GetEffectiveMaxCharges()
    {
        int baseCharges = config?.maxCharges ?? 1;
        if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("maxCharges", out var accum))
        {
            baseCharges = (int)((baseCharges + accum.flatDelta) * (1f + accum.percentDelta / 100f));
            if (accum.hasSetOverride) baseCharges = (int)accum.setNumeric;
        }
        return Mathf.Max(1, baseCharges);
    }

    private float GetEffectiveRechargeTime()
    {
        float baseTime = config?.chargeRechargeTime ?? 1f;
        if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("chargeRechargeTime", out var accum))
        {
            baseTime = ApplyRateDurationModifiers(baseTime, accum);
            if (accum.hasSetOverride) baseTime = accum.setNumeric;
        }
        return Mathf.Max(0.1f, baseTime);
    }

    private static float ApplyRateDurationModifiers(float baseDuration, AbilityModifierRuntime.AccumulatedValue accum)
    {
        if (accum == null)
            return baseDuration;

        float flatAdjusted = baseDuration + accum.flatDelta;
        float denominator = Mathf.Max(0.01f, 1f + (accum.percentDelta / 100f));
        return flatAdjusted / denominator;
    }

    private ProjectileConfig GetEffectiveProjectileConfig() => _effectiveProjectileConfig ?? config?.projectileConfig;
    private AreaConfig GetEffectiveAreaConfig() => _effectiveAreaConfig ?? config?.areaConfig;
    private BeamAbilityConfig GetEffectiveBeamConfig() => _effectiveBeamConfig ?? config?.beamConfig;
    private MeleeConfig GetEffectiveMeleeConfig() => _effectiveMeleeConfig ?? config?.meleeConfig;
    private ExplosionConfig GetEffectiveExplosionConfig() => _effectiveExplosionConfig ?? config?.explosionConfig;
    private SummonConfig GetEffectiveSummonConfig() => _effectiveSummonConfig ?? config?.summonConfig;
    private ConstructConfig GetEffectiveConstructConfig() => _effectiveConstructConfig ?? config?.constructConfig;
    private HoldChargeConfig GetEffectiveHoldChargeConfig() => _effectiveHoldChargeConfig ?? config?.holdChargeConfig;

    private SubAbilityContext CreateSubAbilityContext() => new SubAbilityContext
    {
        rawParentConfig = config,
        parentConfig = EffectiveAbilityConfig,
        owner = gameObject,
        statOwner = gameObject
    };

    private bool ExecuteMovementAbility()
    {
        Debug.Log($"[Movement] ExecuteMovementAbility: ability={config?.abilityName}, movementAbility={movementAbility != null}, isExecuting={movementAbility?.IsExecuting}, playerControl={playerControl}, scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

        if (movementAbility == null)
        {
            Debug.LogError($"[Movement] MovementAbility component is null for {config?.abilityName} — was InitializeAbility called?");
            return false;
        }

        AbilityDataConfig effectiveConfig = EffectiveAbilityConfig;
        if (effectiveConfig?.movementConfig == null)
        {
            Debug.LogError($"[Movement] movementConfig is null on {config?.abilityName}");
            return false;
        }

        // Already mid-dash — the cooldown is shorter than the movement duration, so the
        // ability passed the cooldown gate but the previous execution hasn't finished yet.
        // Silently reject so the player hears no input, rather than logging a false error.
        if (movementAbility.IsExecuting)
            return false;


        float movementPrecastDelay = GetMovementPrecastDelay(effectiveConfig);
        if (movementPrecastDelay > 0f)
        {
            AbilityDataConfig pendingConfig = effectiveConfig;
            isMovementPrecastPending = true;
            PlayPreAnimation();
            StartCoroutine(ExecuteMovementAfterPrecast(movementPrecastDelay, pendingConfig));
            Debug.Log($"[Movement] Delaying movement execution for precast: ability={pendingConfig.abilityName}, delay={movementPrecastDelay:F3}s");
            return true;
        }

        bool success = movementAbility.Execute();
        if (success)
        {
            Debug.Log($"[Movement] Execute() succeeded — additive movement started, movementAbility.IsExecuting={movementAbility.IsExecuting}");
        }
        else
        {
            Debug.LogWarning($"[Movement] Execute() FAILED for {config?.abilityName} — movementAbility.IsExecuting={movementAbility.IsExecuting}");
        }
        return success;
    }

    private IEnumerator ExecuteMovementAfterPrecast(float delay, AbilityDataConfig movementConfig)
    {
        yield return new WaitForSeconds(delay);

        isMovementPrecastPending = false;

        if (movementConfig == null)
            yield break;

        AbilityDataConfig originalConfig = config;
        config = movementConfig;

        OnAbilityActivated();

        bool success = movementAbility != null && movementAbility.Execute();
        if (success)
        {
            StartCooldown();
            ConsumeMana();
            Debug.Log($"[Movement] Precast complete — movement started for {movementConfig.abilityName}");
        }
        else
        {
            Debug.LogError($"[Movement] Precast complete but movement failed to start for {movementConfig.abilityName}");
        }

        config = originalConfig;
    }

    #region Weapon Initialization

    private void InitializeBeamAbility()
    {
        // Check if BeamAbility component already exists
        beamAbility = GetComponent<BeamAbility>();

        if (beamAbility == null)
        {
            // Add BeamAbility component
            beamAbility = gameObject.AddComponent<BeamAbility>();
            Debug.Log("[DataDrivenAbility] BeamAbility component added");
        }
        else
        {
            Debug.Log("[DataDrivenAbility] BeamAbility component found");
        }

        // Initialize BeamAbility with effective config/context
        if (beamAbility != null && config != null)
        {
            AbilityDataConfig runtimeConfig = EffectiveAbilityConfig;
            Debug.Log($"[DataDrivenAbility] Calling BeamAbility.Initialize with config: {runtimeConfig.abilityName}, isBeamAbility={runtimeConfig.isBeamAbility}, beamConfig={runtimeConfig.beamConfig != null}");
            beamAbility.SetContext(CreateSubAbilityContext());
            beamAbility.Initialize(runtimeConfig);
        }
        else
        {
            Debug.LogError($"[DataDrivenAbility] Failed to initialize BeamAbility: beamAbility={beamAbility != null}, config={config != null}");
        }
    }

    private void InitializeChannelAbility()
    {
        // Check if ChannelAbility component already exists
        channelAbility = GetComponent<ChannelAbility>();

        if (channelAbility == null)
        {
            // Add ChannelAbility component
            channelAbility = gameObject.AddComponent<ChannelAbility>();
            Debug.Log("[DataDrivenAbility] ChannelAbility component added");
        }
        else
        {
            Debug.Log("[DataDrivenAbility] ChannelAbility component found");
        }

        // Initialize ChannelAbility with config
        if (channelAbility != null && config != null)
        {
            Debug.Log($"[DataDrivenAbility] Calling ChannelAbility.Initialize with config: {config.abilityName}, isChanneled={config.isChanneled}, channelConfig={config.channelConfig != null}");
            channelAbility.Initialize(config, abilitySlotIndex);
            // Pass the same hold-check function DataDrivenAbility uses so ChannelAbility
            // stops on exactly the correct input event regardless of binding.
            channelAbility.SetHoldChecker(() => IsAbilityButtonHeld());
        }
        else
        {
            Debug.LogError($"[DataDrivenAbility] Failed to initialize ChannelAbility: channelAbility={channelAbility != null}, config={config != null}");
        }
    }


    #endregion

    #region Ability Execution

    private bool CanUseAbility(out string reason)
    {
        reason = null;
        if (config == null)
        {
            reason = "config is null";
            return false;
        }
        // Only players have a cast state machine; enemies gate casts through their AI instead.
        if (!config.isInstant && ownerAsPlayer != null && (ownerAsPlayer.CurrentAbilityState == PlayerController.AbilityState.Executing || ownerAsPlayer.CurrentAbilityState == PlayerController.AbilityState.Precast))
        {
            reason = $"another ability is {ownerAsPlayer.CurrentAbilityState}";
            return false;
        }
        if (config.requiredWeaponTypes != null && config.requiredWeaponTypes.Count > 0)
        {
            if (!HasRequiredWeapons())
            {
                reason = $"missing required weapon types [{string.Join(", ", config.requiredWeaponTypes)}]";
                return false;
            }
        }

        if (config.hasCharges && currentCharges <= 0)
        {
            reason = $"no charges remaining (currentCharges={currentCharges})";
            Debug.Log($"{config.abilityName} has no charges remaining");
            return false;
        }

        // Follow-up combo steps ride the cooldown started by the opening step.
        bool isComboFollowUp = config.isCombo && currentComboIndex > 0 && Time.time <= comboWindowExpiresAt;
        if (!_autocastBurstActive && !isComboFollowUp && isOnCooldown)
        {
            reason = $"on cooldown (remaining={GetRemainingCooldown():F2}s, total={GetEffectiveCooldown():F2}s)";
            return false;
        }

        if (isMovementPrecastPending)
        {
            reason = "movement precast is still pending";
            return false;
        }

        if (isCharging)
        {
            reason = "ability is charging/casting";
            return false;
        }

        if (ownerEffectManager != null)
        {
            EffectConfig blockingEffect = ownerEffectManager.GetFirstAbilityBlockingEffect();
            if (blockingEffect != null)
            {
                reason = $"blocked by active effect '{blockingEffect.effectName}'";
                return false;
            }
        }

        float effectiveEnergyCost = GetEffectiveEnergyCost();
        if (ownerOrganism != null && effectiveEnergyCost > 0 && ownerOrganism.CurrentEnergy < effectiveEnergyCost)
        {
            reason = $"not enough energy (need={effectiveEnergyCost}, have={ownerOrganism.CurrentEnergy})";
            Debug.Log($"Not enough energy for {config.abilityName}. Need {effectiveEnergyCost}, have {ownerOrganism.CurrentEnergy}");
            return false;
        }

        return true;
    }

    public override bool TryUseAbility()
    {
        string abilityName = config != null ? config.abilityName : "<null config>";
        if (!CanUseAbility(out string blockedReason) || config == null)
        {
            Debug.Log($"Cannot use ability {abilityName}: {blockedReason}");
            return false;
        }

        if (isPlacingConstruct)
        {
            PlacementLog($"TryUseAbility early-exit: isPlacingConstruct=true, ability={abilityName}");
            return false;
        }

        if (config.isCombo)
        {
            if (isExecutingCombo || comboSteps == null || comboSteps.Length == 0)
            {
                return false;
            }

            if (Time.time > comboWindowExpiresAt || currentComboIndex >= comboSteps.Length)
                ResetComboChain();

            int stepIndex = Mathf.Clamp(currentComboIndex, 0, comboSteps.Length - 1);
            if (comboIdleWatcher != null)
            {
                StopCoroutine(comboIdleWatcher);
                comboIdleWatcher = null;
            }

            if (stepIndex == 0)
            {
                StartCooldown();
                ConsumeMana();
                lastFireTime = Time.time;
            }

            comboChainCoroutine = StartCoroutine(ExecuteComboChain(stepIndex));
            return true;
        }

        // Check ammo dependency 
        if (config.usesAmmo && (GetActiveAmmoConfig()?.dependsOnAmmo ?? true))
        {
            if (isReloading)
            {
                return false;
            }
            if (currentAmmo <= 0)
            {
                StartReload();
                return false;
            }
        }

        bool movementHasDelayedPrecast = GetMovementPrecastDelay(config) > 0f;

        // Immediate/Delayed Sequence path
        if (!string.IsNullOrEmpty(config.mainhandAnimationName))
        {
            chargingCoroutine = StartCoroutine(AbilityCastSequence());

            return true;
        }

        OnAbilityActivated();
        if (!isComboStep && !config.activateOnButtonRelease)
            isHoldingFire = true;
        bool abilityExecuted = FireAbility();

        if (abilityExecuted)
        {
            bool deferredCost = config.isConstructAbility && (config.constructConfig?.holdToPlace ?? false);
            if (!config.isBeamAbility && !config.isChanneled && !deferredCost && !movementHasDelayedPrecast)
            {
                StartCooldown();
                ConsumeMana();
                lastFireTime = Time.time;
            }
        }

        return abilityExecuted;

    }




    /// <summary>
    /// Execute all enabled ability types based on config flags
    /// </summary>
    private bool FireAbility()
    {
        bool abilityExecuted = false;
        // 8. Standalone Projectile (no weapon)

        if (config.isProjectileAbility)
        {
            abilityExecuted = ExecuteStandaloneProjectile() || abilityExecuted;
        }
        if (config.isMovementAbility)
        {
            abilityExecuted = ExecuteMovementAbility() || abilityExecuted;
        }
        // 3. Channeling Ability
        if (config.isChanneled)
        {
            abilityExecuted = ExecuteChanneledAbility() || abilityExecuted;
        }

        // 4. Beam Ability
        if (config.isBeamAbility)
        {
            abilityExecuted = ExecuteBeamAbility() || abilityExecuted;
        }

        // 5. Area Spell
        if (config.isAreaAbility)
        {
            abilityExecuted = ExecuteAreaAbility() || abilityExecuted;
        }

        // 6. Construct/Summon Ability
        if (config.isConstructAbility)
        {
            abilityExecuted = ExecuteConstructAbility() || abilityExecuted;
        }
        // 7. Trap Ability
        if (config.isTrapAbility)
        {
            abilityExecuted = ExecuteTrapAbility() || abilityExecuted;
        }
        // 9. Explosion Ability
        if (config.isExplosionAbility)
        {
            abilityExecuted = ExecuteExplosionAbility() || abilityExecuted;
        }
        // 10. Melee Ability
        if (config.isMeleeAbility)
        {
            abilityExecuted = ExecuteMeleeAbility() || abilityExecuted;
        }
        // 11. Summon Ability
        if (config.isSummonAbility)
        {
            abilityExecuted = ExecuteSummonAbility() || abilityExecuted;
        }
        if (abilityExecuted && config.isAttack)
            ownerAsPlayer?.NotifyAttack(config);

        return abilityExecuted;
    }

    private IEnumerator ExecuteComboChain(int startIndex)
    {
        isExecutingCombo = true;

        try
        {
            int stepIndex = startIndex;

            while (stepIndex >= 0 && stepIndex < comboSteps.Length)
            {
                DataDrivenAbility step = comboSteps[stepIndex];
                if (step == null)
                {
                    Debug.LogWarning($"[Combo] Step {stepIndex} of '{config.abilityName}' has no runner; aborting chain.");
                    ResetComboChain();
                    yield break;
                }

                Debug.Log($"[Combo] {config.abilityName} step {stepIndex + 1}/{comboSteps.Length}: {step.AbilityName}");

                if (!step.TryUseAbility())
                {
                    ResetComboChain();
                    yield break;
                }

                // The last step wraps back to the first, so a held button cycles the chain
                // instead of ending it. The pose only drops once the input window lapses.
                bool isLastStep = stepIndex >= comboSteps.Length - 1;
                currentComboIndex = isLastStep ? 0 : stepIndex + 1;
                comboWindowExpiresAt = Time.time + GetConfiguredComboInputWindow(config);

                // Gate the next step behind this step's cast time so a held button
                // cannot flush the whole chain in a single frame.
                float stepEndsAt = Time.time + GetComboStepDelay(stepIndex);
                while (Time.time < stepEndsAt)
                    yield return null;

                comboWindowExpiresAt = Time.time + GetConfiguredComboInputWindow(config);

                // Holding the button auto-advances; otherwise the player re-triggers the next
                // step manually via TryUseAbility while the input window is open.
                if (!IsAbilityButtonHeld())
                {
                    comboIdleWatcher = StartCoroutine(ReturnToIdleWhenComboWindowExpires(step));
                    yield break;
                }

                if (isLastStep)
                {
                    // A wrap costs the same as a fresh press.
                    if (!CanUseAbility(out string blockedReason))
                    {
                        Debug.Log($"[Combo] {config.abilityName} cannot cycle: {blockedReason}");
                        comboIdleWatcher = StartCoroutine(ReturnToIdleWhenComboWindowExpires(step));
                        yield break;
                    }

                    StartCooldown();
                    ConsumeMana();
                    lastFireTime = Time.time;
                    stepIndex = 0;
                }
                else
                {
                    stepIndex++;
                }
            }
        }
        finally
        {
            isExecutingCombo = false;
            comboChainCoroutine = null;
        }
    }

    /// <summary>
    /// Combo steps hold their final pose while the next step can still be triggered.
    /// Once the input window lapses, drop the pose using the shell's own idle animation.
    /// </summary>
    private IEnumerator ReturnToIdleWhenComboWindowExpires(DataDrivenAbility lastStep)
    {
        while (Time.time <= comboWindowExpiresAt)
            yield return null;

        ResetComboChain();
        lastStep?.ClearWeaponDirectionLock();
        ReturnToIdlePose();
        comboIdleWatcher = null;
    }

    /// <summary>
    /// Plays this ability's configured weapon idle animation on both hands.
    /// </summary>
    public void ReturnToIdlePose()
    {
        if (config == null || string.IsNullOrEmpty(config.weaponIdleAnimationName)) return;

        if (weaponIdleReturnCoroutine != null)
        {
            StopCoroutine(weaponIdleReturnCoroutine);
            weaponIdleReturnCoroutine = null;
        }

        PlayWeaponAnimationState(transform.Find("WeaponHolder/Weapon"), config.weaponIdleAnimationName, 1f);
        PlayWeaponAnimationState(transform.Find("OffHandWeaponHolder/OffHandWeapon"), config.weaponIdleAnimationName, 1f);

        Debug.Log($"[Combo] {config.abilityName} returned weapons to '{config.weaponIdleAnimationName}'.");
    }

    /// <summary>
    /// Releases the aim lock a cast pose was holding.
    /// </summary>
    public void ClearWeaponDirectionLock()
    {
        if (!isWeaponDirectionLocked) return;

        isWeaponDirectionLocked = false;
        isMainhandLocked = false;
        isOffhandLocked = false;
        rotationLockEndTime = 0f;
        ownerAsPlayer?.ForceAnimationUpdate();
    }

    /// <summary>
    /// Creates one runner component per combo step so each step casts through the normal,
    /// fully-initialized ability pipeline instead of the shell borrowing their configs.
    /// </summary>
    private void BuildComboSteps()
    {
        if (config.comboAbilities == null || config.comboAbilities.Length == 0)
            return;

        comboSteps = new DataDrivenAbility[config.comboAbilities.Length];

        for (int i = 0; i < config.comboAbilities.Length; i++)
        {
            AbilityDataConfig stepConfig = config.comboAbilities[i];
            if (stepConfig == null)
            {
                Debug.LogWarning($"[Combo] '{config.abilityName}' has a null combo step at index {i}.");
                continue;
            }

            if (stepConfig == config || stepConfig.isCombo)
            {
                Debug.LogError($"[Combo] Step {i} of '{config.abilityName}' points at a combo shell ('{stepConfig.abilityName}'); nested/self-referencing combos are not supported.");
                continue;
            }

            DataDrivenAbility step = gameObject.AddComponent<DataDrivenAbility>();
            step.SetAbilityReference(new AbilityReference(stepConfig));
            step.ConfigureAsComboStep(abilitySlotIndex);
            step.InitializeAbility();
            comboSteps[i] = step;
        }
    }

    private void DestroyComboSteps()
    {
        if (comboSteps == null) return;

        foreach (DataDrivenAbility step in comboSteps)
        {
            if (step != null)
                Destroy(step);
        }
        comboSteps = null;
    }

    private void ResetComboChain()
    {
        currentComboIndex = 0;
        comboWindowExpiresAt = -999f;
    }

    private float GetConfiguredComboInputWindow(AbilityDataConfig shellConfig)
    {
        float configuredWindow = shellConfig != null ? shellConfig.comboInputWindow : 0.75f;
        return Mathf.Max(0.05f, configuredWindow);
    }

    /// <summary>
    /// Wait time before the next step: the step's own cast time (already scaled by its
    /// attack/cast speed) plus the authored delay, which attack speed also shortens.
    /// </summary>
    private float GetComboStepDelay(int stepIndex)
    {
        float configuredDelay = 0.3f;
        if (config.comboStepDelays != null && stepIndex >= 0 && stepIndex < config.comboStepDelays.Length)
            configuredDelay = Mathf.Max(0f, config.comboStepDelays[stepIndex]);

        DataDrivenAbility step = comboSteps != null && stepIndex < comboSteps.Length ? comboSteps[stepIndex] : null;
        float castDuration = step != null ? step.GetCastDuration() : 0f;

        return castDuration + (configuredDelay / GetOwnerAttackSpeedMultiplier());
    }

    private float GetOwnerAttackSpeedMultiplier()
    {
        float bonus = ownerOrganism != null && ownerOrganism.AllStats != null
            ? ownerOrganism.AllStats.GetStat("AttackSpeed")
            : 0f;
        return Mathf.Max(0.01f, 1f + bonus);
    }

    /// <summary>
    /// How long this ability takes to resolve its cast, scaled by attack/cast speed.
    /// </summary>
    public float GetCastDuration()
    {
        if (config == null) return 0f;

        float castTime = GetPrecastDelay() + GetCharacterAnimationDuration(config);

        AbilityDataConfig effective = EffectiveAbilityConfig;
        if (effective != null && effective.isMovementAbility && effective.movementConfig != null)
            castTime = Mathf.Max(castTime, effective.movementConfig.duration + GetMovementPrecastDelay(effective));

        return castTime;
    }

    #endregion

    #region Standalone Projectile Logic

    private bool ExecuteStandaloneProjectile()
    {
        if (config.projectileConfig == null)
        {
            Debug.LogError("DataDrivenAbility: Projectile Config not set!");
            return false;
        }

        float damageMultiplier = isCharging ? config.projectileConfig.chargeDamageMultiplier : 1f;
        if (HasConfiguredCastAnimation(config))
        {
            StartCoroutine(SpawnStandaloneProjectileNextFrame(damageMultiplier));
            return true;
        }
        PerformProjectileShoot(damageMultiplier);
        return true;
    }
    private IEnumerator SpawnStandaloneProjectileNextFrame(float damageMultiplier)
    {
        yield return null;
        PerformProjectileShoot(damageMultiplier);
    }

    private static bool HasConfiguredCastAnimation(AbilityDataConfig abilityConfig)
    {
        return abilityConfig != null &&
               (!string.IsNullOrEmpty(abilityConfig.characterAnimationName)
                || !string.IsNullOrEmpty(abilityConfig.characterAnimationUp)
                || !string.IsNullOrEmpty(abilityConfig.mainhandAnimationName)
                || !string.IsNullOrEmpty(abilityConfig.offhandAnimationName));
    }

    /// <summary>
    /// Unified cast sequence for any ability type that has a precast animation or hold-to-charge config.
    /// Handles: precast → bar fill → hold phase → charge modifiers → fire → resources.
    /// Replaces the per-type delayed coroutines (ChargeProjectile, SpawnAreaAbilityDelayed, SpawnMeleeAttackDelayed).
    /// </summary>
    private IEnumerator AbilityCastSequence(bool consumeResourcesAtEnd = true)
    {
        _lastCastSequenceSucceeded = false;
        isCharging = true;
        chargeStartTime = Time.time;
        lastChargeValue = 0f;
        if (ownerAsPlayer)
            ownerAsPlayer.CurrentAbilityState = PlayerController.AbilityState.Precast;
        var hcc = GetEffectiveHoldChargeConfig();
        int maxBars = hcc != null ? Mathf.Max(1, hcc.maxBars) : 1;

        // 1. Precast animation
        if (config.hasPrecast && HasConfiguredPrecastAnimation(config))
            PlayPreAnimation();

        // 2. Precast duration — driven by animation clip length.
        // Start the charge bar at the same time if this is a hold-to-release ability with a charge config.
        float precastDuration = GetPrecastDelay();
        if (precastDuration > 0f)
        {
            if (config.activateOnButtonRelease && hcc != null)
                chargeBar?.StartCharge(hcc.barDuration, ownerAsPlayer?.transform, maxBars);
            yield return new WaitForSeconds(precastDuration);
        }

        // 3. Hold phase — continue charge bar and wait for button release
        if (config.activateOnButtonRelease)
        {
            if (IsAbilityButtonHeld())
            {
                if (hcc != null)
                {
                    // Continue from wherever precast left the bar
                    float startChargeLevel = precastDuration / hcc.barDuration;
                    if (ownerAsPlayer)
                        ownerAsPlayer.CurrentAbilityState = PlayerController.AbilityState.Holding;
                    yield return WaitForChargeRelease(hcc.barDuration, maxBars, startChargeLevel);
                }
                else
                {
                    if (ownerAsPlayer)
                        ownerAsPlayer.CurrentAbilityState = PlayerController.AbilityState.Holding;
                    yield return WaitForButtonRelease();
                }
            }
            else
            {
                lastChargeValue = hcc != null ? precastDuration / hcc.barDuration : 1f; // How far precast got
            }
        }
        else
        {
            lastChargeValue = precastDuration > 0f ? 1f : 0f;
        }

        if (!isCharging) yield break; // Cancelled externally

        chargeBar?.CompleteCharge();


        // 4. Patch effective sub-configs with scaled charge modifiers before firing
        var savedProjectile = _effectiveProjectileConfig;
        var savedMelee = _effectiveMeleeConfig;
        var savedArea = _effectiveAreaConfig;

        var chargeOverrides = BuildChargeAccumulatedOverrides(lastChargeValue);
        if (chargeOverrides != null)
        {
            var patchedProjectile = AbilityModifierRuntime.BuildEffectiveSubConfig(GetEffectiveProjectileConfig(), "projectileConfig", chargeOverrides);
            if (patchedProjectile != null) _effectiveProjectileConfig = patchedProjectile;

            var patchedMelee = AbilityModifierRuntime.BuildEffectiveSubConfig(GetEffectiveMeleeConfig(), "meleeConfig", chargeOverrides);
            if (patchedMelee != null) _effectiveMeleeConfig = patchedMelee;

            var patchedArea = AbilityModifierRuntime.BuildEffectiveSubConfig(GetEffectiveAreaConfig(), "areaConfig", chargeOverrides);
            if (patchedArea != null) _effectiveAreaConfig = patchedArea;
        }

        // 5. Fire all enabled ability types
        if (ownerAsPlayer)
            ownerAsPlayer.CurrentAbilityState = PlayerController.AbilityState.Executing;
        OnAbilityActivated();
        bool abilityExecuted = FireAbility();

        _lastCastSequenceSucceeded = abilityExecuted;

        if (abilityExecuted && !isComboStep && !config.activateOnButtonRelease)
            isHoldingFire = true;

        // 6. Restore sub-configs and consume resources
        _effectiveProjectileConfig = savedProjectile;
        _effectiveMeleeConfig = savedMelee;
        _effectiveAreaConfig = savedArea;

        if (consumeResourcesAtEnd)
        {
            ConsumeMana();
            StartCooldown();
            lastFireTime = Time.time;
        }
        if (ownerAsPlayer)
            ownerAsPlayer.CurrentAbilityState = PlayerController.AbilityState.Idle;
        isCharging = false;
        chargeBar?.StopCharge();
        chargingCoroutine = null;
    }

    /// <summary>
    /// Hold phase for abilities with HoldChargeConfig.
    /// Tracks multi-bar progress and records lastChargeValue at button release.
    /// </summary>
    private IEnumerator WaitForChargeRelease(float barDuration, int maxTotalBars, float startChargeLevel = 1f)
    {
        isHoldingForRelease = true;
        PlayHoldAnimation();
        float holdStart = Time.time;

        while (IsAbilityButtonHeld())
        {
            float elapsed = Time.time - holdStart;
            float chargeLevel = Mathf.Clamp(startChargeLevel + elapsed / barDuration, startChargeLevel, maxTotalBars);
            chargeBar?.UpdateHoldPhase(chargeLevel, maxTotalBars);
            yield return null;
        }

        float totalElapsed = Time.time - holdStart;
        lastChargeValue = Mathf.Clamp(startChargeLevel + totalElapsed / barDuration, startChargeLevel, maxTotalBars);
        isHoldingForRelease = false;
    }

    /// <summary>
    /// Converts HoldChargeConfig modifiers scaled by chargeLevel into an AccumulatedValue
    /// dictionary compatible with AbilityModifierRuntime.BuildEffectiveSubConfig.
    /// Returns null when there are no modifiers or chargeLevel is 0.
    /// </summary>
    private Dictionary<string, AbilityModifierRuntime.AccumulatedValue> BuildChargeAccumulatedOverrides(float chargeLevel)
    {
        var hcc = GetEffectiveHoldChargeConfig();
        if (hcc?.modifiers == null || hcc.modifiers.Count == 0 || chargeLevel <= 0f)
            return null;

        Dictionary<string, AbilityModifierRuntime.AccumulatedValue> result = null;

        foreach (var mod in hcc.modifiers)
        {
            Debug.Log("[charged projectile] Processing modifier: " + mod.propertyPath);
            if (string.IsNullOrEmpty(mod.propertyPath) || mod.valuePerBar == 0f) continue;

            float effectiveLevel = mod.allowFractional ? chargeLevel : Mathf.Floor(chargeLevel);
            float totalValue = effectiveLevel * mod.valuePerBar;
            if (totalValue == 0f) continue;

            result ??= new Dictionary<string, AbilityModifierRuntime.AccumulatedValue>();

            if (!result.TryGetValue(mod.propertyPath, out var acc))
            {
                acc = new AbilityModifierRuntime.AccumulatedValue();
                result[mod.propertyPath] = acc;
            }

            acc.Apply(new AbilityPropertyOverride
            {
                propertyPath = mod.abilityType + "." + mod.propertyPath,
                overrideMode = mod.overrideMode,
                numericValue = totalValue
            });
            Debug.Log($"[charged projectile] Applied modifier: {mod.propertyPath}, totalValue: {totalValue}");
        }
        Debug.Log($"[DataDrivenAbility] Built charge accumulated overrides for charge level: {chargeLevel}, result: {result}");
        return result;
    }

    /// <summary>
    /// Returns the bar duration for the charge phase.
    /// Uses holdChargeConfig.barDuration when set; otherwise falls back to the precast animation length.
    /// </summary>
    private float GetHoldChargeBarDuration()
    {
        var hcc = GetEffectiveHoldChargeConfig();
        if (hcc != null && hcc.barDuration > 0f)
            return hcc.barDuration;
        return GetPrecastDelay();
    }

    private void StopContinuousFiring()
    {
        isHoldingFire = false;

        if (comboIdleWatcher != null)
        {
            StopCoroutine(comboIdleWatcher);
            comboIdleWatcher = null;
        }

        // Stop combo if active
        if (isExecutingCombo)
        {
            Debug.Log($"[DataDrivenAbility] Stopping combo execution");
            isExecutingCombo = false;
            if (comboChainCoroutine != null)
            {
                StopCoroutine(comboChainCoroutine);
                comboChainCoroutine = null;
            }
            ResetComboChain();
        }

        // Cancel charging if can cancel — but NOT if we're in hold-for-release mode
        // (releasing the button is the trigger to fire, not cancel)
        if (isCharging && !isHoldingForRelease && config.projectileConfig != null && config.projectileConfig.canCancelCharge)
        {
            if (chargingCoroutine != null)
            {
                StopCoroutine(chargingCoroutine);
                chargingCoroutine = null;
            }
            isCharging = false;
        }

        // Cancel any ongoing weapon activation
        if (weaponActivationCoroutine != null)
        {
            StopCoroutine(weaponActivationCoroutine);
            weaponActivationCoroutine = null;
            isActivatingWeapon = false;
        }
    }

    private void PerformProjectileShoot(float damageMultiplier = 1f, bool consumeAmmo = true)
    {
        if (config == null || config.projectileConfig == null) return;

        Debug.Log($"{AbilityPipelineTag} PerformProjectileShoot: ability={config.abilityName}, slot={abilitySlotIndex}, damageMultiplier={damageMultiplier:F3}, usesAmmo={config.usesAmmo}, currentAmmo={currentAmmo}, autocast={config.autocast}");

        // Try to consume ammo
        if (consumeAmmo && config.usesAmmo)
        {
            if (currentAmmo <= 0)
            {
                StartReload();
                return;
            }

            // Dual-wielding the same weapon type alternates shots between hands (see
            // ShouldUseAlternatingAnimations/PlayAbilityAnimations). lastAnimationWasMainhand
            // is updated for THIS shot by PlayAbilityAnimations (called via OnAbilityActivated,
            // which always runs before FireAbility/PerformProjectileShoot), so it reliably
            // tells us which hand just fired. Only consuming ammo on the mainhand half of the
            // alternation halves the effective ammo cost per shot — two weapons sharing one pool.
            bool consumesAmmoThisShot = !ShouldUseAlternatingAnimations() || lastAnimationWasMainhand;
            if (consumesAmmoThisShot)
            {
                currentAmmo--;

                // Trigger reload if this was the last shot
                if (currentAmmo == 0)
                {
                    StartReload();
                }
            }
        }

        // Charge damage multiplier (if applicable)
        Debug.Log($"[DmgPipeline] <{config.abilityName}> Projectile damageMultiplier={damageMultiplier:F3}x");

        ProjectileConfig effectiveProjectileConfig = GetEffectiveProjectileConfig();
        if (effectiveProjectileConfig == null)
            return;

        bool isAutocastProjectile = config.autocast;

        // Alternating dual-wield: resolve which hand fires THIS shot so the spawn/launch
        // origin, projectile prefab override, and weapon-specific FX overrides all come from
        // the correct weapon instead of always defaulting to mainhand.
        bool firedFromOffhand = IsCurrentShotFromOffhand();

        Vector3 spawnPos = transform.position;

        // Try to find weapon LaunchZone for more precise spawn position.
        // Autocast must not use launch-zone data.
        Transform weaponTransform = GetActiveWeaponTransform(firedFromOffhand);
        if (!isAutocastProjectile && weaponTransform != null)
        {
            spawnPos = WeaponLaunchPoint.GetLaunchPosition(weaponTransform);
        }

        Vector3 direction = ResolveProjectileFireDirection(spawnPos, weaponTransform, effectiveProjectileConfig, isAutocastProjectile);

        // Log the positions and direction for debugging
        string ownerType = ownerAsPlayer != null ? "Player" : "Enemy";
        Debug.Log($"<color=orange>[ProjectileFire] {ownerType} firing: SpawnPos: {spawnPos}, Direction: {direction} (angle: {Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg:F1}°), firedFromOffhand: {firedFromOffhand}</color>");

        // Check for weapon projectile override  
        GameObject projectileOverride = GetWeaponProjectileOverride(weaponTransform, isAutocastProjectile, firedFromOffhand);
        WeaponConfig weaponConfig = GetCurrentWeaponConfig(firedFromOffhand);

        // NETWORK SUPPORT: DataDrivenAbility is added via AddComponent at runtime, so FishNet
        // never registers a NetworkObject for it — [ServerRpc] on this component is a no-op.
        // Instead we check network state via InstanceFinder and route through PlayerController
        // (ownerAsPlayer), which IS properly registered and can carry ServerRpc calls.
        var nm = InstanceFinder.NetworkManager;
        bool isNetworkActive = nm != null && (nm.IsServerStarted || nm.IsClientStarted);
        bool isOwner = ownerAsPlayer != null ? ownerAsPlayer.IsOwner : false;
        bool isServer = nm != null && nm.IsServerStarted;

        if (isNetworkActive)
        {
            // In multiplayer: request server to spawn the projectile so all clients can see it
            if (isOwner)
            {
                Debug.Log($"{AbilityPipelineTag} Projectile path: owner predictive + ServerRpc, ability={config.abilityName}, slot={abilitySlotIndex}");
                // IMMEDIATE OWNER FEEDBACK is only needed on non-host clients.
                // In host mode the authoritative server spawn runs locally and already renders
                // this shot, so predictive visuals would duplicate muzzle/projectile effects.
                bool shouldUsePredictiveVisuals = !isServer;
                int salvoSizeOwner = effectiveProjectileConfig.salvoSize;
                float salvoIntervalOwner = effectiveProjectileConfig.salvoInterval;
                float salvoAngleOwner = effectiveProjectileConfig.salvoAngle;
                uint clientTick = InstanceFinder.TimeManager.Tick;
                GameObject capturedOverrideOwner = projectileOverride;
                WeaponConfig capturedWeaponCfgOwner = weaponConfig;
                if (shouldUsePredictiveVisuals)
                {
                    float muzzleAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    SpawnMuzzleFlashLocally(spawnPos, muzzleAngle);

                    if (salvoSizeOwner <= 1)
                        SpawnPredictiveProjectile(spawnPos, direction, capturedOverrideOwner, capturedWeaponCfgOwner);
                    else
                        StartCoroutine(SalvoCoroutine(
                            salvoDirection => SpawnPredictiveProjectile(spawnPos, salvoDirection, capturedOverrideOwner, capturedWeaponCfgOwner),
                            direction, salvoSizeOwner, salvoIntervalOwner, salvoAngleOwner, clientTick));
                }

                // Server authoritative spawn — handles hits, damage and observer replication.
                // Pass current TimeManager.Tick so the server can compute how much time elapsed
                // between the client firing and the server processing the RPC (latency compensation).
                // firedFromOffhand is forwarded so the server resolves the same weapon (and its
                // launch-zone/prefab overrides) the client used, instead of always assuming mainhand.
                ownerAsPlayer.ServerRpcSpawnAbilityProjectile(abilitySlotIndex, config.abilityName, spawnPos, direction, damageMultiplier, clientTick, firedFromOffhand);
            }
            else if (isServer)
            {
                Debug.Log($"{AbilityPipelineTag} Projectile path: server direct spawn, ability={config.abilityName}, slot={abilitySlotIndex}");
                // Server (for NPCs/enemies) spawns directly
                int salvoSizeServer = effectiveProjectileConfig.salvoSize;
                float salvoIntervalServer = effectiveProjectileConfig.salvoInterval;
                float salvoAngleServer = effectiveProjectileConfig.salvoAngle;
                GameObject capturedOverrideServer = projectileOverride;
                WeaponConfig capturedWeaponCfgServer = weaponConfig;
                if (salvoSizeServer <= 1)
                    SpawnProjectileOnServer(spawnPos, direction, damageMultiplier, capturedOverrideServer, capturedWeaponCfgServer);
                else
                    StartCoroutine(SalvoCoroutine(
                        salvoDirection => SpawnProjectileOnServer(spawnPos, salvoDirection, damageMultiplier, capturedOverrideServer, capturedWeaponCfgServer),
                        direction, salvoSizeServer, salvoIntervalServer, salvoAngleServer, (uint)Time.frameCount));
            }
        }
        else
        {
            Debug.Log($"{AbilityPipelineTag} Projectile path: local single-player spawn, ability={config.abilityName}, slot={abilitySlotIndex}");
            // Single-player: spawn locally
            int salvoSizeLocal = effectiveProjectileConfig.salvoSize;
            float salvoIntervalLocal = effectiveProjectileConfig.salvoInterval;
            float salvoAngleLocal = effectiveProjectileConfig.salvoAngle;
            if (salvoSizeLocal <= 1)
                SpawnProjectileLocally(spawnPos, direction, damageMultiplier, projectileOverride, weaponConfig);
            else
                StartCoroutine(SalvoCoroutine(
                    salvoDirection => SpawnProjectileLocally(spawnPos, salvoDirection, damageMultiplier, projectileOverride, weaponConfig),
                    direction, salvoSizeLocal, salvoIntervalLocal, salvoAngleLocal, (uint)Time.frameCount));
        }
    }

    private Vector3 ResolveProjectileFireDirection(Vector3 spawnPos, Transform weaponTransform, ProjectileConfig projectileConfig, bool isAutocastProjectile)
    {
        if (_autocastTarget.HasValue)
        {
            Vector3 autocastDirection = _autocastTarget.Value - spawnPos;
            if (autocastDirection.sqrMagnitude > 0.0001f)
                return autocastDirection.normalized;
        }

        if (ownerAsPlayer != null && CursorManager.Instance?.TargetedOrganism != null)
        {
            Vector3 targetDirection = CursorManager.Instance.TargetedOrganism.transform.position - spawnPos;
            if (targetDirection.sqrMagnitude > 0.0001f)
                return targetDirection.normalized;
        }

        Vector3 targetWorldPos = GetTargetWorldPosition();
        Vector3 fallbackDirection = targetWorldPos - spawnPos;
        if (fallbackDirection.sqrMagnitude <= 0.0001f)
            return Vector3.right;

        return fallbackDirection.normalized;
    }

    /// <summary>
    /// ServerRpc called by clients to request projectile spawn on server.
    /// NOTE: This is kept for reference but is NOT used at runtime because DataDrivenAbility
    /// is added via AddComponent post-spawn and has no registered NetworkObject.
    /// The actual RPC path is: client calls ownerAsPlayer.ServerRpcSpawnAbilityProjectile()
    /// which calls ExecuteServerSpawn() on this component server-side.
    /// </summary>
    [ServerRpc]
    private void ServerSpawnProjectile(Vector3 spawnPos, Vector3 direction, float damageMultiplier, GameObject projectileOverride)
    {
        // Server spawns the projectile with network authority
        WeaponConfig weaponConfig = GetCurrentWeaponConfig();
        SpawnProjectileOnServer(spawnPos, direction, damageMultiplier, projectileOverride, weaponConfig);
    }

    /// <summary>
    /// Called server-side by PlayerController.ServerRpcSpawnAbilityProjectile to execute
    /// the actual projectile spawn with the correct weapon config resolved on the server.
    /// tick is the client-side tick at spawn time, used for latency compensation.
    /// firedFromOffhand mirrors the client's alternating-fire hand choice for this shot (see
    /// IsCurrentShotFromOffhand) so the server picks the same weapon/launch-zone overrides.
    /// </summary>
    public void ExecuteServerSpawn(Vector3 spawnPos, Vector3 direction, float damageMultiplier, uint tick, bool firedFromOffhand = false)
    {
        // Tick is forwarded to RpcClientInitialize so observers could potentially
        // use it in the future, but position fast-forward is disabled for now
        // because the fast leap was overshooting for short-range projectiles.

        WeaponConfig weaponConfig = GetCurrentWeaponConfig(firedFromOffhand);
        Transform weaponTransform = GetActiveWeaponTransform(firedFromOffhand);
        GameObject projectileOverride = GetWeaponProjectileOverride(weaponTransform, config != null && config.autocast, firedFromOffhand);
        ProjectileConfig projCfgForSalvo = GetEffectiveProjectileConfig();
        int salvoSizeExec = projCfgForSalvo?.salvoSize ?? 1;
        float salvoIntervalExec = projCfgForSalvo?.salvoInterval ?? 0.15f;
        float salvoAngleExec = projCfgForSalvo?.salvoAngle ?? 0f;
        GameObject capturedOverrideExec = projectileOverride;
        WeaponConfig capturedWeaponCfgExec = weaponConfig;
        uint capturedTick = tick;
        if (salvoSizeExec <= 1)
            SpawnProjectileOnServer(spawnPos, direction, damageMultiplier, capturedOverrideExec, capturedWeaponCfgExec, 0f, capturedTick);
        else
            StartCoroutine(SalvoCoroutine(
                salvoDirection => SpawnProjectileOnServer(spawnPos, salvoDirection, damageMultiplier, capturedOverrideExec, capturedWeaponCfgExec, 0f, capturedTick),
                direction, salvoSizeExec, salvoIntervalExec, salvoAngleExec, capturedTick));

        // Broadcast muzzle flash to non-server clients.
        // The server already spawned the flash locally via ProjectileSpawner.SpawnMuzzleFlash.
        // Non-server clients use SpawnMuzzleFlashLocally which reads from the ScriptableObject config,
        // avoiding the null-ref that plagues Projectile.RpcSpawnMuzzleFlash (server-only InitializeFromConfig).
        if (ownerAsPlayer != null && InstanceFinder.IsServerStarted)
        {
            float muzzleAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            ownerAsPlayer.ObserversRpcSpawnMuzzleFlash(abilitySlotIndex, config.abilityName, spawnPos, muzzleAngle, firedFromOffhand);
        }
    }

    /// <summary>
    /// Spawn projectile on server (called by ServerRpc or directly if server)
    /// </summary>
    private void SpawnProjectileOnServer(Vector3 spawnPos, Vector3 direction, float damageMultiplier, GameObject projectileOverride, WeaponConfig weaponConfig, float passedTime = 0f, uint tick = 0)
    {
        SpawnProjectileLocally(spawnPos, direction, damageMultiplier, projectileOverride, weaponConfig, passedTime, tick);
    }

    /// <summary>
    /// Spawns a cosmetic-only "predictive" projectile immediately on the owner so they see
    /// zero-latency visual feedback while the ServerRpc round-trip to spawn the authoritative
    /// projectile completes.  The clone moves identically to the real one but has its collider
    /// disabled and auto-destroys after 0.5 s (well before any realistic RTT + travel time).
    /// </summary>
    private void SpawnPredictiveProjectile(Vector3 spawnPos, Vector3 direction, GameObject projectileOverride, WeaponConfig weaponConfig)
    {
        ProjectileConfig projCfg = GetEffectiveProjectileConfig();
        if (projCfg == null) return;

        // Mirror the same config-merging logic used by SpawnProjectileLocally so the
        // predictive clone uses the correct prefab and movement settings.
        ProjectileConfig configToUse = projCfg;
        bool hasWeaponOverrides = projCfg.allowOverride && (projectileOverride != null ||
            (weaponConfig != null && (weaponConfig.muzzleFlashOverride != null ||
                                      weaponConfig.overrideMuzzleLight ||
                                      weaponConfig.overrideHitEffects)));
        if (hasWeaponOverrides)
        {
            GameObject prefabSrc = projectileOverride != null ? projectileOverride : projCfg.hitbox.prefab;
            configToUse = CreateConfigWithWeaponOverrides(projCfg, prefabSrc, weaponConfig);
        }

        GameObject prefab = configToUse.hitbox.prefab;
        if (prefab == null) return;

        GameObject obj = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
        Projectile proj = obj.GetComponent<Projectile>();
        if (proj == null) { Object.Destroy(obj); return; }

        // Populate movement fields (speed, behavior, etc.) from config, then mark as
        // predictive before Initialize so the collider is off from the start.
        proj.InitializeFromConfig(configToUse);
        proj.SetupAsPredictive();
        proj.Initialize(spawnPos, direction, configToUse.speed > 0f ? configToUse.speed : -1f);
    }

    /// <summary>
    /// Spawns muzzle flash VFX locally using ScriptableObject asset references available on all clients.
    /// Called on non-server clients via PlayerController.ObserversRpcSpawnMuzzleFlash.
    /// </summary>
    public void SpawnMuzzleFlashLocally(Vector3 position, float angle, bool firedFromOffhand = false)
    {
        ProjectileConfig baseProjectileConfig = GetEffectiveProjectileConfig();
        if (baseProjectileConfig == null)
        {
            Debug.LogWarning($"[MuzzleFlashTrace][Local-Resolve] ability={config?.abilityName ?? "<null>"}, owner={gameObject.name}, reason=NoEffectiveProjectileConfig");
            return;
        }

        Transform weaponTransform = GetActiveWeaponTransform(firedFromOffhand);
        GameObject projectileOverride = GetWeaponProjectileOverride(weaponTransform, config != null && config.autocast, firedFromOffhand);
        WeaponConfig weaponConfig = GetCurrentWeaponConfig(firedFromOffhand);

        ProjectileConfig flashConfig = baseProjectileConfig;
        bool hasWeaponOverrides = baseProjectileConfig.allowOverride &&
                                  (projectileOverride != null ||
                                   (weaponConfig != null && (weaponConfig.muzzleFlashOverride != null ||
                                                             weaponConfig.overrideMuzzleLight ||
                                                             weaponConfig.overrideHitEffects)));
        if (hasWeaponOverrides)
        {
            GameObject prefabToUse = projectileOverride != null ? projectileOverride : baseProjectileConfig.hitbox.prefab;
            flashConfig = CreateConfigWithWeaponOverrides(baseProjectileConfig, prefabToUse, weaponConfig);
        }

        // Match ProjectileSpawner behavior: projectile-prefab-level overrides can define
        // muzzle flash even when the base/weapon config leaves it empty.
        GameObject flashProjectilePrefab = flashConfig != null && flashConfig.hitbox != null ? flashConfig.hitbox.prefab : null;
        ProjectilePrefab projectilePrefabComponent = flashProjectilePrefab != null ? flashProjectilePrefab.GetComponent<ProjectilePrefab>() : null;
        if (projectilePrefabComponent != null)
            flashConfig = projectilePrefabComponent.ApplyOverrides(flashConfig);

        GameObject flashPrefab = flashConfig.muzzleFlashPrefab;
        if (flashPrefab == null)
        {
            Debug.LogWarning($"[MuzzleFlashTrace][Local-Resolve] ability={config?.abilityName ?? "<null>"}, owner={gameObject.name}, reason=ResolvedMuzzlePrefabNull");
            return;
        }

        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        bool shouldFlipY = Mathf.Abs(angle) > 90f;
        if (flashConfig.localMuzzle)
        {
            ProjectileSpawner.InstantiateMuzzleFlashRoot(flashPrefab, position, rotation, weaponTransform, shouldFlipY);
        }
        else
        {
            ProjectileSpawner.InstantiateMuzzleFlashWorld(flashPrefab, position, rotation, weaponTransform, shouldFlipY);

        }

        // Optional point light burst
        if (flashConfig.enableMuzzleLight)
        {
            GameObject lightObj = new GameObject("MuzzleFlashLight");
            lightObj.transform.position = position;

            if (weaponTransform != null)
                lightObj.transform.SetParent(weaponTransform, true);

            Light2D light2D = lightObj.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Point;
            light2D.color = flashConfig.muzzleLightColor;
            light2D.intensity = flashConfig.muzzleLightIntensity;
            light2D.pointLightOuterRadius = flashConfig.muzzleLightRange;
            MuzzleLightFader fader = lightObj.AddComponent<MuzzleLightFader>();
            fader.Initialize(flashConfig.muzzleLightDuration);
        }
    }

    /// <summary>
    /// Local projectile spawning logic (used by both single-player and server)
    /// </summary>
    private void SpawnProjectileLocally(Vector3 spawnPos, Vector3 direction, float damageMultiplier, GameObject projectileOverride, WeaponConfig weaponConfig, float passedTime = 0f, uint tick = 0)
    {
        // Check for weapon projectile override (trait-modified base config is already baked in)
        ProjectileConfig baseProjectileConfig = GetEffectiveProjectileConfig();
        ProjectileConfig projectileConfigToUse = baseProjectileConfig;

        // Apply weapon overrides if available (projectile prefab, muzzle flash, or hit effects)
        // Only apply overrides if allowOverride is true in the projectile config
        bool hasWeaponOverrides = baseProjectileConfig.allowOverride &&
                                  (projectileOverride != null ||
                                   (weaponConfig != null && (weaponConfig.muzzleFlashOverride != null ||
                                                             weaponConfig.overrideMuzzleLight ||
                                                             weaponConfig.overrideHitEffects)));

        if (hasWeaponOverrides)
        {
            // Create a copy of the config with all weapon overrides applied
            // Use original projectile prefab if no override exists
            GameObject prefabToUse = projectileOverride != null ? projectileOverride : baseProjectileConfig.hitbox.prefab;
            projectileConfigToUse = CreateConfigWithWeaponOverrides(baseProjectileConfig, prefabToUse, weaponConfig);
        }

        // Get cursor position for homing target acquisition
        // For autocast: will use owner position instead, but we still pass cursor for consistency
        Vector3? cursorPosition = ownerAsPlayer != null ? InputUtility.GetMouseWorldPosition() : (Vector3?)null;
        bool isAutocast = config.autocast;

        Debug.Log($"{AbilityPipelineTag} SpawnProjectileLocally: ability={config.abilityName}, prefab={(projectileConfigToUse != null && projectileConfigToUse.hitbox.prefab != null ? projectileConfigToUse.hitbox.prefab.name : "NULL")}, spawnPos={spawnPos}, direction={direction}, cursor={(cursorPosition.HasValue ? cursorPosition.Value.ToString() : "NULL")}, autocast={isAutocast}");

        ProjectileSpawner.SpawnProjectiles(
            projectileConfigToUse,
            spawnPos,
            direction,
            gameObject,
            damageMultiplier,
            config.abilityName, // Pass ability name
            config.abilityTags?.GetAllTags(), // Pass ability tags
            passedTime, // Network latency compensation (server fast-forward)
            tick, // Client tick for observer RpcClientInitialize
            cursorPosition, // Cursor position for homing target search
            isAutocast, // Whether this is an autocast ability
            config // Top-level ability config for centralized hit visuals
        );
    }

    /// <summary>
    /// Get projectile prefab override from weapon (LaunchZone takes priority, then WeaponConfig)
    /// Only applies to players, not enemies using this component
    /// </summary>
    private GameObject GetWeaponProjectileOverride(Transform weaponTransform, bool isAutocastProjectile = false, bool firedFromOffhand = false)
    {
        if (weaponTransform == null || ownerAsPlayer == null) return null;

        // Autocast explicitly bypasses launch-zone data.
        if (!isAutocastProjectile)
        {
            // First, check LaunchZone-specific override
            GameObject launchZoneOverride = WeaponLaunchPoint.GetProjectilePrefabOverride(weaponTransform);
            if (launchZoneOverride != null)
            {
                return launchZoneOverride;
            }
        }

        // Second, check WeaponConfig override
        WeaponConfig weaponConfig = GetCurrentWeaponConfig(firedFromOffhand);
        if (weaponConfig != null && weaponConfig.projectilePrefabOverride != null)
        {
            return weaponConfig.projectilePrefabOverride;
        }

        return null;
    }

    /// <summary>
    /// Fires a repeating action <paramref name="salvoSize"/> times, waiting
    /// <paramref name="intervalSeconds"/> between each shot (first shot is immediate).
    /// </summary>
    private IEnumerator SalvoCoroutine(
        System.Action<Vector3> fireOnce,
        Vector3 baseDirection,
        int salvoSize,
        float intervalSeconds,
        float salvoAngle,
        uint randomSeed)
    {
        for (int i = 0; i < salvoSize; i++)
        {
            if (i > 0)
                yield return new WaitForSeconds(intervalSeconds);

            float angleOffset = GetDeterministicSalvoAngle(salvoAngle, randomSeed, i);
            Vector3 salvoDirection = Quaternion.Euler(0f, 0f, angleOffset) * baseDirection.normalized;
            fireOnce?.Invoke(salvoDirection);
        }
    }

    private static float GetDeterministicSalvoAngle(float maxAngle, uint seed, int salvoIndex)
    {
        if (maxAngle <= 0f)
            return 0f;

        uint hash = seed ^ ((uint)(salvoIndex + 1) * 0x9E3779B9u);
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        hash ^= hash >> 16;

        float normalized = (hash & 0x00FFFFFFu) / 16777215f;
        return normalized * maxAngle;
    }

    /// <summary>
    /// Get the current weapon config from the player's character data
    /// Returns null if owner is not a player
    /// </summary>
    /// <summary>
    /// Plays a weapon animation state and syncs it over the network.
    /// Uses NetworkAnimator.Play() which immediately queues the state for the next network send,
    /// avoiding the polling gap that can cause NetworkAnimator to miss rapid state changes when
    /// Animator.Play() is called directly. Falls back to Animator.Play() in single-player.
    /// Because NetworkAnimator and Animator are co-located on WeaponSprite,
    /// GetComponentInChildren finds the NetworkAnimator from the weapon root.
    /// </summary>
    private void PlayWeaponAnimationState(Transform weaponRoot, string stateName, float speed)
    {
        if (weaponRoot == null || string.IsNullOrEmpty(stateName)) return;
        Animator anim = GetWeaponAnimator(weaponRoot);
        if (anim == null) return;
        anim.speed = speed;
        NetworkAnimator netAnim = weaponRoot.GetComponentInChildren<NetworkAnimator>();
        if (netAnim != null)
            netAnim.Play(stateName);
        else
            anim.Play(stateName, 0, 0f);
    }

    private Animator GetWeaponAnimator(Transform weaponRoot)
    {
        if (weaponRoot == null) return null;
        var netAnim = weaponRoot.GetComponentInChildren<FishNet.Component.Animating.NetworkAnimator>();
        if (netAnim != null) return netAnim.GetComponent<Animator>();
        return weaponRoot.GetComponentInChildren<Animator>();
    }

    private WeaponConfig GetCurrentWeaponConfig(bool offhand = false)
    {
        if (ownerAsPlayer == null) return null;

        CharacterData characterData = ownerAsPlayer.GetCurrentCharacterData();

        if (offhand)
        {
            WeaponConfig offhandConfig = null;
            if (characterData != null)
            {
                offhandConfig = characterData.mainHandWeaponConfig?.offhandWeaponConfig;
                if (offhandConfig == null && characterData.hasDualWeapons)
                    offhandConfig = characterData.offHandWeaponConfig;
            }

            if (offhandConfig == null)
                offhandConfig = ownerAsPlayer.GetEquippedOffhandWeaponConfig();

            return offhandConfig;
        }

        WeaponConfig mainConfig = characterData != null ? characterData.mainHandWeaponConfig : null;
        if (mainConfig == null)
            mainConfig = ownerAsPlayer.GetEquippedMainWeaponConfig();

        return mainConfig;
    }

    private bool IsCurrentShotFromOffhand()
    {
        return ShouldUseAlternatingAnimations() && !lastAnimationWasMainhand;
    }

    private Transform GetActiveWeaponTransform(bool firedFromOffhand)
    {
        if (firedFromOffhand)
        {
            Transform offhandTransform = transform.Find("OffHandWeaponHolder/OffHandWeapon");
            if (offhandTransform != null)
                return offhandTransform;
        }

        return ownerAsPlayer != null
            ? ownerAsPlayer.GetEquippedMainWeaponTransform()
            : transform.Find("WeaponHolder/Weapon");
    }

    private bool HasRequiredWeapons()
    {
        if (config.requiredWeaponTypes == null || config.requiredWeaponTypes.Count == 0)
        {
            return true; // No requirements
        }

        if (ownerAsPlayer == null)
        {
            // Enemies carry their weapons on EnemyConfig rather than CharacterData.
            EnemyConfig enemyConfig = (ownerOrganism as Enemy)?.GetConfig();
            if (enemyConfig == null) return false;

            return MatchesRequiredWeaponType(enemyConfig.mainHandWeaponConfig)
                || MatchesRequiredWeaponType(enemyConfig.offhandWeaponConfig);
        }

        CharacterData characterData = ownerAsPlayer.GetCurrentCharacterData();
        if (characterData == null) return false;

        // Check mainhand weapon
        bool mainhandMatches = false;
        if (characterData.mainHandWeaponConfig != null)
        {
            string mainhandType = characterData.mainHandWeaponConfig.weaponType;
            mainhandMatches = config.requiredWeaponTypes.Contains(mainhandType) || config.requiredWeaponTypes.Contains("Any");
        }

        // Check offhand weapon
        bool offhandMatches = false;
        if (characterData.offHandWeaponConfig != null)
        {
            string offhandType = characterData.offHandWeaponConfig.weaponType;
            offhandMatches = config.requiredWeaponTypes.Contains(offhandType) || config.requiredWeaponTypes.Contains("Any");
        }

        // Check dual-weapon offhand from mainhand config
        if (!offhandMatches && characterData.mainHandWeaponConfig != null && characterData.mainHandWeaponConfig.offhandWeaponConfig != null)
        {
            string dualOffhandType = characterData.mainHandWeaponConfig.offhandWeaponConfig.weaponType;
            offhandMatches = config.requiredWeaponTypes.Contains(dualOffhandType) || config.requiredWeaponTypes.Contains("Any");
        }

        // Return true if ANY equipped weapon matches
        return mainhandMatches || offhandMatches;
    }

    private bool MatchesRequiredWeaponType(WeaponConfig weapon)
    {
        if (weapon == null) return false;
        return config.requiredWeaponTypes.Contains(weapon.weaponType) || config.requiredWeaponTypes.Contains("Any");
    }


    private bool ShouldUseAlternatingAnimations() => ShouldUseAlternatingAnimations(out _);

    private bool ShouldUseAlternatingAnimations(out bool isSameWeaponAsset)
    {
        isSameWeaponAsset = false;

        // A mainhand animation name is the only strictly required field — offhandAnimationName
        // is an optional override (falls back to mainhandAnimationName when unset), since a
        // genuinely identical weapon in both hands shares the same animator/state names.
        if (string.IsNullOrEmpty(config.mainhandAnimationName))
        {
            return false;
        }

        // Check if offhand weapon exists
        Transform offhandWeaponTransform = transform.Find("OffHandWeaponHolder/OffHandWeapon");
        if (offhandWeaponTransform == null)
        {
            return false;
        }

        // Get current mainhand weapon config
        WeaponConfig mainWeaponConfig = GetCurrentWeaponConfig();
        if (mainWeaponConfig == null)
        {
            return false;
        }

        // Resolve offhand weapon config from either source: a weapon-set's nested
        // offhandWeaponConfig, or an independently-equipped dual-wield weapon on CharacterData.
        WeaponConfig offhandWeaponConfig = mainWeaponConfig.offhandWeaponConfig;
        if (offhandWeaponConfig == null && ownerAsPlayer != null)
        {
            CharacterData characterData = ownerAsPlayer.GetCurrentCharacterData();
            if (characterData != null && characterData.hasDualWeapons)
                offhandWeaponConfig = characterData.offHandWeaponConfig;
        }
        if (offhandWeaponConfig == null)
        {
            return false;
        }

        isSameWeaponAsset = mainWeaponConfig.weaponType == offhandWeaponConfig.weaponType;

        // Alternate whenever both hands wield the same weapon type.
        return !string.IsNullOrEmpty(mainWeaponConfig.weaponType)
            && string.Equals(mainWeaponConfig.weaponType, offhandWeaponConfig.weaponType, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Create a copy of ProjectileConfig with overridden projectile prefab and weapon-specific effects
    /// </summary>
    private ProjectileConfig CreateConfigWithWeaponOverrides(ProjectileConfig original, GameObject overridePrefab, WeaponConfig weaponConfig)
    {
        // Shallow-copy every public instance field automatically so no field is ever missed
        // when new fields are added to ProjectileConfig.
        ProjectileConfig copy = new ProjectileConfig();
        foreach (var field in typeof(ProjectileConfig).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            field.SetValue(copy, field.GetValue(original));
        }

        // Clone the hitbox so prefab/effect overrides never mutate the shared ability config.
        copy.hitbox = original.hitbox.Clone();

        // Apply projectile prefab override
        copy.hitbox.prefab = overridePrefab;

        // Apply weapon-specific overrides on top of the copied base values
        if (weaponConfig != null)
        {
            if (weaponConfig.muzzleFlashOverride != null)
                copy.muzzleFlashPrefab = weaponConfig.muzzleFlashOverride;

            if (weaponConfig.overrideMuzzleLight)
            {
                copy.enableMuzzleLight = true;
                copy.muzzleLightColor = weaponConfig.muzzleLightColorOverride;
                copy.muzzleLightIntensity = weaponConfig.muzzleLightIntensityOverride;
                copy.muzzleLightRange = weaponConfig.muzzleLightRangeOverride;
                copy.muzzleLightDuration = weaponConfig.muzzleLightDurationOverride;
            }

            if (weaponConfig.overrideHitEffects)
            {
                copy.hitbox.effects = new HitFeedbackModule
                {
                    hitEffectPrefab = weaponConfig.hitVisualPrefabOverride,
                    hitSound = weaponConfig.hitSoundOverride,
                    hitFlashColor = weaponConfig.hitFlashColorOverride
                };
            }

            if (weaponConfig.overrideStatusEffects)
                copy.hitbox.onHitEffects = weaponConfig.onHitEffectsOverride;
        }

        return copy;
    }

    private AmmoConfig GetActiveAmmoConfig()
    {
        AmmoConfig baseConfig = GetCurrentWeaponConfig()?.ammoConfig;
        if (baseConfig == null) return null;
        if (_ammoMagazineBonus == 0 && _ammoReloadDelta == 0f) return baseConfig;

        if (_effectiveAmmoConfig == null) _effectiveAmmoConfig = new AmmoConfig();
        _effectiveAmmoConfig.dependsOnAmmo = baseConfig.dependsOnAmmo;
        _effectiveAmmoConfig.magazineSize = Mathf.Max(1, baseConfig.magazineSize + _ammoMagazineBonus);
        _effectiveAmmoConfig.reloadTime = Mathf.Max(0.1f, baseConfig.reloadTime + _ammoReloadDelta);
        _effectiveAmmoConfig.ammoIcon = baseConfig.ammoIcon;
        return _effectiveAmmoConfig;
    }

    /// <summary>
    /// Accumulates weapon ammo modifiers from all active Weapon/WeaponUpgrade traits.
    /// Called by CharacterTraitManager whenever traits change.
    /// </summary>
    public void RebuildAmmoModifiers()
    {
        _ammoMagazineBonus = 0;
        _ammoReloadDelta = 0f;

        CharacterTraitManager traitManager = GetComponent<CharacterTraitManager>();
        if (traitManager == null) return;

        foreach (TraitData data in traitManager.GetActiveTraits())
        {
            if (data == null) continue;
            if (data.weaponAmmoModifier == null || data.weaponAmmoModifier.IsEmpty) continue;

            // If the trait has a requiredAbility, only apply to that specific ability
            if (data.requiredAbility != null && data.requiredAbility != config)
                continue;

            _ammoMagazineBonus += data.weaponAmmoModifier.magazineSizeBonus;
            _ammoReloadDelta += data.weaponAmmoModifier.reloadTimeDelta;
        }

        Debug.Log($"[DataDrivenAbility] RebuildAmmoModifiers {config?.abilityName}: magazineBonus={_ammoMagazineBonus}, reloadDelta={_ammoReloadDelta:F2}s");
    }

    /// <summary>
    /// Accumulates ability config modifiers from all active traits using the Property Path System.
    /// Called by CharacterTraitManager whenever traits change.
    /// </summary>
    public void RebuildConfigModifiers()
    {
        _accumulatedOverrides = null;
        _effectiveProjectileConfig = null;
        _effectiveAreaConfig = null;
        _effectiveBeamConfig = null;
        _effectiveMeleeConfig = null;
        _effectiveExplosionConfig = null;
        _effectiveSummonConfig = null;
        _effectiveConstructConfig = null;
        _effectiveHoldChargeConfig = null;
        _effectiveAbilityConfig = null;
        _effectiveAbilityIcon = null;

        if (config == null) return;

        CharacterTraitManager traitManager = GetComponent<CharacterTraitManager>();
        if (traitManager == null) return;

        // Collect all AbilityConfigModifiers paired with their source traits (for tier scaling)
        var traitModifierPairs = new List<AbilityModifierRuntime.TraitModifierPair>();
        foreach (TraitData data in traitManager.GetActiveTraits())
        {
            if (data?.abilityConfigModifiers == null) continue;
            foreach (var modifier in data.abilityConfigModifiers)
            {
                traitModifierPairs.Add(new AbilityModifierRuntime.TraitModifierPair(data, modifier));

                // Check for ability icon override
                if (modifier.targetAbility == config && modifier.abilityIcon != null)
                    _effectiveAbilityIcon = modifier.abilityIcon;
            }
        }

        // Accumulate using the Property Path System with tier scaling
        _accumulatedOverrides = AbilityModifierRuntime.AccumulateOverrides(config, traitModifierPairs);
        _effectiveAbilityConfig = AbilityModifierRuntime.BuildEffectiveAbilityConfig(config, _accumulatedOverrides);

        if (movementAbility != null && config.isMovementAbility)
            movementAbility.Initialize(EffectiveAbilityConfig);

        // Build cached effective sub-configs using reflection-based application
        if (config.isProjectileAbility && config.projectileConfig != null)
            _effectiveProjectileConfig = AbilityModifierRuntime.BuildEffectiveSubConfig(
                config.projectileConfig, "projectileConfig", _accumulatedOverrides);

        if (config.isAreaAbility && config.areaConfig != null)
            _effectiveAreaConfig = AbilityModifierRuntime.BuildEffectiveSubConfig(
                config.areaConfig, "areaConfig", _accumulatedOverrides);

        if (config.isBeamAbility && config.beamConfig != null)
            _effectiveBeamConfig = AbilityModifierRuntime.BuildEffectiveSubConfig(
                config.beamConfig, "beamConfig", _accumulatedOverrides);

        if (config.isMeleeAbility && config.meleeConfig != null)
            _effectiveMeleeConfig = AbilityModifierRuntime.BuildEffectiveSubConfig(
                config.meleeConfig, "meleeConfig", _accumulatedOverrides);

        if (config.isExplosionAbility && config.explosionConfig != null)
            _effectiveExplosionConfig = AbilityModifierRuntime.BuildEffectiveSubConfig(
                config.explosionConfig, "explosionConfig", _accumulatedOverrides);

        if (config.isSummonAbility && config.summonConfig != null)
            _effectiveSummonConfig = AbilityModifierRuntime.BuildEffectiveSubConfig(
                config.summonConfig, "summonConfig", _accumulatedOverrides);

        if (config.isConstructAbility && config.constructConfig != null)
            _effectiveConstructConfig = AbilityModifierRuntime.BuildEffectiveSubConfig(
                config.constructConfig, "constructConfig", _accumulatedOverrides);

        if (config.isConstructAbility)
            RefreshActiveConstructConfigs(_effectiveConstructConfig ?? config.constructConfig);

        if (config.holdChargeConfig != null)
            _effectiveHoldChargeConfig = AbilityModifierRuntime.BuildEffectiveSubConfig(
                config.holdChargeConfig, "holdChargeConfig", _accumulatedOverrides);

        Debug.Log($"[DataDrivenAbility] RebuildConfigModifiers {config?.abilityName}: " +
                  $"{_accumulatedOverrides?.Count ?? 0} property overrides, " +
                  $"effectiveProj={_effectiveProjectileConfig != null}, " +
                  $"effectiveArea={_effectiveAreaConfig != null}, " +
                  $"effectiveBeam={_effectiveBeamConfig != null}, " +
                  $"effectiveMelee={_effectiveMeleeConfig != null}, " +
                  $"effectiveExplosion={_effectiveExplosionConfig != null}, " +
                  $"effectiveSummon={_effectiveSummonConfig != null}");
    }

    /// <summary>
    /// Pushes the latest construct config to already spawned constructs so trait modifier
    /// changes (damage, salvo size, attack speed, etc.) apply without re-summoning.
    /// </summary>
    private void RefreshActiveConstructConfigs(ConstructConfig refreshedConfig)
    {
        if (refreshedConfig == null || activeConstructs == null || activeConstructs.Count == 0)
            return;

        CleanupDestroyedConstructs();

        for (int i = 0; i < activeConstructs.Count; i++)
        {
            GameObject go = activeConstructs[i];
            if (go == null)
                continue;

            Construct construct = go.GetComponent<Construct>();
            if (construct == null)
                continue;

            construct.ApplyRuntimeConfig(refreshedConfig);
        }
    }

    private void StartReload()
    {
        AmmoConfig ammoCfg = GetActiveAmmoConfig();
        int magazineSize = ammoCfg?.magazineSize ?? 0;
        if (!config.usesAmmo || isReloading || currentAmmo >= magazineSize)
        {
            return;
        }

        isReloading = true;
        reloadStartTime = Time.time;

        // Stop continuous firing when reloading
        if (isHoldingFire)
        {
            StopContinuousFiring();
        }

        // Activate reload bar UI (only if owner is a player)
        if (reloadBar != null && ownerAsPlayer != null)
        {
            reloadBar.StartReload(ammoCfg?.reloadTime ?? 2f, ownerAsPlayer.transform);
        }
    }

    private void UpdateReload()
    {
        if (!isReloading) return;

        AmmoConfig ammoCfg = GetActiveAmmoConfig();
        if (Time.time >= reloadStartTime + (ammoCfg?.reloadTime ?? 2f))
        {
            currentAmmo = ammoCfg?.magazineSize ?? 0;
            isReloading = false;

            // ReloadBar handles its own fade-out effect, no need to stop it here
        }
    }

    private Vector3 GetAimDirection()
    {
        // When an autocast/retaliation target is set, aim directly at it regardless of
        // whether this is a player or enemy — this ensures retaliation melee faces the attacker.
        if (_autocastTarget.HasValue)
        {
            Vector3 dir = (_autocastTarget.Value - transform.position);
            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }

        // Players aim at mouse cursor
        if (ownerAsPlayer != null)
        {
            return InputUtility.GetDirectionToMouse(transform.position);
        }

        // Enemies: Check for FakeMouse target (set by enemy AI)
        Transform fakeMouse = transform.Find("FakeMouse");
        if (fakeMouse != null)
        {
            Vector3 direction = (fakeMouse.position - transform.position).normalized;
            Debug.Log($"[DataDrivenAbility] Enemy {gameObject.name} using FakeMouse aim: {direction}, angle: {Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg:F1}°");
            return direction;
        }

        // Fallback: Find player and aim at them (for enemy AI)
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Vector3 direction = (player.transform.position - transform.position).normalized;
            Debug.Log($"[DataDrivenAbility] Enemy {gameObject.name} aiming at player (no FakeMouse): {direction}");
            return direction;
        }

        // Last resort: aim right
        Debug.LogWarning($"[DataDrivenAbility] Enemy {gameObject.name} has no FakeMouse and no Player found! Aiming right.");
        return Vector3.right;
    }

    /// <summary>
    /// Get the target world position for abilities.
    /// Players target their mouse cursor, enemies target player or FakeMouse.
    /// </summary>
    private Vector3 GetTargetWorldPosition()
    {
        // Autocast: override target with nearest enemy found this frame
        if (_autocastTarget.HasValue)
        {
            return _autocastTarget.Value;
        }

        // Players aim at mouse cursor
        if (ownerAsPlayer != null)
        {
            if (CursorManager.Instance != null)
            {
                Organism targetedOrganism = CursorManager.Instance.TargetedOrganism;
                if (targetedOrganism != null)
                    return targetedOrganism.transform.position;
            }

            return InputUtility.GetMouseWorldPosition();
        }

        // Enemies: Check for FakeMouse target (set by enemy AI)
        Transform fakeMouse = transform.Find("FakeMouse");
        if (fakeMouse != null)
        {
            return fakeMouse.position;
        }

        // Fallback: Find player and aim at them (for enemy AI)
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            return player.transform.position;
        }

        // Last resort: position in front of enemy
        return transform.position + transform.right;
    }

    /// <summary>
    /// Finds a random living hostile Organism within range for use by the autocast system.
    /// Hostile/friendly classification is resolved by comparing parent/root layers.
    /// Returns null if no valid target is found.
    /// </summary>
    private Vector3? FindAutocastTarget(float range)
    {
        return FindAutocastTarget(range, null);
    }

    /// <summary>
    /// Finds a random living hostile Organism within range for use by the autocast system.
    /// Hostile/friendly classification is resolved by comparing parent/root layers.
    /// Excludes targets that have already been used (for multi-target bursts).
    /// Returns null if no valid target is found.
    /// </summary>
    private Vector3? FindAutocastTarget(float range, HashSet<Vector3> excludeTargets)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
        var candidates = new List<Vector3>();
        int ownerLayer = GetOrganismParentLayer(ownerOrganism);

        foreach (var col in hits)
        {
            Organism candidate = col.GetComponentInParent<Organism>();
            if (candidate == null || !candidate.IsAlive || candidate == ownerOrganism) continue;

            int candidateLayer = GetOrganismParentLayer(candidate);
            if (candidateLayer == ownerLayer) continue;

            Vector3 candidatePos = candidate.transform.position;

            // Skip if this target was already used in a previous burst cast
            if (excludeTargets != null && excludeTargets.Contains(candidatePos)) continue;

            candidates.Add(candidatePos);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Finds a random living friendly Organism within range for use by the autocast system.
    /// Excludes targets that have already been used (for multi-target bursts).
    /// Returns null if no valid target is found.
    /// </summary>
    private Vector3? FindFriendlyAutocastTarget(float range, HashSet<Vector3> excludeTargets)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
        var candidates = new List<Vector3>();
        int ownerLayer = GetOrganismParentLayer(ownerOrganism);

        foreach (var col in hits)
        {
            Organism candidate = col.GetComponentInParent<Organism>();
            if (candidate == null || !candidate.IsAlive || candidate == ownerOrganism) continue;

            int candidateLayer = GetOrganismParentLayer(candidate);
            if (candidateLayer != ownerLayer) continue;

            Vector3 candidatePos = candidate.transform.position;

            if (excludeTargets != null && excludeTargets.Contains(candidatePos)) continue;

            candidates.Add(candidatePos);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Picks a random point within autocast range around the caster.
    /// Used as a fallback when no target modes are enabled.
    /// </summary>
    private Vector3 FindRandomAutocastPoint(float range)
    {
        float clampedRange = Mathf.Max(0.1f, range);
        Vector2 offset = Random.insideUnitCircle * clampedRange;
        return transform.position + new Vector3(offset.x, offset.y, 0f);
    }

    /// <summary>
    /// Resolves an Organism's team layer from its root object (parent layer source of truth).
    /// </summary>
    private static int GetOrganismParentLayer(Organism organism)
    {
        if (organism == null)
            return -1;

        Transform root = organism.transform.root;
        return root != null ? root.gameObject.layer : organism.gameObject.layer;
    }

    #endregion

    #region Beam Weapon Logic

    private bool ExecuteBeamAbility()
    {
        if (beamAbility == null)
        {
            Debug.LogWarning("[DataDrivenAbility] BeamAbility is null!");
            return false;
        }
        WeaponLaunchPoint.FindLaunchZone(transform);
        // Refresh BeamAbility with latest effective config so trait overrides apply immediately.
        beamAbility.SetContext(CreateSubAbilityContext());
        beamAbility.Initialize(EffectiveAbilityConfig);

        // NOTE: OnAbilityActivated() is already called in TryUseAbility() before FireAbility().
        // Do NOT call it again here — doing so would double-trigger animations and movement blocks.
        Debug.Log($"[DataDrivenAbility] Calling beamAbility.Activate()");
        bool result = beamAbility.Activate();
        Debug.Log($"[DataDrivenAbility] beamAbility.Activate() returned: {result}");
        return result;
    }

    #endregion

    #region Melee Weapon Logic

    /* TODO: Implement when MeleeWeapon class exists
    private bool ExecuteMeleeWeapon()
    {
        if (meleeWeapon == null) return false;

        meleeWeapon.PerformAttack();
        return true;
    }
    */

    #endregion

    #region Area Ability Logic

    private bool ExecuteAreaAbility()
    {
        if (config.areaConfig == null) return false;
        SpawnAreaAbility();
        return true;
    }

    private void SpawnAreaAbility()
    {
        AreaConfig effectiveAreaConfig = GetEffectiveAreaConfig();
        int count = Mathf.Max(1, effectiveAreaConfig.areaCount);

        float sizeMultiplier = 1f;
        Organism organism = GetComponent<Organism>();
        if (organism != null)
        {
            float abilitySizePercent = organism.AllStats.GetStat("AbilitySize");
            if (abilitySizePercent != 0f)
            {
                sizeMultiplier = 1f + abilitySizePercent;
                Debug.Log($"[SpawnAreaAbility] AbilitySize stat: +{abilitySizePercent}%, multiplier: {sizeMultiplier}");
            }
        }

        // First area uses the normal calculated position (cursor or current autocast target).
        Vector3 firstPosition = CalculateAreaAbilityPosition();
        var usedTargets = new HashSet<Vector3> { firstPosition };
        SpawnSingleAreaAt(firstPosition, effectiveAreaConfig, sizeMultiplier);

        // Each additional area targets a distinct enemy (autocast only).
        for (int i = 1; i < count; i++)
        {
            Vector3 extraPosition;
            if (_autocastTarget.HasValue)
            {
                float range = config.autocastRange > 0f ? config.autocastRange
                    : (effectiveAreaConfig.range > 0f ? effectiveAreaConfig.range : 20f);
                Vector3? extraTarget = FindAutocastTarget(range, usedTargets);
                if (!extraTarget.HasValue) break;
                extraPosition = extraTarget.Value;
            }
            else
            {
                extraPosition = firstPosition;
            }
            usedTargets.Add(extraPosition);
            SpawnSingleAreaAt(extraPosition, effectiveAreaConfig, sizeMultiplier);
        }
    }

    private void SpawnSingleAreaAt(Vector3 spawnPosition, AreaConfig effectiveAreaConfig, float sizeMultiplier)
    {
        GameObject areaAbilityGO;

        // Check if a custom prefab is specified, otherwise use the default Aura_Area prefab
        if (effectiveAreaConfig.hitbox.prefab != null)
        {
            // Spawn from custom prefab
            areaAbilityGO = Instantiate(
                effectiveAreaConfig.hitbox.prefab,
                spawnPosition,
                Quaternion.identity
            );
            Debug.Log($"[SpawnAreaAbility] Spawned area ability from custom prefab: {effectiveAreaConfig.hitbox.prefab.name}");
        }
        else
        {
            // Use default Aura_Area prefab from Resources for proper NetworkObject serialization
            GameObject auraPrefab = Resources.Load<GameObject>("Prefabs/Abilities/Aura_Area");
            if (auraPrefab == null)
            {
                Debug.LogError($"[SpawnAreaAbility] Aura_Area prefab not found in Resources/Prefabs/Abilities! Create a prefab with NetworkObject and AreaAbility components.");
                return;
            }

            areaAbilityGO = Instantiate(auraPrefab, spawnPosition, Quaternion.identity);
            areaAbilityGO.name = $"{config.abilityName}_Area";
            Debug.Log($"[SpawnAreaAbility] Spawned area ability from default Aura_Area prefab for {config.abilityName}");
        }

        // Network spawn if in multiplayer
        var networkManager = InstanceFinder.NetworkManager;
        if (networkManager != null && networkManager.IsServerStarted)
        {
            networkManager.ServerManager.Spawn(areaAbilityGO);
            Debug.Log($"[DataDrivenAbility] Network-spawned AoE ability: {config.abilityName}");
        }

        // Find AreaAbility component — check children too in case it sits below the prefab root.
        // If the prefab is a visual-only asset without the component, add it so lifecycle
        // management (duration, destroy) always runs.
        AreaAbility areaAbilityComponent = areaAbilityGO.GetComponentInChildren<AreaAbility>();
        if (areaAbilityComponent == null)
        {
            areaAbilityComponent = areaAbilityGO.AddComponent<AreaAbility>();
            Debug.Log($"[SpawnAreaAbility] AreaAbility component not found on {areaAbilityGO.name} — added automatically.");
        }

        Debug.Log($"[DmgPipeline] <{config.abilityName}> Area ability");
        areaAbilityComponent.SetContext(CreateSubAbilityContext());
        areaAbilityComponent.InitializeFromConfig(effectiveAreaConfig);

        // Set caster for auras that follow
        if (effectiveAreaConfig.isAura && effectiveAreaConfig.followCaster)
        {
            areaAbilityComponent.SetCaster(transform);
            Debug.Log($"[SpawnAreaAbility] Set aura caster to {transform.name}");
        }

        // Configure particle systems to match area shape
        areaAbilityComponent.ConfigureParticles(effectiveAreaConfig);

        // Apply size modifier — must happen AFTER InitializeFromConfig sets scale from config
        if (sizeMultiplier != 1f)
        {
            areaAbilityGO.transform.localScale *= sizeMultiplier;
        }

        areaAbilityComponent.Activate();
    }

    // Area indicator methods moved to AreaAbility class

    private Vector3 CalculateAreaAbilityPosition()
    {
        AreaConfig areaConfig = GetEffectiveAreaConfig();

        if (areaConfig.isPointBlank)
        {
            // Spawn at player position
            return transform.position;
        }

        // Explicit/autocast target: spawn at the supplied world position.
        if (_autocastTarget.HasValue)
        {
            return _autocastTarget.Value;
        }

        // Manual cast: spawn at mouse position, clamped to range
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mousePosition.z = 0f;

        Vector3 direction = (mousePosition - transform.position).normalized;
        float distance = Mathf.Min(
            Vector3.Distance(transform.position, mousePosition),
            areaConfig.range
        );

        return transform.position + direction * distance;
    }

    #endregion

    #region Construct/Summon Logic

    private bool ExecuteConstructAbility()
    {
        if (config.constructConfig == null)
        {
            Debug.LogWarning($"{AbilityPipelineTag} ExecuteConstructAbility aborted: ability={config?.abilityName}, constructConfig=NULL");
            return false;
        }

        ConstructConfig constructConfig = GetEffectiveConstructConfig();

        PlacementLog($"ExecuteConstructAbility: holdToPlace={constructConfig.holdToPlace}, isPlacingConstruct={isPlacingConstruct}, prefab={(constructConfig.constructPrefab != null ? constructConfig.constructPrefab.name : "NULL")}");

        // ── Hold-to-place mode ───────────────────────────────────────────────
        // While the ghost is already visible, suppress hold-fire retriggers.
        if (constructConfig.holdToPlace && isPlacingConstruct)
        {
            PlacementLog("Suppressed re-trigger — coroutine already running");
            return false;
        }

        OnAbilityActivated();

        Debug.Log($"{AbilityPipelineTag} ExecuteConstructAbility start: ability={config.abilityName}, prefab={(constructConfig.constructPrefab != null ? constructConfig.constructPrefab.name : "NULL")}, maxConstructs={constructConfig.maxConstructs}, abilities={constructConfig.constructAbilities?.Count ?? 0}");

        if (constructConfig.constructPrefab == null)
        {
            Debug.LogError($"[DataDrivenAbility] No construct prefab assigned!");
            return false;
        }

        if (constructConfig.holdToPlace)
        {
            PlacementLog("holdToPlace branch reached — setting isPlacingConstruct=true and starting coroutine");
            isPlacingConstruct = true;
            placementCoroutine = StartCoroutine(ConstructPlacementRoutine(constructConfig));
            return true;
        }
        PlacementLog("holdToPlace=false — falling through to immediate spawn");

        // Clean up destroyed constructs
        CleanupDestroyedConstructs();

        Debug.Log($"[DataDrivenAbility] ExecuteConstructAbility - Current activeConstructs count: {activeConstructs.Count}, Max: {constructConfig.maxConstructs}");
        Debug.Log($"[DataDrivenAbility] activeConstructs list instance ID: {activeConstructs.GetHashCode()}");

        // Calculate spawn position
        Vector3 spawnPosition = CalculateConstructSpawnPosition(constructConfig);

        // Create a ConstructAbility manager to handle this construct
        GameObject constructManager = new GameObject($"{constructConfig.constructPrefab.name}_Manager");
        constructManager.transform.SetParent(transform); // Parent to caster for cleanup
        ConstructAbility constructAbility = constructManager.AddComponent<ConstructAbility>();

        Debug.Log($"[DataDrivenAbility] Passing activeConstructs list (count: {activeConstructs.Count}) to ConstructAbility.SpawnConstruct");

        // Spawn the construct (handles limits, animations, lifetime internally)
        // Pass the shared activeConstructs list for proper limit enforcement
        constructAbility.SetContext(CreateSubAbilityContext());
        constructAbility.SpawnConstruct(constructConfig, spawnPosition, activeConstructs);

        if (constructAbility.ConstructInstance != null)
        {
            // Note: construct is already added to activeConstructs in SpawnConstruct method
            Debug.Log($"[DataDrivenAbility] Construct spawned successfully. Current count: {activeConstructs.Count}");

            // Setup abilities on the construct
            SetupConstructAbilities(constructAbility.ConstructInstance, constructConfig);

            Debug.Log($"{AbilityPipelineTag} ExecuteConstructAbility success: ability={config.abilityName}, construct={constructAbility.ConstructInstance.name}, spawnPosition={spawnPosition}");
            Debug.Log($"[DataDrivenAbility] Spawned construct via ConstructAbility at {spawnPosition}");
            return true;
        }

        Debug.LogWarning($"{AbilityPipelineTag} ExecuteConstructAbility failed: ability={config.abilityName}, construct instance not created");
        Debug.Log($"[DataDrivenAbility] ConstructInstance was null, spawn failed");
        return false;
    }

    /// <summary>
    /// Spawns a semi-transparent ghost of the construct prefab that follows the cursor.
    /// On button release the ghost is destroyed and the real construct is spawned at the same position.
    /// </summary>
    private IEnumerator ConstructPlacementRoutine(ConstructConfig constructConfig)
    {
        // Stop the hold-fire loop — this coroutine owns button tracking from here.
        isHoldingFire = false;
        PlacementLog($"ConstructPlacementRoutine START: ability={config?.abilityName}");

        float alpha = constructConfig.ghostAlpha > 0f ? constructConfig.ghostAlpha : 0.45f;

        // ── Local helpers ────────────────────────────────────────────────────
        // Spawns (or re-spawns) the ghost from a given prefab at a given position.
        // Strips NetworkObject, disables gameplay components, sets alpha.
        System.Func<GameObject, Vector3, GameObject> SpawnGhost = (prefab, pos) =>
        {
            var ghost = Instantiate(prefab, pos, Quaternion.identity);
            ghost.name = $"{prefab.name}_PlacementGhost";

            var no = ghost.GetComponent<NetworkObject>();
            if (no != null) Destroy(no);

            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is Construct) mb.enabled = false;
            foreach (var col in ghost.GetComponentsInChildren<Collider2D>(true))
                col.enabled = false;
            foreach (var rb in ghost.GetComponentsInChildren<Rigidbody2D>(true))
                rb.simulated = false;

            // alphaMultiplier before Start() so beam sequences use it from frame 1.
            foreach (var br in ghost.GetComponentsInChildren<BeamRenderer>(true))
                br.alphaMultiplier = alpha;

            return ghost;
        };

        // ── Initial ghost ────────────────────────────────────────────────────
        Vector3 startPos = CalculateConstructSpawnPosition(constructConfig);
        int currentDirIndex = constructConfig.use8WayPlacement
            ? ConstructConfig.DirectionIndex(startPos - transform.position)
            : -1;
        GameObject currentPrefab = constructConfig.GetDirectionalPrefab(currentDirIndex);

        constructPlacementGhost = SpawnGhost(currentPrefab, startPos);
        PlacementLog($"Ghost spawned at {startPos}, dir={currentDirIndex}, prefab={currentPrefab.name}");

        // Wait one frame so LightningBoltRenderer.Start() has created its child SpriteRenderers.
        yield return null;

        foreach (var sr in constructPlacementGhost.GetComponentsInChildren<SpriteRenderer>(true))
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);

        // ── Track cursor until button released ───────────────────────────────
        PlacementLog($"Entering tracking loop. Button held={IsAbilityButtonHeld()}, slot={abilitySlotIndex}");
        while (IsAbilityButtonHeld())
        {
            Vector3 pos = CalculateConstructSpawnPosition(constructConfig);
            pos.z = 0f;

            if (constructConfig.use8WayPlacement)
            {
                int newDir = ConstructConfig.DirectionIndex(pos - transform.position);
                if (newDir != currentDirIndex)
                {
                    // Direction changed — swap ghost for the new directional prefab.
                    GameObject newPrefab = constructConfig.GetDirectionalPrefab(newDir);
                    Destroy(constructPlacementGhost);
                    constructPlacementGhost = SpawnGhost(newPrefab, pos);

                    // Tint next frame after Start() runs on the new ghost.
                    yield return null;
                    foreach (var sr in constructPlacementGhost.GetComponentsInChildren<SpriteRenderer>(true))
                        sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);

                    currentDirIndex = newDir;
                    currentPrefab = newPrefab;
                    PlacementLog($"Direction changed to {newDir}, prefab={newPrefab.name}");
                }
            }

            constructPlacementGhost.transform.position = pos;
            yield return null;
        }
        PlacementLog("Button released — confirming placement");

        // ── Swap ghost for real construct at the same position ───────────────
        Vector3 confirmedPosition = constructPlacementGhost.transform.position;
        Destroy(constructPlacementGhost);
        constructPlacementGhost = null;

        CleanupDestroyedConstructs();

        // Override constructPrefab on a temporary copy so SpawnConstruct uses the directional one.
        ConstructConfig spawnConfig = constructConfig;
        if (constructConfig.use8WayPlacement && currentDirIndex >= 0)
        {
            spawnConfig = new ConstructConfig();
            System.Array.Copy(
                new[] { constructConfig }, new[] { spawnConfig }, 0); // shallow field copy via reflection
            foreach (var field in typeof(ConstructConfig).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                field.SetValue(spawnConfig, field.GetValue(constructConfig));
            spawnConfig.constructPrefab = currentPrefab;
        }

        GameObject constructManager = new GameObject($"{currentPrefab.name}_Manager");
        constructManager.transform.SetParent(transform);
        ConstructAbility constructAbility = constructManager.AddComponent<ConstructAbility>();
        constructAbility.SetContext(CreateSubAbilityContext());
        constructAbility.SpawnConstruct(spawnConfig, confirmedPosition, activeConstructs);

        if (constructAbility.ConstructInstance != null)
        {
            SetupConstructAbilities(constructAbility.ConstructInstance, spawnConfig);
            PlacementLog($"Confirmed at {confirmedPosition}, dir={currentDirIndex}, prefab={currentPrefab.name}");
        }

        StartCooldown();
        ConsumeMana();

        isPlacingConstruct = false;
        placementCoroutine = null;
        PlacementLog("ConstructPlacementRoutine END");
    }

    private void PlacementLog(string message) { if (logPlacement) Debug.Log($"[Placement] {message}"); }

    private GameObject GetConstructPrefab(ConstructConfig constructConfig)
    {
        // Prefer direct prefab reference
        if (constructConfig.constructPrefab != null)
        {
            return constructConfig.constructPrefab;
        }

        // Fallback to legacy Resources loading
        if (!string.IsNullOrEmpty(constructConfig.prefabName))
        {
            string fullPath = constructConfig.resourcesPath + constructConfig.prefabName;
            return Resources.Load<GameObject>(fullPath);
        }

        return null;
    }

    private void CleanupDestroyedConstructs()
    {
        activeConstructs.RemoveAll(c => c == null);
    }

    private bool HandleConstructLimit(ConstructConfig constructConfig)
    {
        switch (constructConfig.limitBehavior)
        {
            case ConstructLimitBehavior.DestroyOldest:
                if (activeConstructs.Count > 0)
                {
                    GameObject oldest = activeConstructs[0];
                    activeConstructs.RemoveAt(0);
                    if (oldest != null)
                    {
                        Destroy(oldest);
                    }
                }
                return true;

            case ConstructLimitBehavior.PreventSpawn:
                return false;

            case ConstructLimitBehavior.ReplaceClosest:
                Vector3 spawnPos = CalculateConstructSpawnPosition(constructConfig);
                GameObject closest = FindClosestConstruct(spawnPos);
                if (closest != null)
                {
                    activeConstructs.Remove(closest);
                    Destroy(closest);
                }
                return true;

            default:
                return false;
        }
    }

    private GameObject FindClosestConstruct(Vector3 position)
    {
        GameObject closest = null;
        float closestDistance = float.MaxValue;

        foreach (GameObject construct in activeConstructs)
        {
            if (construct == null) continue;

            float distance = Vector3.Distance(position, construct.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = construct;
            }
        }

        return closest;
    }

    private Vector3 CalculateConstructSpawnPosition(ConstructConfig constructConfig)
    {
        if (constructConfig.spawnAtCaster)
        {
            if (constructConfig.spawnAtCasterRadius > 0f)
            {
                Vector2 randomOffset = Random.insideUnitCircle * constructConfig.spawnAtCasterRadius;
                return transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
            }
            return transform.position;
        }

        if (constructConfig.spawnAtMouse)
        {
            Vector3 mousePos = GetTargetWorldPosition();
            mousePos.z = 0f;

            Vector2 directionToMouse = (mousePos - transform.position);
            float distanceToMouse = directionToMouse.magnitude;

            float actualDistance = Mathf.Min(distanceToMouse, constructConfig.maxRange);
            Vector2 direction = directionToMouse.normalized;

            return transform.position + (Vector3)(direction * actualDistance);
        }

        // Default: spawn at caster position
        return transform.position;
    }

    /// <summary>
    /// Setup abilities on a spawned construct
    /// </summary>
    private void SetupConstructAbilities(GameObject construct, ConstructConfig constructConfig)
    {
        Debug.Log($"[SetupConstructAbilities] Setting up abilities for {construct.name}");

        // Setup configured abilities
        if (constructConfig.constructAbilities != null)
        {
            foreach (ConstructAbilityConfig abilityConfig in constructConfig.constructAbilities)
            {
                if (abilityConfig == null) continue;

                SetupConstructAbility(construct, abilityConfig);
            }
        }

        Debug.Log($"[SetupConstructAbilities] Abilities setup complete");
    }

    /// <summary>
    /// Setup an area ability on the construct
    /// </summary>
    private void SetupConstructAreaAbility(GameObject construct, AreaConfig areaConfig)
    {
        Debug.Log($"[SetupConstructAreaAbility] Setting up AreaAbility on {construct.name}");

        // Create a dedicated child object for the area damage zone so that
        // AreaAbility never touches the root construct's own Collider2D or scale.
        // (AreaAbility.Awake() grabs GetComponent<Collider2D>() and CreateCollider()
        // then destroys it — placing it on the root would wipe the construct's hitbox.)
        GameObject areaNode = new GameObject("AreaDamageZone");
        areaNode.transform.SetParent(construct.transform);
        areaNode.transform.localPosition = Vector3.zero;
        areaNode.transform.localRotation = Quaternion.identity;
        areaNode.transform.localScale = Vector3.one;

        GameObject areaEffectObject = null;

        // If a spell prefab is configured, spawn it as a child for visuals
        if (areaConfig.hitbox.prefab != null)
        {
            Debug.Log($"[SetupConstructAreaAbility] Spawning area spell prefab as child: {areaConfig.hitbox.prefab.name}");
            areaEffectObject = Instantiate(areaConfig.hitbox.prefab, areaNode.transform);
            areaEffectObject.transform.localPosition = Vector3.zero;
            areaEffectObject.transform.localRotation = Quaternion.identity;
            areaEffectObject.name = "AreaEffect";
        }

        // Add AreaAbility to the dedicated child node, not the construct root
        AreaAbility areaAbility = areaNode.AddComponent<AreaAbility>();
        Debug.Log($"[SetupConstructAreaAbility] Added AreaAbility component to AreaDamageZone child");

        // Link caster so aura-follow works correctly
        areaAbility.SetCaster(construct.transform);

        // Initialize and activate
        areaAbility.SetContext(CreateSubAbilityContext());
        areaAbility.InitializeFromConfig(areaConfig);
        // Configure particle systems to match area shape
        areaAbility.ConfigureParticles(areaConfig);

        // Start particles in child if present
        if (areaEffectObject != null)
        {
            ParticleSystem[] particleSystems = areaEffectObject.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                ps.Play();
            }
        }

        areaAbility.Activate();

        // Setup light if configured
        if (areaConfig.hasLight)
        {
            Debug.Log($"[SetupConstructAreaAbility] Adding Light2D");

            // Create a separate child GameObject for the light to avoid affecting sprite renderer
            GameObject lightObject = new GameObject("ConstructLight");
            lightObject.transform.SetParent(construct.transform);
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;

            Light2D light = lightObject.AddComponent<Light2D>();
            light.color = areaConfig.lightColor;
            light.intensity = areaConfig.lightIntensity;
            light.pointLightOuterRadius = areaConfig.lightRadius;

            // Area shape now comes from prefab collider + scale, so keep light unwarped.
            lightObject.transform.localScale = Vector3.one;

            Debug.Log($"[SetupConstructAreaAbility] Light2D added with scale {lightObject.transform.localScale}");
        }

        Debug.Log($"[SetupConstructAreaAbility] AreaAbility setup complete");
    }

    /// <summary>
    /// Setup any ability type on the construct (Area, Projectile, etc.)
    /// </summary>
    private void SetupConstructAbility(GameObject construct, ConstructAbilityConfig abilityConfig)
    {
        Debug.Log($"[SetupConstructAbility] Setting up {abilityConfig.abilityType} ability on {construct.name}");

        // Determine ability type and setup accordingly
        switch (abilityConfig.abilityType)
        {
            case ConstructAbilityConfig.AbilityType.Area:
                if (abilityConfig.areaConfig != null)
                {
                    SetupConstructAreaAbility(construct, abilityConfig.areaConfig);
                }
                break;

            case ConstructAbilityConfig.AbilityType.Projectile:
                if (abilityConfig.projectileConfig != null)
                {
                    if (construct.GetComponent<AutoTurret>() != null)
                    {
                        Debug.Log($"{AbilityPipelineTag} SetupConstructAbility: projectile config is handled by AutoTurret on {construct.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"{AbilityPipelineTag} SetupConstructAbility: projectile config present on {construct.name}, but no AutoTurret component is handling it");
                    }
                }
                break;

            case ConstructAbilityConfig.AbilityType.Beam:
                Debug.LogWarning($"[SetupConstructAbility] Beam abilities on constructs not yet implemented");
                break;

            case ConstructAbilityConfig.AbilityType.Channel:
                Debug.LogWarning($"[SetupConstructAbility] Channel abilities on constructs not yet implemented");
                break;
        }
    }

    #endregion

    #region Summon Ability Logic

    private bool ExecuteSummonAbility()
    {
        SummonConfig summonCfg = GetEffectiveSummonConfig();
        if (summonCfg == null)
        {
            Debug.LogWarning($"{AbilityPipelineTag} ExecuteSummonAbility aborted: ability={config?.abilityName}, summonConfig=NULL");
            return false;
        }

        if (summonCfg.summonPrefab == null)
        {
            Debug.LogError($"[DataDrivenAbility] No summon prefab assigned for ability={config?.abilityName}!");
            return false;
        }

        OnAbilityActivated();

        // Clean up destroyed summons
        activeSummons.RemoveAll(s => s == null);

        // Calculate spawn position near the caster
        Vector3 spawnPosition = transform.position + (Vector3)summonCfg.spawnOffset;

        // Create the persistent group manager on first use; reuse it on subsequent casts.
        if (_summonAbility == null)
        {
            GameObject summonManager = new GameObject($"{summonCfg.summonPrefab.name}_SummonManager");
            summonManager.transform.SetParent(transform);
            _summonAbility = summonManager.AddComponent<SummonAbility>();
            _summonAbility.SetContext(CreateSubAbilityContext());
        }

        bool spawned = _summonAbility.SpawnSummon(summonCfg, spawnPosition, activeSummons);

        if (spawned)
        {
            Debug.Log($"{AbilityPipelineTag} ExecuteSummonAbility success: ability={config.abilityName}, position={spawnPosition}, total={activeSummons.Count}");
            return true;
        }

        Debug.LogWarning($"{AbilityPipelineTag} ExecuteSummonAbility failed: ability={config.abilityName}, spawn blocked");
        return false;
    }

    #endregion

    #region Trap Ability Logic

    private bool ExecuteTrapAbility()
    {
        if (config.trapConfig == null) return false;

        OnAbilityActivated();

        TrapAbilityConfig trapConfig = config.trapConfig;

        if (trapConfig.trapPrefab == null)
        {
            Debug.LogError($"[DataDrivenAbility] No trap prefab assigned!");
            return false;
        }

        // Clean up destroyed traps
        CleanupDestroyedTraps();

        Debug.Log($"[DataDrivenAbility] ExecuteTrapAbility - Current activeTraps count: {activeTraps.Count}, Max: {trapConfig.maxTraps}");

        // Handle trap limit
        if (trapConfig.maxTraps > 0 && activeTraps.Count >= trapConfig.maxTraps)
        {
            if (!HandleTrapLimit(trapConfig))
            {
                Debug.Log($"[DataDrivenAbility] Trap limit reached, spawn prevented");
                return false;
            }
        }

        // Calculate spawn position
        Vector3 spawnPosition = CalculateTrapSpawnPosition(trapConfig);

        // Spawn the trap (local first)
        GameObject trap = Instantiate(trapConfig.trapPrefab, spawnPosition, Quaternion.identity);
        trap.name = $"{trapConfig.trapPrefab.name}_{activeTraps.Count}";

        // Network spawn if in multiplayer
        var networkManager = InstanceFinder.NetworkManager;
        if (networkManager != null && networkManager.IsServerStarted)
        {
            networkManager.ServerManager.Spawn(trap);
            Debug.Log($"[DataDrivenAbility] Network-spawned trap: {trapConfig.trapPrefab.name}");
        }

        // Initialize trap
        TrapAbility trapAbility = trap.GetComponent<TrapAbility>();
        if (trapAbility == null)
        {
            trapAbility = trap.AddComponent<TrapAbility>();
        }

        trapAbility.SetContext(CreateSubAbilityContext());
        trapAbility.Initialize(trapConfig);

        // Track the trap
        activeTraps.Add(trap);

        Debug.Log($"[DataDrivenAbility] Spawned trap at {spawnPosition}. Active traps: {activeTraps.Count}");
        return true;
    }

    private void CleanupDestroyedTraps()
    {
        activeTraps.RemoveAll(t => t == null);
    }

    private bool HandleTrapLimit(TrapAbilityConfig trapConfig)
    {
        switch (trapConfig.limitBehavior)
        {
            case TrapLimitBehavior.DestroyOldest:
                if (activeTraps.Count > 0)
                {
                    GameObject oldest = activeTraps[0];
                    activeTraps.RemoveAt(0);
                    if (oldest != null)
                    {
                        Destroy(oldest);
                    }
                }
                return true;

            case TrapLimitBehavior.PreventSpawn:
                return false;

            case TrapLimitBehavior.ReplaceClosest:
                Vector3 spawnPos = CalculateTrapSpawnPosition(trapConfig);
                GameObject closest = FindClosestTrap(spawnPos);
                if (closest != null)
                {
                    activeTraps.Remove(closest);
                    Destroy(closest);
                }
                return true;

            default:
                return false;
        }
    }

    private GameObject FindClosestTrap(Vector3 position)
    {
        GameObject closest = null;
        float closestDistance = float.MaxValue;

        foreach (GameObject trap in activeTraps)
        {
            if (trap == null) continue;

            float distance = Vector3.Distance(position, trap.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = trap;
            }
        }

        return closest;
    }

    private Vector3 CalculateTrapSpawnPosition(TrapAbilityConfig trapConfig)
    {
        if (trapConfig.spawnAtCaster)
        {
            return transform.position;
        }

        if (trapConfig.spawnAtMouse)
        {
            Vector3 mousePos = GetTargetWorldPosition();
            mousePos.z = 0f;

            Vector2 directionToMouse = (mousePos - transform.position);
            float distanceToMouse = directionToMouse.magnitude;

            float actualDistance = Mathf.Min(distanceToMouse, trapConfig.maxRange);
            Vector2 direction = directionToMouse.normalized;

            return transform.position + (Vector3)(direction * actualDistance);
        }

        // Default: spawn at caster position
        return transform.position;
    }

    #endregion

    #region Channeling Logic

    private bool ExecuteChanneledAbility()
    {
        if (channelAbility == null)
        {
            Debug.LogWarning("[DataDrivenAbility] ChannelAbility is null!");
            return false;
        }

        // NOTE: OnAbilityActivated() is already called in TryUseAbility() before FireAbility().
        // Do NOT call it again here — doing so would double-trigger animations and movement blocks.
        Debug.Log($"[DataDrivenAbility] Calling channelAbility.Activate()");
        bool result = channelAbility.Activate();
        Debug.Log($"[DataDrivenAbility] channelAbility.Activate() returned: {result}");
        return result;
    }

    /// <summary>
    /// Called server-side by PlayerController.ServerRpcSpawnChannelObject to instantiate and
    /// network-spawn the channel prefab. Gives the server-side ChannelAbility its object reference
    /// and returns the spawned GameObject so PlayerController can send a TargetRpc to the owner.
    /// </summary>
    public GameObject SpawnChannelObjectOnServer(Vector3 spawnPos, Quaternion rotation)
    {
        if (channelAbility == null || config?.channelConfig?.channelObjectPrefab == null)
        {
            Debug.LogError("[DataDrivenAbility] SpawnChannelObjectOnServer: channelAbility or channelObjectPrefab is null");
            return null;
        }

        GameObject channelObj = Instantiate(config.channelConfig.channelObjectPrefab, spawnPos, rotation, null);
        channelObj.transform.localScale = Vector3.one * config.channelConfig.scale;

        NetworkObject netObj = channelObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            InstanceFinder.NetworkManager.ServerManager.Spawn(channelObj);
            Debug.Log($"[DataDrivenAbility] SpawnChannelObjectOnServer: network-spawned '{channelObj.name}'");
        }
        else
        {
            Debug.LogWarning($"[DataDrivenAbility] SpawnChannelObjectOnServer: '{channelObj.name}' has no NetworkObject — spawned locally only");
        }

        // Give the server-side ChannelAbility its reference immediately
        channelAbility.SetChannelObjectReferences(channelObj);

        return channelObj;
    }

    /// <summary>
    /// Called on the owning client after PlayerController.TargetRpcReceiveChannelObject arrives.
    /// Passes the server-spawned channel object to the local ChannelAbility for position tracking
    /// and collider-based damage detection.
    /// </summary>
    public void ReceiveChannelObjectFromServer(GameObject channelObj)
    {
        if (channelObj == null)
        {
            Debug.LogError("[DataDrivenAbility] ReceiveChannelObjectFromServer: received null channel object");
            return;
        }
        Debug.Log($"[DataDrivenAbility] ReceiveChannelObjectFromServer: passing '{channelObj.name}' to ChannelAbility");
        channelAbility?.SetChannelObjectReferences(channelObj);
    }

    #endregion

    #region Explosion Ability Logic

    private bool ExecuteExplosionAbility()
    {
        if (config.explosionConfig == null)
        {
            Debug.LogError("[DataDrivenAbility] Explosion config is null!");
            return false;
        }

        OnAbilityActivated();
        SpawnExplosionAbility();
        return true;
    }

    private void SpawnExplosionAbility()
    {
        // Get size modifier from AbilitySize stat
        float sizeMultiplier = 1f;
        Organism organism = GetComponent<Organism>();
        if (organism != null)
        {
            float abilitySizePercent = organism.AllStats.GetStat("AbilitySize");
            if (abilitySizePercent != 0f)
            {
                sizeMultiplier = 1f + (abilitySizePercent);
                Debug.Log($"[SpawnExplosionAbility] AbilitySize stat: +{abilitySizePercent}%, multiplier: {sizeMultiplier}");
            }
        }

        Debug.Log($"[DmgPipeline] <{config.abilityName}> Explosion | sizeMult={sizeMultiplier:F2}x");
        GameObject explosionGO = new GameObject("ExplosionAbility");
        explosionGO.transform.position = GetTargetWorldPosition();

        ExplosionAbility explosionAbility = explosionGO.AddComponent<ExplosionAbility>();
        explosionAbility.SetContext(CreateSubAbilityContext());
        explosionAbility.Initialize(GetEffectiveExplosionConfig(), sizeMultiplier);
    }

    #endregion

    #region Melee Ability Logic

    private bool ExecuteMeleeAbility()
    {
        Debug.Log($"[Melee] ExecuteMeleeAbility: ability={config?.abilityName}, hasMeleeConfig={config?.meleeConfig != null}, directionLocked={isWeaponDirectionLocked}, playerControl={playerControl}, combo={isExecutingCombo}/{currentComboIndex}");

        if (config.meleeConfig == null)
        {
            Debug.LogError("[Melee] meleeConfig is null!");
            return false;
        }

        Vector2 attackDirection = GetAimDirection();

        bool firedFromOffhand = IsCurrentShotFromOffhand();
        PlayerController ownerPlayer = GetComponent<PlayerController>();
        bool isNetworkActive = InstanceFinder.NetworkManager != null &&
            (InstanceFinder.IsServerStarted || InstanceFinder.IsClientStarted);

        if (isNetworkActive && ownerPlayer != null && ownerPlayer.IsOwner && !InstanceFinder.IsServerStarted)
        {
            SpawnMeleeAttack(attackDirection, GetEffectiveMeleeConfig(), firedFromOffhand, visualOnly: true);
            ownerPlayer.ServerRpcExecuteMeleeAbility(abilitySlotIndex, config.abilityName, attackDirection, firedFromOffhand);
        }
        else
        {
            SpawnMeleeAttack(attackDirection, GetEffectiveMeleeConfig(), firedFromOffhand);
        }

        return true;
    }

    public void ExecuteServerMelee(Vector2 attackDirection, bool firedFromOffhand)
    {
        if (!InstanceFinder.IsServerStarted) return;
        SpawnMeleeAttack(attackDirection, GetEffectiveMeleeConfig(), firedFromOffhand);
    }

    private void SpawnMeleeAttack(Vector2 attackDirection, MeleeConfig meleeConfig, bool firedFromOffhand, bool visualOnly = false)
    {
        Debug.Log($"[DmgPipeline] <{config.abilityName}> Melee, firedFromOffhand={firedFromOffhand}");
        MeleeAbility meleeAbility = gameObject.AddComponent<MeleeAbility>();
        meleeAbility.SetContext(CreateSubAbilityContext());
        meleeAbility.PerformAttack(meleeConfig, attackDirection, firedFromOffhand, visualOnly);
        Debug.Log($"[Melee] MeleeAbility.PerformAttack called successfully");
    }

    #endregion

    #region Update & Input Handling

    private void Update()
    {
        if (isTriggeredProjectileOnly)
            return;

        bool movementExecutingNow = movementAbility != null && movementAbility.IsExecuting;
        if (_lastMovementControlLoggedPlayerControl != playerControl
            || _lastMovementControlLoggedMovementExecuting != movementExecutingNow
            || _lastMovementControlLoggedPrecastPending != isMovementPrecastPending
            || _lastMovementControlLoggedToken != movementControlLockToken)
        {
            Debug.Log($"[MovementControl] ability={config?.abilityName ?? "<null>"}, playerControl={playerControl}, movementExecuting={movementExecutingNow}, precastPending={isMovementPrecastPending}, lockToken={movementControlLockToken}");
            _lastMovementControlLoggedPlayerControl = playerControl;
            _lastMovementControlLoggedMovementExecuting = movementExecutingNow;
            _lastMovementControlLoggedPrecastPending = isMovementPrecastPending;
            _lastMovementControlLoggedToken = movementControlLockToken;
        }

        // Update reload progress
        UpdateReload();

        // Manual keybind polling for active trait abilities (Q/E/1-7).
        // Weapon and dash are handled by PlayerController InputActions; autocast handles itself below.
        if (!isComboStep && config != null && config.RequiresKeybind && abilitySlotIndex >= 2 && InputHelper.GetAbilityButtonDown(abilitySlotIndex))
        {
            if (!PlayerController.InputEnabled)
            {
                Debug.Log($"{AbilityPipelineTag} Manual input ignored: ability={config.abilityName}, slot={abilitySlotIndex}, reason=PlayerController.InputEnabled false");
            }
            else if (CursorManager.Instance != null && CursorManager.Instance.IsInUIMode)
            {
                Debug.Log($"{AbilityPipelineTag} Manual input ignored: ability={config.abilityName}, slot={abilitySlotIndex}, reason=UI mode");
            }
            else
            {
                Debug.Log($"{AbilityPipelineTag} Manual input received: ability={config.abilityName}, slot={abilitySlotIndex}");
                bool success = TryUseAbilityManually();
                Debug.Log($"{AbilityPipelineTag} Manual input result: ability={config.abilityName}, slot={abilitySlotIndex}, success={success}");
            }
        }

        // Autocast: fire at nearest enemy on cooldown, no player input required
        if (!isComboStep && config != null && config.autocast)
        {
            float cooldown = CooldownTime;
            bool cooldownReady = Time.time >= _lastAutocastAttempt + cooldown;
            if (cooldownReady && !isOnCooldown)
            {
                float range = config.autocastRange > 0f ? config.autocastRange
                    : (config.projectileConfig != null && config.projectileConfig.maxRange > 0f ? config.projectileConfig.maxRange
                    : (config.areaConfig != null && config.areaConfig.range > 0f ? config.areaConfig.range
                    : (config.explosionConfig != null && config.explosionConfig.activationRange > 0f ? config.explosionConfig.activationRange : 20f)));

                // Pre-collect unique targets before casting so all positions are locked in upfront
                int baseAutocastTargets = config.autocastTargets;
                if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("autocastTargets", out var atAccum))
                {
                    baseAutocastTargets = (int)((baseAutocastTargets + atAccum.flatDelta) * (1f + atAccum.percentDelta / 100f));
                    if (atAccum.hasSetOverride) baseAutocastTargets = (int)atAccum.setNumeric;
                }
                int targetCount = Mathf.Max(1, baseAutocastTargets);

                // castAtFeet is a top-level bool override — only Set mode applies to bools,
                // so check hasSetOverride rather than treating flat/percent deltas as toggles.
                bool effectiveCastAtFeet = config.castAtFeet;
                if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("castAtFeet", out var cafAccum) && cafAccum.hasSetOverride)
                {
                    effectiveCastAtFeet = cafAccum.setNumeric != 0f;
                }
                bool effectiveCastAtTargets = config.castAtTargets;
                if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("castAtTargets", out var catAccum) && catAccum.hasSetOverride)
                {
                    effectiveCastAtTargets = catAccum.setNumeric != 0f;
                }
                bool effectiveCastAtFriendlyTargets = config.castAtFriendlyTargets;
                if (_accumulatedOverrides != null && _accumulatedOverrides.TryGetValue("castAtFriendlyTargets", out var caftAccum) && caftAccum.hasSetOverride)
                {
                    effectiveCastAtFriendlyTargets = caftAccum.setNumeric != 0f;
                }

                var targets = new List<Vector3>(targetCount + 2);
                var seen = new HashSet<Vector3>();
                if (effectiveCastAtFeet)
                {
                    if (seen.Add(transform.position))
                        targets.Add(transform.position);
                }
                if (effectiveCastAtTargets)
                {
                    for (int t = 0; t < targetCount; t++)
                    {
                        Vector3? pos = FindAutocastTarget(range, seen);
                        if (!pos.HasValue) break;
                        if (seen.Add(pos.Value))
                            targets.Add(pos.Value);
                    }
                }
                if (effectiveCastAtFriendlyTargets)
                {
                    for (int t = 0; t < targetCount; t++)
                    {
                        Vector3? pos = FindFriendlyAutocastTarget(range, seen);
                        if (!pos.HasValue) break;
                        if (seen.Add(pos.Value))
                            targets.Add(pos.Value);
                    }
                }

                // If no explicit target mode is active (or no candidates found),
                // cast randomly within range and respect autocastTargets count.
                if (targets.Count == 0)
                {
                    for (int t = 0; t < targetCount; t++)
                    {
                        targets.Add(FindRandomAutocastPoint(range));
                    }
                }


                // Get multicast count from player stats (minimum 1 cast per target)
                int multicastCount = 1;
                if (config.canMulticast && ownerOrganism != null && ownerOrganism.AllStats != null)
                {
                    multicastCount = Mathf.Max(1, Mathf.RoundToInt(ownerOrganism.AllStats.GetStat("Multicast")));
                }

                // Cast once per pre-collected target, multicastCount times each.
                // _autocastBurstActive suppresses the cooldown gate in CanUseAbility so all
                // burst casts fire even though StartCooldown() is called after the first one.
                int successfulCasts = 0;
                bool firstCast = true;
                _autocastBurstActive = true;
                try
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        for (int m = 0; m < multicastCount; m++)
                        {
                            _autocastTarget = targets[i];
                            if (firstCast)
                            {
                                _lastAutocastAttempt = Time.time;
                                firstCast = false;
                            }
                            bool fired = TryUseAbility();
                            if (fired) successfulCasts++;
                            _autocastTarget = null;
                        }
                    }
                }
                finally
                {
                    _autocastBurstActive = false;
                }
            }
        }

        // Log movement ability state periodically (every 0.5s) for diagnostics
        if (config != null && config.isMovementAbility && Time.frameCount % 30 == 0)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
        if (config != null && config.isMovementAbility && Time.frameCount % 30 == 0)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        // Check if rotation lock has expired
        if (isWeaponDirectionLocked && rotationLockEndTime > 0f && Time.time >= rotationLockEndTime)
        {
            Debug.Log($"<color=green>[DataDrivenAbility] Rotation lock duration expired ({config?.rotationLockDuration}s) - RELEASING weapon direction lock</color>");
            isWeaponDirectionLocked = false;
            isMainhandLocked = false;
            isOffhandLocked = false;
            rotationLockEndTime = 0f;
        }

        // Movement ability logic - update movement and check if complete
        // This MUST run regardless of combo state to stop movement at the right time
        if (movementAbility != null && movementAbility.IsExecuting)
        {
            movementAbility.UpdateMovement();

            // Check if movement ended
            if (!movementAbility.IsExecuting)
            {
                Debug.Log($"[Movement] Movement ended — isWeaponDirectionLocked={isWeaponDirectionLocked}, isExecutingCombo={isExecutingCombo}, comboIndex={currentComboIndex}");

                // Force PlayerController to update character animations immediately
                // Force animation refresh to prevent character getting stuck in attack pose
                ownerAsPlayer?.ForceAnimationUpdate();
                Debug.Log($"[Movement] ForceAnimationUpdate called — weaponIdleCoroutineActive={weaponIdleReturnCoroutine != null}");
            }
        }

        // A combo-chain shell is executing its own coroutine sequence.
        if (isExecutingCombo && config != null && config.isCombo)
        {
            return;
        }

        // Handle hold-to-fire/autocast for ANY ability type
        // Works regardless of ability config or type - checks button state for this ability's slot
        if (isHoldingFire)
        {
            if (!IsAbilityButtonHeld())
            {
                StopContinuousFiring();
            }
            else if (!isActivatingWeapon && !isReloading && !isCharging) // Don't fire while weapon is activating/drawing, reloading, or charging
            {
                // Check if it's time to fire again based on cooldown/attack speed interval
                float fireInterval = CooldownTime;
                if (Time.time >= lastFireTime + fireInterval)
                {
                    // Check if we can still fire (energy, charges, alive)
                    // Note: We don't check isOnCooldown here because we already handle timing with lastFireTime/fireInterval
                    if (ownerOrganism != null && ownerOrganism.IsAlive)
                    {
                        // Check charges
                        if (config?.hasCharges == true && currentCharges <= 0)
                        {
                            // Can't fire - out of charges
                        }
                        // Check energy
                        else if (GetEffectiveEnergyCost() > 0 && ownerOrganism.CurrentEnergy < GetEffectiveEnergyCost())
                        {
                            StopContinuousFiring();
                        }
                        // Check ammo (projectile-specific)
                        else if (config?.usesAmmo == true && currentAmmo <= 0)
                        {
                            StartReload();
                            StopContinuousFiring();
                        }
                        else
                        {
                            // Try to use the ability again (handles all ability types)
                            bool success = TryUseAbility();
                            if (success)
                            {
                                lastFireTime = Time.time;
                            }
                        }
                    }
                    else
                    {
                        StopContinuousFiring();
                    }
                }
            }
        }

        // Beam ability handles its own updates internally

        // Handle reload input for abilities with ammo (works even without config check)
        if (config?.usesAmmo == true && InputHelper.GetReload)
        {
            StartReload();
        }

        // Channel abilities are now managed by ChannelAbility component
    }

    /// <summary>
    /// Check if the button for THIS ability's slot is currently held
    /// Works dynamically based on which slot the ability is bound to
    /// </summary>
    private bool IsAbilityButtonHeld()
    {
        // Route through the centralized slot-aware helper so slot 2 (Space) works correctly
        return InputHelper.IsAbilityButtonHeld(abilitySlotIndex);
    }

    #endregion

    #region Lifecycle

    private void OnDisable()
    {
        StopContinuousFiring();

        ClearCastMoveSpeedModifier("OnDisable");

        // End movement ability if still executing (prevents stuck movement state on scene transitions/teleport)
        if (movementAbility != null && movementAbility.IsExecuting)
        {
            movementAbility.End();
            Debug.Log($"[DataDrivenAbility] OnDisable ended movement ability for {config?.abilityName}");
        }

        // Always return control to player when disabled
        if (!playerControl)
        {
            ReleaseMovementControl("OnDisable", force: true);
            Debug.Log($"[DataDrivenAbility] OnDisable returned playerControl for {config?.abilityName}");
        }

        // Channel abilities are managed by ChannelAbility component

        // BeamAbility handles its own cleanup in OnDisable

        // Clean up weapon activation
        if (weaponActivationCoroutine != null)
        {
            StopCoroutine(weaponActivationCoroutine);
            weaponActivationCoroutine = null;
            isActivatingWeapon = false;
        }
    }

    /// <summary>
    /// Force-reset all ability states. Called by PlayerController during scene/arena transitions
    /// to ensure no stale blocking states remain.
    /// </summary>
    public void ForceResetAbilityState()
    {
        // End movement ability
        if (movementAbility != null && movementAbility.IsExecuting)
        {
            movementAbility.End();
            Debug.Log($"[DataDrivenAbility] ForceReset ended movement ability for {config?.abilityName}");
        }

        // Return control to player
        playerControl = true;

        // Stop continuous firing
        StopContinuousFiring();

        // Reset weapon activation
        if (weaponActivationCoroutine != null)
        {
            StopCoroutine(weaponActivationCoroutine);
            weaponActivationCoroutine = null;
        }
        isActivatingWeapon = false;
        isWeaponDirectionLocked = false;
    }

    private void OnDestroy()
    {
        DestroyComboSteps();

        // Unsubscribe retaliation handler so the event doesn't fire on a destroyed ability
        if (config != null && config.retaliationCast && ownerOrganism != null)
        {
            ownerOrganism.OnBlock -= HandleRetaliationHit;
            ownerOrganism.OnDamageTaken -= HandleRetaliationHit;
        }

        // Clean up the owned MovementAbility component (one per DataDrivenAbility instance)
        if (movementAbility != null)
        {
            Destroy(movementAbility);
        }

        // Clean up all active constructs when ability is destroyed
        foreach (GameObject construct in activeConstructs)
        {
            if (construct != null)
            {
                Destroy(construct);
            }
        }
        activeConstructs.Clear();

        // Clean up all active summons when ability is destroyed
        foreach (GameObject summon in activeSummons)
        {
            if (summon != null)
            {
                Destroy(summon);
            }
        }
        activeSummons.Clear();
    }

    #endregion
}
