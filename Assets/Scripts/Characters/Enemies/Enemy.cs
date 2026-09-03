using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Component.Transforming;
using FishNet.Component.Animating;
using JoeConticello.VisualEffects;

public class Enemy : Organism
{
    // Static flag to disable all enemy actions during loading screens
    private static bool actionsEnabled = true;
    public static bool ActionsEnabled
    {
        get => actionsEnabled;
        set
        {
            actionsEnabled = value;
            Debug.Log($"[Enemy] Actions {(value ? "Enabled" : "Disabled")}");
        }
    }

    [Header("Enemy Configuration")]
    [SerializeField] private EnemyConfig config;

    [Header("Death VFX")]
    [Tooltip("Particle-system prefab spawned at the enemy's position on death.")]
    [SerializeField] private GameObject deathVFXPrefab;

    [Header("Level Scaling")]
    private bool levelScalingApplied = false;

    // Runtime values from config
    protected float detectionRange;
    protected bool canMove;
    protected float knockbackDuration;
    // Data-driven ability system
    protected List<EnemyAbilityInstance> abilityInstances = new List<EnemyAbilityInstance>();

    protected Transform targetTransform;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected Collider2D combatCollider;
    protected bool isChasing = false;
    protected bool isKnockedBack = false;
    protected float knockbackTimer = 0f;

    private AIPathfinding _pathfinding;
    private EffectManager effectManager;
    private float ABILITY_ATTEMPT_INTERVAL = 1f; // Interval between ability attempts
    private float nextAbilityAttemptTime = 0f; // Next time an ability can be attempted
    // Fake mouse for player-like aiming system
    private GameObject fakeMouse;
    private WeaponSortingManager weaponSortingManager;
    private WeaponSortingManager.Direction currentAimDirection = WeaponSortingManager.Direction.SouthEast;

    // Network weapon tracking
    private NetworkObject _currentWeaponNOB;
    private NetworkObject _currentOffHandWeaponNOB;

    // Finite state machine driving all AI behavior (server-side).
    private readonly EnemyStateMachine stateMachine = new EnemyStateMachine();
    private float distanceToTarget = float.MaxValue;
    private Vector3 spawnPosition;
    private const float KITE_DISTANCE = 2.5f;
    private const float DEFAULT_RETREAT_HEALTH_PERCENT = 15f;

    // Behavior state instances (one set per enemy so per-state fields stay isolated).
    public IdleState IdleBehavior { get; } = new IdleState();
    public ChaseState ChaseBehavior { get; } = new ChaseState();
    public AttackState AttackBehavior { get; } = new AttackState();
    public CastingState CastingBehavior { get; } = new CastingState();
    public StrafeState StrafeBehavior { get; } = new StrafeState();
    public RetreatState RetreatBehavior { get; } = new RetreatState();
    public PatrolState PatrolBehavior { get; } = new PatrolState();

    // Collision damage tracking
    private float collisionDamageTimer = 0f;

    // Runtime scaling set by MobSpawner
    private float runtimeDamageMultiplier = 1f;

    protected override void Awake()
    {
        base.Awake();
        effectManager = GetComponent<EffectManager>();
        if (effectManager == null)
        {
            effectManager = GetComponentInChildren<EffectManager>();
        }
        WorldHealthBar healthBar = GetComponentInChildren<WorldHealthBar>();
        if (config == null)
        {
            Debug.LogError($"[Enemy] {gameObject.name} has no EnemyConfig assigned!");
            return;
        }
        config.stats.CopyToStatContainer(statContainer);

        // Re-initialize health after stats are copied (base.Awake set it to 0)
        float maxHealth = statContainer.GetStat("MaxHealth");
        if (maxHealth > 0)
        {
            ModifyHealth(maxHealth - CurrentHealth); // Set to full health
        }
        

        // Initialize basic values from config
        detectionRange = config.detectionRange;
        canMove = config.canMove;
        knockbackDuration = 0.3f; // Default knockback duration

        // Initialize AIPathfinding component
        _pathfinding = gameObject.AddComponent<AIPathfinding>();
        _pathfinding.Initialize(config.pathfindingObstacleLayers, config.obstacleAvoidanceStrength,
            debug: config.debugDrawPathfindingRays);

        // Initialize data-driven ability system
        foreach (var abilitySlot in config.abilities)
        {
            if (abilitySlot.abilityConfig != null)
            {
                DataDrivenAbility abilityComponent = gameObject.AddComponent<DataDrivenAbility>();
                abilityComponent.SetAbilityReference(new AbilityReference(abilitySlot.abilityConfig));
                abilityComponent.InitializeAbility(); // Initialize immediately after setting reference

                abilityInstances.Add(new EnemyAbilityInstance
                {
                    ability = abilityComponent,
                    config = abilitySlot.abilityConfig,
                    range = abilitySlot.range,
                    priority = abilitySlot.priority
                });
            }
        }

        // Sort abilities by priority (highest first)
        abilityInstances.Sort((a, b) => b.priority.CompareTo(a.priority));

        // Get components from root GameObject or children
        Rigidbody2D foundRb = GetComponentInChildren<Rigidbody2D>();
        spriteRenderer = transform.Find("Visuals") != null ? transform.Find("Visuals").GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
        combatCollider = GetComponentInChildren<Collider2D>();

        // Simple enemies skip the weapon aiming / fake-mouse system entirely
        if (!config.isSimpleEnemy)
        {
            // Create fake mouse for aiming system (enemies aim at this instead of real mouse)
            fakeMouse = new GameObject("FakeMouse");
            fakeMouse.transform.SetParent(transform);
            fakeMouse.transform.localPosition = Vector3.zero;
            fakeMouse.tag = "FakeMouse"; // Tag for InputUtility to detect
        }

        // Initialize weapon sorting manager
        weaponSortingManager = gameObject.AddComponent<WeaponSortingManager>();
        if (config.mainHandWeaponConfig != null)
        {
            // For enemies, we'll create a minimal character data just for weapon sorting
            weaponSortingManager.Initialize(spriteRenderer, null);
        }

        // CRITICAL: Rigidbody2D MUST be on the root GameObject for movement to work
        // If it's on a child, the child moves but the parent stays still
        if (foundRb != null && foundRb.gameObject != gameObject)
        {
            // Destroy the child's Rigidbody2D (can't have Rigidbody2D in parent-child hierarchy)
            DestroyImmediate(foundRb);

            // Add new Rigidbody2D to root for movement
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        else if (foundRb != null)
        {
            rb = foundRb;
        }
        else
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }


        // Ensure enemy is on correct layer for player projectiles to hit
        if (gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            gameObject.layer = LayerMask.NameToLayer("Enemy");
        }

        // Configure rigidbody
        if (rb != null)
        {
            rb.linearDamping = 2f;
            rb.gravityScale = 0; // Top-down game, no gravity
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Prevent rotation
        }

        // Find initial target (prefer player)
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            targetTransform = player.transform;
        }

        // Store spawn position for patrol/retreat actions
        spawnPosition = transform.position;

        // Start in the appropriate movement state.
        stateMachine.ChangeState(this, SelectMovementState());

        // Add WeaponSortingManager if enemy has weapons (needed on all clients for sorting)
        if (config.mainHandWeaponConfig != null || config.offhandWeaponConfig != null)
        {
            WeaponSortingManager sortingManager = GetComponent<WeaponSortingManager>();
            if (sortingManager == null)
            {
                sortingManager = gameObject.AddComponent<WeaponSortingManager>();
            }
        }

        // NOTE: Weapon equipping moved to OnStartServer so weapons are network-spawned
    }

    /// <summary>
    /// Server-side: spawn weapons as networked objects so all clients see them.
    /// Deferred by one frame to avoid spawning NetworkObjects inside the parent's
    /// spawn callback, which causes duplicate-key errors in FishNet's client cache.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(DeferredNetworkEquipWeapons());
    }

    /// <summary>
    /// On every non-server machine, make the Rigidbody2D kinematic. AI movement (HandleUpdate)
    /// is already server-only, but the body itself stayed Dynamic everywhere, so every client's
    /// local physics independently resolved player-enemy collisions with their own push impulse —
    /// diverging from the server's authoritative result and desyncing the player's position.
    /// Kinematic bodies still physically shove Dynamic bodies (the player) on contact, but never
    /// receive force/collision response themselves, so only the server's copy can drift.
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsServerStarted && rb != null)
            rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private IEnumerator DeferredNetworkEquipWeapons()
    {
        yield return null; // Wait one frame so the enemy's own spawn is fully processed
        if (this != null && IsServerStarted)
        {
            NetworkEquipWeapons();
        }
    }

    /// <summary>
    /// Network-spawn weapons so they are visible to all clients.
    /// Follows the same pattern as PlayerController.SpawnWeaponPairOnServer.
    /// </summary>
    private void NetworkEquipWeapons()
    {
        if (config == null) return;

        // ── Main hand ────────────────────────────────────────────────────────
        if (config.mainHandWeaponConfig != null)
        {
            WeaponSettings settings = config.mainHandWeaponConfig.ToWeaponSettings();
            if (settings.weaponPrefab != null)
            {
                // Ensure WeaponHolder exists on server (clients get it via ObserversRpc)
                WeaponHolder weaponHolder = GetComponent<WeaponHolder>();
                if (weaponHolder == null)
                    weaponHolder = gameObject.AddComponent<WeaponHolder>();

                // Instantiate and network-spawn the weapon
                GameObject mainWeapon = Instantiate(settings.weaponPrefab);
                _currentWeaponNOB = mainWeapon.GetComponent<NetworkObject>();

                if (_currentWeaponNOB == null)
                {
                    Debug.LogError($"[Enemy] Weapon prefab '{settings.weaponPrefab.name}' has no NetworkObject! " +
                                   "Run Tools > Add Network Components to Weapons.");
                    Destroy(mainWeapon);
                }
                else
                {
                    InstanceFinder.ServerManager.Spawn(mainWeapon);
                    _currentWeaponNOB.SetParent(this.NetworkObject);
                    mainWeapon.transform.localPosition = Vector3.zero;
                    mainWeapon.transform.localRotation = Quaternion.identity;

                    // Grant weapon abilities (server only)
                    if (config.useWeaponGrantedAbilities && config.mainHandWeaponConfig.grantedPrimaryAbility != null)
                    {
                        AbilityDataConfig weaponAbility = config.mainHandWeaponConfig.grantedPrimaryAbility as AbilityDataConfig;
                        if (weaponAbility != null)
                        {
                            DataDrivenAbility abilityComponent = gameObject.AddComponent<DataDrivenAbility>();
                            abilityComponent.SetAbilityReference(new AbilityReference(weaponAbility));
                            abilityComponent.InitializeAbility();

                            abilityInstances.Add(new EnemyAbilityInstance
                            {
                                ability = abilityComponent,
                                config = weaponAbility,
                                range = config.weaponAbilityRange,
                                priority = 100
                            });
                        }
                        else
                        {
                            Debug.LogWarning($"[Enemy] {gameObject.name} weapon grantedPrimaryAbility is not AbilityDataConfig!");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[Enemy] {gameObject.name} has mainHandWeaponConfig but no weapon prefab!");
            }
        }

        // ── Offhand ──────────────────────────────────────────────────────────
        if (config.offhandWeaponConfig != null)
        {
            WeaponSettings offhandSettings = config.offhandWeaponConfig.ToWeaponSettings();
            if (offhandSettings.weaponPrefab != null)
            {
                OffHandWeaponHolder offhandHolder = GetComponent<OffHandWeaponHolder>();
                if (offhandHolder == null)
                    offhandHolder = gameObject.AddComponent<OffHandWeaponHolder>();

                GameObject offHandWeapon = Instantiate(offhandSettings.weaponPrefab);
                _currentOffHandWeaponNOB = offHandWeapon.GetComponent<NetworkObject>();

                if (_currentOffHandWeaponNOB == null)
                {
                    Debug.LogError($"[Enemy] OffHand prefab '{offhandSettings.weaponPrefab.name}' has no NetworkObject!");
                    Destroy(offHandWeapon);
                }
                else
                {
                    InstanceFinder.ServerManager.Spawn(offHandWeapon);
                    _currentOffHandWeaponNOB.SetParent(this.NetworkObject);
                    offHandWeapon.transform.localPosition = Vector3.zero;
                    offHandWeapon.transform.localRotation = Quaternion.identity;

                    // Grant offhand weapon abilities
                    if (config.useWeaponGrantedAbilities && config.offhandWeaponConfig.grantedPrimaryAbility != null)
                    {
                        AbilityDataConfig offhandAbility = config.offhandWeaponConfig.grantedPrimaryAbility as AbilityDataConfig;
                        if (offhandAbility != null)
                        {
                            DataDrivenAbility abilityComponent = gameObject.AddComponent<DataDrivenAbility>();
                            abilityComponent.SetAbilityReference(new AbilityReference(offhandAbility));
                            abilityComponent.InitializeAbility();

                            abilityInstances.Add(new EnemyAbilityInstance
                            {
                                ability = abilityComponent,
                                config = offhandAbility,
                                range = config.weaponAbilityRange,
                                priority = 99
                            });
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"[Enemy] {gameObject.name} has offhandWeaponConfig but no weapon prefab!");
            }
        }

        // Re-sort abilities after potentially adding weapon abilities
        abilityInstances.Sort((a, b) => b.priority.CompareTo(a.priority));

        // Tell all clients to set up weapon visuals under WeaponHolder
        ObserversRpcSetupEnemyWeaponVisuals(_currentWeaponNOB, _currentOffHandWeaponNOB);
    }

    /// <summary>
    /// Runs on ALL clients (including server via RunLocally) to parent the
    /// FishNet-spawned weapon under WeaponHolder and configure visuals.
    /// BufferLast = true so late-joining clients also receive this.
    /// </summary>
    [ObserversRpc(BufferLast = true, RunLocally = true)]
    private void ObserversRpcSetupEnemyWeaponVisuals(
        NetworkObject mainWeaponNOB,
        NetworkObject offHandWeaponNOB)
    {
        if (mainWeaponNOB != null)
        {
            WeaponHolder weaponHolder = GetComponent<WeaponHolder>();
            if (weaponHolder == null)
                weaponHolder = gameObject.AddComponent<WeaponHolder>();
            weaponHolder.SetupNetworkWeapon(mainWeaponNOB.gameObject);
        }

        if (offHandWeaponNOB != null)
        {
            OffHandWeaponHolder offHandHolder = GetComponent<OffHandWeaponHolder>();
            if (offHandHolder == null)
                offHandHolder = gameObject.AddComponent<OffHandWeaponHolder>();
            offHandHolder.SetupNetworkWeapon(offHandWeaponNOB.gameObject);
        }

    }

    /// <summary>
    /// Syncs weapon sorting order changes to all clients.
    /// Called from UpdateWeaponAiming when sorting changes.
    /// </summary>
    [ObserversRpc(RunLocally = true)]
    private void ObserversRpcSyncWeaponSorting(int mainSortingOrder, string sortingLayerName)
    {
        WeaponHolder weaponHolder = GetComponent<WeaponHolder>();
        if (weaponHolder == null || !weaponHolder.HasWeapon()) return;

        GameObject weapon = weaponHolder.GetCurrentWeapon();
        if (weapon == null) return;

        SpriteRenderer weaponRenderer = null;
        Transform weaponSpriteChild = weapon.transform.Find("WeaponSprite");
        if (weaponSpriteChild != null)
            weaponRenderer = weaponSpriteChild.GetComponent<SpriteRenderer>();
        if (weaponRenderer == null)
        {
            foreach (SpriteRenderer sr in weapon.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sr.gameObject.name.Contains("HandHolder"))
                {
                    weaponRenderer = sr;
                    break;
                }
            }
        }

        if (weaponRenderer != null)
        {
            weaponRenderer.sortingLayerName = sortingLayerName;
            weaponRenderer.sortingOrder = mainSortingOrder;

            // Update HandHolder sprites
            foreach (Transform child in weapon.GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains("HandHolder"))
                {
                    SpriteRenderer handSR = child.GetComponent<SpriteRenderer>();
                    if (handSR != null)
                    {
                        handSR.sortingLayerName = sortingLayerName;
                        handSR.sortingOrder = mainSortingOrder + 1;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Cast the highest-priority ability that is in range, aimed at the current target.
    /// Cooldowns and costs are owned by each AbilityDataConfig; this only schedules when the
    /// next cast is attempted so the state machine is not hammering TryUseAbility every frame.
    /// </summary>
    public bool TryUseAbilities() => TryUseAbilities(distanceToTarget);

    private bool TryUseAbilities(float distanceToTarget)
    {
        if (Time.time < nextAbilityAttemptTime)
            return false;

        Vector3 targetPosition = targetTransform != null
            ? targetTransform.position
            : transform.position + transform.right;

        foreach (var instance in abilityInstances)
        {
            if (instance.ability == null || distanceToTarget > instance.range)
                continue;

            if (instance.ability.GetRemainingCooldown() > 0f)
                continue;

            // Passing the target as the cast position is the enemy equivalent of the player's cursor.
            if (instance.ability.TryUseAbilityAt(targetPosition))
            {
                nextAbilityAttemptTime = Time.time + Mathf.Max(ABILITY_ATTEMPT_INTERVAL, instance.ability.GetRemainingCooldown());
                return true;
            }
        }

        // Nothing fired — wait for the soonest cooldown instead of retrying next frame.
        nextAbilityAttemptTime = Time.time + Mathf.Max(ABILITY_ATTEMPT_INTERVAL, GetShortestRemainingCooldown(distanceToTarget));
        return false;
    }

    /// <summary>
    /// Shortest remaining cooldown across in-range abilities, or 0 when one is already ready.
    /// </summary>
    private float GetShortestRemainingCooldown(float distanceToTarget)
    {
        float shortest = float.MaxValue;
        foreach (var instance in abilityInstances)
        {
            if (instance.ability == null || distanceToTarget > instance.range)
                continue;

            shortest = Mathf.Min(shortest, instance.ability.GetRemainingCooldown());
        }

        return shortest == float.MaxValue ? 0f : Mathf.Max(0f, shortest);
    }

    /// <summary>
    /// True when at least one ability is in range and off cooldown.
    /// </summary>
    public bool HasAbilityReady() => HasAbilityReady(distanceToTarget);

    private bool HasAbilityReady(float distanceToTarget)
    {
        foreach (var instance in abilityInstances)
        {
            if (instance.ability != null
                && distanceToTarget <= instance.range
                && instance.ability.GetRemainingCooldown() <= 0f)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True while any ability's precast/cast sequence is actively animating, regardless of
    /// whether that ability locks movement. Used so the state machine hands full control to
    /// <see cref="CastingState"/> and never overwrites the ability's animation mid-cast.
    /// </summary>
    public bool IsAnyAbilityBusy()
    {
        foreach (var instance in abilityInstances)
        {
            if (instance.ability != null && (instance.ability.IsCastSequenceActive || instance.ability.IsPerformingAbility))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get the config for a specific ability by index
    /// </summary>
    public AbilityDataConfig GetAbilityConfig(int index)
    {
        if (index >= 0 && index < abilityInstances.Count)
        {
            return abilityInstances[index].config;
        }
        return null;
    }

    /// <summary>
    /// Get the primary (highest priority) ability config
    /// </summary>
    public AbilityDataConfig GetPrimaryAbilityConfig()
    {
        return GetAbilityConfig(0);
    }

    /// <summary>
    /// Update weapon aiming to point at target (orbital aiming)
    /// </summary>
    /// <summary>
    /// Update weapon aiming. Pass invertAim=true for retreat/kite to aim away from target.
    /// Uses player-like aiming system with fake mouse and weapon sorting.
    /// </summary>
    private void UpdateWeaponAiming(bool invertAim = false)
    {
        if (targetTransform == null || spriteRenderer == null || fakeMouse == null) return;

        // Calculate direction to target (or opposite if inverting)
        Vector2 direction = (targetTransform.position - transform.position).normalized;
        if (invertAim)
        {
            direction = -direction;
        }

        // Position fake mouse in front of enemy in the aim direction
        // This makes the ability system think the "mouse" is there
        float fakeMouseDistance = 5f; // Distance in front of enemy
        Vector3 fakeMouseWorldPos = transform.position + (Vector3)direction * fakeMouseDistance;
        fakeMouse.transform.position = fakeMouseWorldPos;

        // Calculate angle for weapon positioning
        float angle = Mathf.Atan2(direction.y, direction.x);
        float angleDeg = angle * Mathf.Rad2Deg;

        // Get weapon holder and weapon transform
        WeaponHolder weaponHolder = GetComponent<WeaponHolder>();
        if (weaponHolder == null || !weaponHolder.HasWeapon()) return;

        Transform weaponHolderTransform = transform.Find("WeaponHolder");
        if (weaponHolderTransform == null) return;

        Transform weaponTransform = weaponHolderTransform.Find("Weapon");
        if (weaponTransform == null) return;

        // Get weapon settings
        if (config.mainHandWeaponConfig == null) return;
        WeaponSettings settings = config.mainHandWeaponConfig.ToWeaponSettings();

        // Determine aim direction enum (4 quadrants)
        WeaponSortingManager.Direction aimDir = GetAimDirectionEnum(angleDeg);
        currentAimDirection = aimDir; // Store for animation selection

        // Get directional offset based on aim direction
        Vector2 weaponOffset = GetWeaponOffsetForDirection(settings, aimDir);

        // Position weapon using radius + offset (like player system)
        float radius = settings.aimingRadius;
        Vector3 weaponPosition = new Vector3(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius,
            0f
        );
        weaponPosition += new Vector3(weaponOffset.x, weaponOffset.y, 0f);

        // Apply rotation directly from the current aim angle.
        weaponTransform.rotation = Quaternion.Euler(0, 0, angleDeg);
        weaponTransform.localPosition = weaponPosition;

        // Log weapon aiming info

        // Handle weapon flipping
        Vector3 weaponScale = weaponTransform.localScale;
        if (settings.flipWeaponOnTurn)
        {
            // Normalize angle to 0-360
            float normalizedAngle = angleDeg;
            if (normalizedAngle < 0) normalizedAngle += 360;
            bool shouldFlip = normalizedAngle > 90f && normalizedAngle < 270f;

            if (settings.flipWeaponOnXAxis)
            {
                weaponScale.x = shouldFlip ? -Mathf.Abs(weaponScale.x) : Mathf.Abs(weaponScale.x);
            }
            if (settings.flipWeaponOnYAxis)
            {
                weaponScale.y = shouldFlip ? -Mathf.Abs(weaponScale.y) : Mathf.Abs(weaponScale.y);
            }
        }
        weaponTransform.localScale = weaponScale;

        // Update weapon sorting using WeaponSortingManager and sync to clients
        if (weaponSortingManager != null)
        {
            bool weaponBehind = weaponSortingManager.ShouldWeaponBeBehind(aimDir, settings);

            if (spriteRenderer != null)
            {
                int newSortingOrder = spriteRenderer.sortingOrder + (weaponBehind ? -10 : 10);
                string sortingLayer = spriteRenderer.sortingLayerName;
                // Don't queue this per-frame RPC while the server is tearing down the GameScene
                // for a CommandScene return — it would risk colliding with this enemy's despawn
                // in the same reliable batch and corrupt the stream (see NetworkSceneTransition).
                
            }
        }
    }

    /// <summary>
    /// Convert angle to direction enum (matches player system)
    /// </summary>
    private WeaponSortingManager.Direction GetAimDirectionEnum(float angle)
    {
        // Normalize angle to 0-360
        if (angle < 0) angle += 360;

        // 4 diagonal directions (90-degree quadrants)
        if (angle >= 22.5f && angle < 90f)
            return WeaponSortingManager.Direction.NorthEast;
        else if (angle >= 90f && angle < 156.5f)
            return WeaponSortingManager.Direction.NorthWest;
        else if (angle >= 156.5f && angle < 270f)
            return WeaponSortingManager.Direction.SouthWest;
        else
            return WeaponSortingManager.Direction.SouthEast;
    }

    /// <summary>
    /// Get weapon offset based on aim direction (matches player system)
    /// </summary>
    private Vector2 GetWeaponOffsetForDirection(WeaponSettings settings, WeaponSortingManager.Direction direction)
    {
        switch (direction)
        {
            case WeaponSortingManager.Direction.NorthEast:
                return settings.northEastOffset;
            case WeaponSortingManager.Direction.NorthWest:
                return settings.northWestOffset;
            case WeaponSortingManager.Direction.SouthEast:
                return settings.southEastOffset;
            case WeaponSortingManager.Direction.SouthWest:
                return settings.southWestOffset;
            default:
                return Vector2.zero;
        }
    }

    protected virtual void OnEnable()
    {
        PlayerController.OnPlayerSpawned += HandlePlayerSpawned;
    }

    protected virtual void OnDisable()
    {
        PlayerController.OnPlayerSpawned -= HandlePlayerSpawned;
    }

    protected virtual void OnDestroy()
    {
        // Clean up fake mouse GameObject
        if (fakeMouse != null)
        {
            Destroy(fakeMouse);
        }
    }

    private void HandlePlayerSpawned(PlayerController newPlayer)
    {
        targetTransform = newPlayer.transform;
    }

    /// <summary>
    /// Check if enemy has a specific action configured
    /// </summary>
    public bool HasAction(EnemyActionType actionType)
    {
        if (config == null || config.actions == null) return false;
        return config.actions.Exists(a => a.actionType == actionType);
    }

    /// <summary>
    /// Get action config for a specific action type
    /// </summary>
    public EnemyActionConfig GetAction(EnemyActionType actionType)
    {
        if (config == null || config.actions == null) return null;
        return config.actions.Find(a => a.actionType == actionType);
    }

    /// <summary>
    /// Furthest range the enemy can currently attack from. Weapon-granted abilities are
    /// registered with EnemyConfig.weaponAbilityRange, so they are covered here too.
    /// </summary>
    private float GetAttackRange()
    {
        float maxRange = 0f;
        foreach (var instance in abilityInstances)
        {
            if (instance.ability != null && instance.range > maxRange)
                maxRange = instance.range;
        }

        return maxRange;
    }

    /// <summary>
    /// Central combat-state selection shared by every state's transition check. Encodes the
    /// behavior priority ladder (retreat &gt; patrol/idle &gt; attack &gt; strafe &gt; chase) purely
    /// from the enemy's configured actions, current health, and distance to target.
    /// </summary>
    public IEnemyState SelectMovementState()
    {
        EnemyActionConfig retreatAction = GetAction(EnemyActionType.Retreat);

        // Flee when health drops below the retreat threshold.
        if (retreatAction != null)
        {
            float threshold = retreatAction.healthPercentThreshold >= 0f
                ? retreatAction.healthPercentThreshold
                : DEFAULT_RETREAT_HEALTH_PERCENT;

            if (HealthPercent <= threshold)
                return RetreatBehavior;
        }

        // No target: patrol around spawn if able, otherwise stand idle.
        if (!HasTarget)
            return HasAction(EnemyActionType.Patrol) ? (IEnemyState)PatrolBehavior : IdleBehavior;

        // Ranged kite: back off when the target closes inside kite distance.
        if (retreatAction != null && distanceToTarget <= KITE_DISTANCE)
            return RetreatBehavior;

        // Within attack range: fire if ready, else strafe (ranged) or hold and wait (melee).
        if (distanceToTarget <= GetAttackRange())
        {
            if (HasAbilityReady())
                return AttackBehavior;

            if (HasAction(EnemyActionType.Strafe))
                return StrafeBehavior;

            return AttackBehavior; // Hold position, wait for cooldown.
        }

        // Out of range: chase if the enemy can move, otherwise stand and wait to be approached.
        if (canMove)
            return ChaseBehavior;

        return AttackBehavior;
    }

    /// <summary>Percentage of max health remaining (0-100).</summary>
    public float HealthPercent => MaxHealth > 0f ? (CurrentHealth / MaxHealth) * 100f : 0f;

    /// <summary>True when a live target is within detection range.</summary>
    public bool HasTarget => targetTransform != null && distanceToTarget <= detectionRange;

    /// <summary>Current target transform, or null.</summary>
    public Transform Target => targetTransform;

    /// <summary>Config asset for this enemy.</summary>
    public EnemyConfig Config => config;

    /// <summary>Spawn position, used as the patrol anchor.</summary>
    public Vector3 SpawnPosition => spawnPosition;

    /// <summary>Distance to the current target (cached each server tick).</summary>
    public float DistanceToTarget() => distanceToTarget;

    /// <summary>Normalized direction from this enemy toward its target (zero if no target).</summary>
    public Vector2 DirectionToTarget()
    {
        if (targetTransform == null) return Vector2.zero;
        return ((Vector2)(targetTransform.position - transform.position)).normalized;
    }

    /// <summary>Movement speed multiplier for a configured action, or a fallback if unconfigured.</summary>
    public float GetActionSpeedMultiplier(EnemyActionType actionType, float fallback)
    {
        EnemyActionConfig action = GetAction(actionType);
        return action != null ? action.movementSpeedMultiplier : fallback;
    }

    /// <summary>Steer toward a preferred direction and drive the walk animation.</summary>
    public void MoveInDirection(Vector2 preferredDirection, float speedMultiplier)
    {
        if (rb == null) return;

        Vector2 steered = CalculateBestMovementDirection(preferredDirection);
        float moveSpeed = statContainer.GetStat("MoveSpeed") * speedMultiplier;
        rb.linearVelocity = steered * moveSpeed;

        PlayMovementAnimation(steered);
    }

    /// <summary>Halt all movement without touching the animator.</summary>
    public void StopMovement()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    /// <summary>Play the idle animation (direction-aware).</summary>
    public void PlayIdle() => PlayIdleAnimation();

    /// <summary>Update weapon aiming toward (or away from) the target; no-op for simple enemies.</summary>
    public void FaceAndAim(bool aimAway)
    {
        if (config != null && !config.isSimpleEnemy)
            UpdateWeaponAiming(aimAway);
    }

    /// <summary>
    /// Returns the steered movement direction, delegating to the AIPathfinding component.
    /// </summary>
    private Vector2 CalculateBestMovementDirection(Vector2 preferredDirection)
    {
        if (_pathfinding == null || preferredDirection == Vector2.zero) return preferredDirection;
        return _pathfinding.GetSteeringDirectionFromPreferred(preferredDirection);
    }

    private void PlayMovementAnimation(Vector2 direction)
    {
        if (animator != null && config != null)
        {
            bool aimingUp;
            if (config.isSimpleEnemy)
            {
                // Simple enemies pick animation from actual movement direction
                aimingUp = direction.y > 0.3f;
            }
            else
            {
                // Use weapon aim direction to determine animation (North = up, South = normal)
                aimingUp = (currentAimDirection == WeaponSortingManager.Direction.NorthEast ||
                            currentAimDirection == WeaponSortingManager.Direction.NorthWest);
            }
            string animName = aimingUp ? config.moveUpAnimationName : config.moveAnimationName;
            PlayAnimationSafe(animName);
        }
    }

    protected override void HandleUpdate()
    {
        if (!isAlive) return;

        // MULTIPLAYER FIX: Only server runs AI logic
        // Clients receive synced position/rotation via NetworkTransform
        if (!IsServerStarted)
        {
            return;
        }

        // Check if enemy actions are globally disabled (e.g., during loading)
        if (!ActionsEnabled)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        if (effectManager != null && effectManager.HasAnyAbilityBlockingEffect())
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            PlayIdleAnimation();
            return;
        }

        // Update knockback timer
        if (isKnockedBack)
        {
            knockbackTimer -= Time.deltaTime;
            if (knockbackTimer <= 0f)
            {
                isKnockedBack = false;
            }
            return; // Don't move while knocked back
        }

        // Find nearest valid target
        targetTransform = FindNearestTarget();
        distanceToTarget = targetTransform != null ? Vector3.Distance(transform.position, targetTransform.position) : float.MaxValue;
        isChasing = HasTarget;

        // Drive the finite state machine (handles transitions + per-state behavior).
        stateMachine.Tick(this, Time.deltaTime);

        ApplyMovementEffectsFromStatus();

        if (spriteRenderer != null)
        {
            if (config != null && config.isSimpleEnemy)
            {
                // Simple enemies always flip based on movement velocity
                if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f)
                {
                    Vector3 localScale = transform.localScale;
                    if (rb.linearVelocity.x < 0)
                    {
                        localScale.x = -Mathf.Abs(localScale.x);
                    }
                    else
                    {
                        localScale.x = Mathf.Abs(localScale.x);
                    }
                    transform.localScale = localScale;
                }
            }
            else if (targetTransform != null)
            {
                // Face toward the target
                spriteRenderer.flipX = targetTransform.position.x < transform.position.x;
            }
            else if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f)
            {
                // No target — face movement direction
                spriteRenderer.flipX = rb.linearVelocity.x < 0;
            }
        }

        // Apply collision damage if enabled
        if (config != null && config.hasCollisionDamage)
        {
            ApplyCollisionDamage();
        }
    }

    private void ApplyMovementEffectsFromStatus()
    {
        if (rb == null || effectManager == null) return;

        if (effectManager.HasAnyMovementBlockingEffect())
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float speedMultiplier = effectManager.GetMovementSpeedMultiplier();
        if (speedMultiplier < 1f)
        {
            rb.linearVelocity *= speedMultiplier;
        }
    }

    /// <summary>
    /// Finds the nearest valid target (Player, Construct, Ally, or Companion) within detection range
    /// </summary>
    protected virtual Transform FindNearestTarget()
    {
        Transform nearest = null;
        float nearestDist = detectionRange;

        // Find all game objects with valid target tags
        string[] targetTags = config != null && config.targetTags != null && config.targetTags.Length > 0
            ? config.targetTags
            : new string[] { "Player" }; // Default to Player if no tags specified

        foreach (string tag in targetTags)
        {
            GameObject[] targets = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject target in targets)
            {
                if (target == null) continue;

                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = target.transform;
                }
            }
        }

        return nearest;
    }

    // Add this public method
    public virtual void ApplyKnockback(Vector2 force)
    {
        if (rb != null)
        {
            isKnockedBack = true;
            knockbackTimer = knockbackDuration;
            rb.linearVelocity = Vector2.zero; // Clear current velocity
            rb.AddForce(force, ForceMode2D.Impulse);
        }
    }

    private void PlayIdleAnimation()
    {
        if (animator != null && config != null)
        {
            // Use weapon aim direction to determine animation (North = up, South = normal)
            bool aimingUp = (currentAimDirection == WeaponSortingManager.Direction.NorthEast ||
                            currentAimDirection == WeaponSortingManager.Direction.NorthWest);
            string animName = (aimingUp && !string.IsNullOrEmpty(config.idleUpAnimationName))
                ? config.idleUpAnimationName
                : config.idleAnimationName;

            PlayAnimationSafe(animName);
        }
    }

    private void PlayAnimationSafe(string animName)
    {
        if (animator == null || string.IsNullOrEmpty(animName)) return;

        // Check if the animation state exists before playing
        if (animator.HasState(0, Animator.StringToHash(animName)))
        {
            animator.Play(animName);
        }
        else
        {
            Debug.LogWarning($"[Enemy] {gameObject.name} tried to play animation '{animName}' but it doesn't exist in the animator controller");
        }
    }

    protected override void HandleDeath()
    {
        // Calculate and grant experience to player.
        // XP reward is defined by PlayerExperienceConfig.xpRewardRatio (default: 10% of max health).
        // GetLocalPlayer() ensures each client gives XP to their own player only,
        // even when HandleDeath runs inside an ObserversRpc (fires on all clients).
        PlayerController player = PlayerController.GetLocalPlayer();
        TriggerDeathAbility();
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        Debug.Log($"[Gold] {gameObject.name} died and trying to drop {config.goldDropped} gold.");
        if (player != null && IsServerStarted)
        {
            Debug.Log($"[Gold] Adding {config.goldDropped} gold to {player.gameObject.name}'s Bag.");
            player.AddBagGold(config.goldDropped);
        }
        //Death Animation
        if (animator != null && !string.IsNullOrEmpty(config.deathAnimationName))
        {
            PlayAnimationSafe(config.deathAnimationName);
        }
        // Destroy enemy GameObject
        if (deathVFXPrefab != null)
        {
            ParticleSystem particles = deathVFXPrefab.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                var shape = particles.shape;
                if (shape.enabled)
                {
                    shape.shapeType = ParticleSystemShapeType.Sprite;
                    SpriteRenderer spriteRenderer = this.GetComponentInChildren<SpriteRenderer>(true); // Get the sprite renderer from the enemy or its children
                    if (spriteRenderer != null && spriteRenderer.sprite != null)
                    {
                        shape.sprite = spriteRenderer.sprite;
                        shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;

                    }
                }
                GameObject effect = Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
                AutoDestroyEffect.SetupAutoDestroy(effect);
            }
        }
        StartCoroutine(DeathAnimation());
    }

    private IEnumerator DeathAnimation()
    {
        // set THIS enemy's movement speed to 0 (statContainer is per-instance;
        // config.stats is the shared EnemyConfig asset and would zero every enemy of this type)
        statContainer.SetStat("MoveSpeed", 0f);
        if (animator != null && !string.IsNullOrEmpty(config.deathAnimationName))
        {
            PlayAnimationSafe(config.deathAnimationName);
            // Wait for the animation to finish
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            float animationLength = stateInfo.length;
            yield return new WaitForSeconds(animationLength);
        }

        // This runs on every client (HandleDeath is called from an ObserversRpc), so calling
        // plain Destroy() here would tear down the GameObject on clients without ever going
        // through FishNet's despawn — leaving the server's NetworkObject bookkeeping stale.
        // Only the server (or an offline/single-player instance) may destroy it; other clients
        // rely on the server's ServerManager.Despawn() to remove their copy for them.
        var networkManager = InstanceFinder.NetworkManager;
        bool isNetworked = networkManager != null && NetworkObject != null;

        if (!isNetworked)
        {
            Destroy(gameObject, 0.1f);
        }
        else if (IsServerStarted)
        {
            yield return new WaitForSeconds(0.1f);
            if (this != null && NetworkObject != null && NetworkObject.IsSpawned)
                networkManager.ServerManager.Despawn(gameObject);
        }
    }

    private void TriggerDeathAbility()
    {
        if (config == null || config.onDeathAbility == null)
            return;

        if (IsNetworkActive && !IsServerInitialized)
            return;

        DataDrivenAbility deathAbility = GetComponent<DataDrivenAbility>();
        if (deathAbility == null)
            deathAbility = gameObject.AddComponent<DataDrivenAbility>();

        deathAbility.SetAbilityReference(new AbilityReference(config.onDeathAbility));
        deathAbility.InitializeAbility();
        deathAbility.TryUseAbilityAt(transform.position);
    }

    /// <summary>
    /// Check for collision damage in a radius around the enemy.
    /// Called from HandleUpdate when hasCollisionDamage is enabled.
    /// </summary>
    private void ApplyCollisionDamage()
    {
        if (config == null || !config.hasCollisionDamage) return;
        if (combatCollider == null) return;

        collisionDamageTimer -= Time.deltaTime;
        if (collisionDamageTimer > 0f) return;

        // Use the actual configured collider shape so every angle is covered correctly.
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(config.collisionHitLayers);
        filter.useTriggers = true;

        Collider2D[] hits = new Collider2D[16];
        int count = Physics2D.OverlapCollider(combatCollider, filter, hits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            if (hit.gameObject.layer == LayerMask.NameToLayer("Aura")) continue;

            Organism target = hit.GetComponentInParent<Organism>();
            if (target != null && target.IsAlive)
            {
                // Pass this enemy as attacker for thorns/reflect damage
                target.TakeDamage(config.collisionDamage * runtimeDamageMultiplier, config.collisionDamageType, transform.position, Color.white, gameObject);
                collisionDamageTimer = config.collisionDamageCooldown;
                return; // One damage tick per cooldown
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw ability ranges from ability instances
        if (abilityInstances != null)
        {
            foreach (var instance in abilityInstances)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, instance.range);
            }
        }
    }

    // Projectile firing is now handled by the shared DataDrivenAbility system

    /// <summary>
    /// Apply level-based stat scaling to this enemy
    /// </summary>
    public void ApplyLevelScaling(float multiplier)
    {
        if (levelScalingApplied)
        {
            Debug.LogWarning($"[Enemy] Level scaling already applied to {gameObject.name}");
            return;
        }

        if (config == null)
        {
            Debug.LogError($"[Enemy] Cannot apply level scaling - no config assigned to {gameObject.name}");
            return;
        }

        // Scale stats based on config stat values
        float baseMaxHealth = statContainer.GetStat("MaxHealth");
        float scaledMaxHealth = baseMaxHealth * multiplier;
        statContainer.SetStat("MaxHealth", scaledMaxHealth);
        ModifyHealth(scaledMaxHealth - CurrentHealth); // Set to full scaled health

        levelScalingApplied = true;
    }

    /// <summary>
    /// Apply map-level scaling with explicit damage multiplier and stat modifiers.
    /// </summary>
    public void ApplyMapLevelScaling(float healthMultiplier, float damageMultiplier, MapEnemyLevelScalingData levelScaling)
    {
        if (levelScalingApplied)
        {
            Debug.LogWarning($"[Enemy] Map level scaling already applied to {gameObject.name}");
            return;
        }

        if (config == null || statContainer == null)
        {
            Debug.LogError($"[Enemy] Cannot apply map level scaling to {gameObject.name} - missing config or stats");
            return;
        }

        float baseMaxHealth = statContainer.GetStat("MaxHealth");
        float scaledMaxHealth = baseMaxHealth * Mathf.Max(0f, healthMultiplier);
        statContainer.SetStat("MaxHealth", scaledMaxHealth);

        if (levelScaling != null && levelScaling.enemyStatModifiers != null)
        {
            for (int i = 0; i < levelScaling.enemyStatModifiers.Count; i++)
            {
                StatModifier mod = levelScaling.enemyStatModifiers[i];
                if (mod == null || string.IsNullOrEmpty(mod.statID) || !statContainer.HasStat(mod.statID))
                    continue;

                float current = statContainer.GetStat(mod.statID);
                float next = current;

                switch (mod.modifierType)
                {
                    case ModifierType.Flat:
                        next = current + mod.value;
                        break;
                    case ModifierType.Percentage:
                        next = current * (1f + (mod.value * 0.01f));
                        break;
                    case ModifierType.Override:
                        next = mod.value;
                        break;
                }

                statContainer.SetStat(mod.statID, next);
            }
        }

        // Re-sync health to the (possibly modified) max health.
        float finalMaxHealth = statContainer.GetStat("MaxHealth");
        ModifyHealth(finalMaxHealth - CurrentHealth);

        runtimeDamageMultiplier = Mathf.Max(0f, damageMultiplier);
        levelScalingApplied = true;

        Debug.Log($"[Enemy] {gameObject.name} map scaling applied — health x{healthMultiplier:F2}, damage x{runtimeDamageMultiplier:F2}");
    }

    /// <summary>
    /// Apply spawner-driven stat scaling (health and damage multipliers based on elapsed time).
    /// Called by MobSpawner immediately after instantiation.
    /// </summary>
    public void ApplySpawnerScaling(float healthMultiplier, float damageMultiplier)
    {
        if (healthMultiplier != 1f && statContainer != null)
        {
            float baseMaxHealth = statContainer.GetStat("MaxHealth");
            float scaledHealth = baseMaxHealth * healthMultiplier;
            statContainer.SetStat("MaxHealth", scaledHealth);
            ModifyHealth(scaledHealth - CurrentHealth); // Fill to new max
        }

        runtimeDamageMultiplier = damageMultiplier;

        Debug.Log($"[Enemy] {gameObject.name} spawner scaling applied — health x{healthMultiplier:F2}, damage x{damageMultiplier:F2}");
    }

    public void ApplyBossHealthScale(float healthMultiplier)
    {
        if (healthMultiplier != 1f && statContainer != null)
        {
            float baseMaxHealth = statContainer.GetStat("MaxHealth");
            float scaledHealth = baseMaxHealth * healthMultiplier;
            statContainer.SetStat("MaxHealth", scaledHealth);
            ModifyHealth(scaledHealth - CurrentHealth);
        }

        Debug.Log($"[Enemy] {gameObject.name} boss health scaling applied — health x{healthMultiplier:F2}");
    }

    /// <summary>
    /// Get the enemy config for external systems
    /// </summary>
    public EnemyConfig GetConfig() => config;

    /// <summary>
    /// Get the stat container for runtime stat modifications
    /// </summary>
    public StatContainer GetStats() => statContainer;


}

/// <summary>
/// Helper class to track ability instances with their configurations
/// </summary>
[System.Serializable]
public class EnemyAbilityInstance
{
    public DataDrivenAbility ability;
    public AbilityDataConfig config;
    public float range;
    public int priority;
}