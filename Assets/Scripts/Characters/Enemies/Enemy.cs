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
    protected Transform transform;
    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected Collider2D combatCollider;
    protected bool isChasing = false;
    protected bool isKnockedBack = false;
    protected float knockbackTimer = 0f;

    // Movement timer state
    private float movementTimer = 0f;
    private bool isMoving = false;

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

    // State Machine System
    private EnemyState currentState = EnemyState.Patrol;
    private float stateTimer = 0f;
    private float stateReassessmentTimer = 0f;
    private Vector3 spawnPosition;
    private Vector3 patrolTarget;
    private bool hasPatrolTarget = false;
    private float strafeAngle = 0f;
    private const float KITE_DISTANCE = 2.5f;
    private const float RETREAT_HEALTH_PERCENT = 15f;
    private const float STATE_REASSESSMENT_INTERVAL = 2f;

    // Collision damage tracking
    private float collisionDamageTimer = 0f;

    // Runtime scaling set by MobSpawner
    private float runtimeDamageMultiplier = 1f;

    protected override void Awake()
    {
        base.Awake();
        transform = GetComponent<Transform>();
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

        // Initialize movement timer (start moving immediately for timed movement)
        if (!config.continuousMovement)
        {
            isMoving = true;
            movementTimer = 0f;
        }

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
    private bool HasAction(EnemyActionType actionType)
    {
        if (config == null || config.actions == null) return false;
        return config.actions.Exists(a => a.actionType == actionType);
    }

    /// <summary>
    /// Get action config for a specific action type
    /// </summary>
    private EnemyActionConfig GetAction(EnemyActionType actionType)
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
    /// Determine next state based on current conditions
    /// </summary>
    private EnemyState DetermineNextState(float distanceToTarget, bool hasTarget, EnemyState currentState)
    {
        if (config == null || config.actions == null || config.actions.Count == 0)
            return EnemyState.Patrol;

        float healthPercent = (CurrentHealth / MaxHealth) * 100f;
        EnemyActionConfig retreatAction = GetAction(EnemyActionType.Retreat);

        // Priority 1: Retreat when health drops below the action's threshold
        if (retreatAction != null)
        {
            float retreatThreshold = retreatAction.healthPercentThreshold >= 0f
                ? retreatAction.healthPercentThreshold
                : RETREAT_HEALTH_PERCENT;

            if (healthPercent <= retreatThreshold)
                return EnemyState.Retreat;
        }

        // Priority 2: Kite if has Retreat action and player within kite distance
        if (retreatAction != null && hasTarget && distanceToTarget <= KITE_DISTANCE)
        {
            return EnemyState.Kite;
        }

        // No target in range: Patrol if has Patrol action
        if (!hasTarget)
        {
            if (HasAction(EnemyActionType.Patrol))
                return EnemyState.Patrol;
            return EnemyState.Patrol; // Default fallback
        }

        // Target within reach of at least one ability: Attack or Strafe
        if (distanceToTarget <= GetAttackRange())
        {
            // If an ability is ready, always attack
            if (HasAbilityReady(distanceToTarget))
            {
                return EnemyState.Attack;
            }

            // Everything on cooldown - stay in Strafe if already strafing
            if (HasAction(EnemyActionType.Strafe))
            {
                if (currentState == EnemyState.Strafe)
                {
                    // Stay in Strafe until an ability comes off cooldown
                    return EnemyState.Strafe;
                }
                else if (currentState == EnemyState.Attack)
                {
                    // Just finished attacking, randomly choose to strafe or wait
                    if (UnityEngine.Random.value > 0.5f)
                    {
                        return EnemyState.Strafe;
                    }
                }
            }

            // Default: stay in Attack state (waiting for cooldown)
            return EnemyState.Attack;
        }

        // Target outside every ability's range: Chase if has Chase action
        if (HasAction(EnemyActionType.Chase))
        {
            return EnemyState.Chase;
        }

        // Default: stand still and attack if in detection range
        return EnemyState.Attack;
    }

    /// <summary>
    /// Execute behavior for the current state
    /// </summary>
    private void ExecuteState(EnemyState state, float distanceToTarget)
    {
        // Simple enemies skip weapon aiming entirely
        if (config != null && !config.isSimpleEnemy)
        {
            // Update weapon aiming based on state
            bool aimAway = (state == EnemyState.Retreat || state == EnemyState.Kite);
            UpdateWeaponAiming(aimAway);
        }

        switch (state)
        {
            case EnemyState.Chase:
                ExecuteChase(distanceToTarget);
                break;

            case EnemyState.Retreat:
                ExecuteRetreat();
                break;

            case EnemyState.Strafe:
                ExecuteStrafe(distanceToTarget);
                break;

            case EnemyState.Patrol:
                ExecutePatrol();
                break;

            case EnemyState.Attack:
                ExecuteAttack(distanceToTarget);
                break;

            case EnemyState.Kite:
                ExecuteKite(distanceToTarget);
                break;
        }
    }

    private void ExecuteChase(float distanceToTarget)
    {
        if (targetTransform == null || rb == null || config == null) return;

        EnemyActionConfig chaseAction = GetAction(EnemyActionType.Chase);
        float speedMultiplier = chaseAction != null ? chaseAction.movementSpeedMultiplier : 1f;

        // Chase toward target using weighted ray pathfinding
        float moveSpeed = statContainer.GetStat("MoveSpeed") * speedMultiplier;
        Vector2 directionToTarget = (targetTransform.position - transform.position).normalized;
        Vector2 bestDirection = CalculateBestMovementDirection(directionToTarget);
        rb.linearVelocity = bestDirection * moveSpeed;

        PlayMovementAnimation(bestDirection);
    }

    private void ExecuteRetreat()
    {
        if (targetTransform == null || rb == null) return;

        EnemyActionConfig retreatAction = GetAction(EnemyActionType.Retreat);
        float speedMultiplier = retreatAction != null ? retreatAction.movementSpeedMultiplier : 1.5f;

        float moveSpeed = statContainer.GetStat("MoveSpeed") * speedMultiplier;
        Vector2 directionAwayFromTarget = (transform.position - targetTransform.position).normalized;
        Vector2 bestDirection = CalculateBestMovementDirection(directionAwayFromTarget);
        rb.linearVelocity = bestDirection * moveSpeed;

        PlayMovementAnimation(bestDirection);
    }

    private void ExecuteStrafe(float distanceToTarget)
    {
        if (targetTransform == null || rb == null) return;

        EnemyActionConfig strafeAction = GetAction(EnemyActionType.Strafe);
        if (strafeAction == null) return;

        Vector2 toTarget = (targetTransform.position - transform.position).normalized;

        // Calculate tangent direction for circular orbit
        float angleIncrement = 90f * Time.deltaTime; // 90 degrees per second
        if (!strafeAction.strafeClockwise)
            angleIncrement = -angleIncrement;

        strafeAngle += angleIncrement;

        // Calculate perpendicular direction for circular movement
        Vector2 perpendicular = new Vector2(-toTarget.y, toTarget.x);
        if (!strafeAction.strafeClockwise)
            perpendicular = -perpendicular;

        // Combine tangential movement with radial correction to maintain distance
        float distanceError = distanceToTarget - strafeAction.strafeDistance;
        Vector2 radialCorrection = -toTarget * distanceError * 0.5f; // Move in/out to correct distance

        Vector2 strafeDirection = (perpendicular + radialCorrection).normalized;

        float moveSpeed = statContainer.GetStat("MoveSpeed") * strafeAction.movementSpeedMultiplier;
        rb.linearVelocity = strafeDirection * moveSpeed;

        PlayMovementAnimation(strafeDirection);
    }

    private void ExecutePatrol()
    {
        if (rb == null) return;

        EnemyActionConfig patrolAction = GetAction(EnemyActionType.Patrol);
        if (patrolAction == null) return;

        // Check if we need a new patrol target
        if (!hasPatrolTarget || Vector3.Distance(transform.position, patrolTarget) < 0.5f)
        {
            // Wait at current position if we just reached a patrol point
            if (hasPatrolTarget)
            {
                rb.linearVelocity = Vector2.zero;

                // Use state timer for wait time
                if (stateTimer < patrolAction.patrolWaitTime)
                {
                    PlayIdleAnimation();
                    return;
                }
            }

            // Generate new random patrol target around spawn position
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * patrolAction.patrolRadius;
            patrolTarget = spawnPosition + new Vector3(randomOffset.x, randomOffset.y, 0f);
            hasPatrolTarget = true;
            stateTimer = 0f; // Reset timer for next wait
        }

        // Move toward patrol target using weighted ray pathfinding
        Vector2 directionToPatrol = (patrolTarget - transform.position).normalized;
        Vector2 bestDirection = CalculateBestMovementDirection(directionToPatrol);
        float moveSpeed = statContainer.GetStat("MoveSpeed") * patrolAction.movementSpeedMultiplier;
        rb.linearVelocity = bestDirection * moveSpeed;

        PlayMovementAnimation(bestDirection);
    }

    private void ExecuteAttack(float distanceToTarget)
    {
        // Stop moving when attacking
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        PlayIdleAnimation();
        TryUseAbilities(distanceToTarget);
    }

    private void ExecuteKite(float distanceToTarget)
    {
        // Kite behavior: move away from the target while still casting whatever is off cooldown
        EnemyActionConfig retreatAction = GetAction(EnemyActionType.Retreat);
        float speedMultiplier = retreatAction != null ? retreatAction.movementSpeedMultiplier : 1.2f;

        // Move away using weighted ray pathfinding
        float moveSpeed = statContainer.GetStat("MoveSpeed") * speedMultiplier;
        Vector2 directionAwayFromTarget = (transform.position - targetTransform.position).normalized;
        Vector2 bestDirection = CalculateBestMovementDirection(directionAwayFromTarget);
        rb.linearVelocity = bestDirection * moveSpeed;

        PlayMovementAnimation(bestDirection);

        TryUseAbilities(distanceToTarget);
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

        stateTimer += Time.deltaTime;

        // Find nearest valid target
        targetTransform = FindNearestTarget();
        float distanceToTarget = targetTransform != null ? Vector3.Distance(transform.position, targetTransform.position) : float.MaxValue;
        bool hasTarget = targetTransform != null && distanceToTarget <= detectionRange;

        // State Machine System
        if (config != null && config.actions != null && config.actions.Count > 0)
        {
            // Decrement reassessment timer
            stateReassessmentTimer -= Time.deltaTime;

            // Only reassess state when timer expires
            if (stateReassessmentTimer <= 0f)
            {
                EnemyState nextState = DetermineNextState(distanceToTarget, hasTarget, currentState);

                // Transition to new state if changed
                if (nextState != currentState)
                {
                    Debug.Log($"[Enemy] {gameObject.name} state transition: {currentState} -> {nextState} (distance: {distanceToTarget:F2}, attackRange: {GetAttackRange():F2}, health: {(CurrentHealth / MaxHealth * 100f):F1}%)");
                    currentState = nextState;
                    stateTimer = 0f;
                }

                // Reset reassessment timer
                stateReassessmentTimer = STATE_REASSESSMENT_INTERVAL;
            }

            // Execute current state
            isChasing = hasTarget;
            ExecuteState(currentState, distanceToTarget);
        }
        else
        {
            // LEGACY BEHAVIOR: No actions configured
            if (!hasTarget)
            {
                isChasing = false;
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }
                PlayIdleAnimation();
                return;
            }

            isChasing = true;

            // Check if we're in range of any ability OR weapon
            bool inAbilityRange = false;

            // Check configured abilities
            foreach (var abilityInstance in abilityInstances)
            {
                if (distanceToTarget <= abilityInstance.range)
                {
                    inAbilityRange = true;
                    break;
                }
            }

            // Check weapon range if using weapon-granted abilities
            if (!inAbilityRange && config.useWeaponGrantedAbilities)
            {
                if (distanceToTarget <= config.weaponAbilityRange)
                {
                    inAbilityRange = true;
                }
            }

            // If in ability range, stop moving and try to attack
            if (inAbilityRange)
            {
                // Stop all movement when in ability range
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                TryUseAbilities(distanceToTarget);
                PlayIdleAnimation();
            }
            // Out of ability range, chase the target
            else if (canMove)
            {
                ChaseTarget();
            }
        }

        // Flip sprite based on movement or target
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

    protected virtual void ChaseTarget()
    {
        if (rb == null || targetTransform == null || isKnockedBack || config == null) return;

        // Handle continuous vs timed movement
        if (config.continuousMovement)
        {
            // Continuous movement - always moving toward target
            MoveTowardTarget();
        }
        else
        {
            // Timed movement - alternate between moving and stopping
            if (isMoving)
            {
                movementTimer += Time.deltaTime;
                MoveTowardTarget();

                if (movementTimer >= config.movementTime)
                {
                    // Switch to stop phase
                    isMoving = false;
                    movementTimer = 0f;
                    rb.linearVelocity = Vector2.zero;
                    PlayIdleAnimation();
                }
            }
            else
            {
                // Currently stopped
                movementTimer += Time.deltaTime;

                if (movementTimer >= config.stopTime)
                {
                    // Switch to move phase
                    isMoving = true;
                    movementTimer = 0f;
                }
            }
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

    private void MoveTowardTarget()
    {
        if (rb == null) return;

        // Get movement speed from stats (allows runtime modification)
        float currentMoveSpeed = statContainer.GetStat("MoveSpeed");
        if (currentMoveSpeed <= 0)
        {
            currentMoveSpeed = 3f; // Default fallback if stat not set
        }

        Vector2 directionToTarget = (targetTransform.position - transform.position).normalized;
        Vector2 bestDirection = CalculateBestMovementDirection(directionToTarget);
        rb.linearVelocity = bestDirection * currentMoveSpeed;

        // Play appropriate animation based on direction
        if (animator != null)
        {
            string animName = bestDirection.y > 0.1f ? config.moveUpAnimationName : config.moveAnimationName;
            PlayAnimationSafe(animName);
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
        if (player != null)
        {
            Debug.Log($"[Gold] {gameObject.name} is granting gold to player {player.gameObject.name}.");
            SaveFileData saveFileData = player.GetCurrentSaveFileData();
            if (saveFileData != null)
            {
                Debug.Log($"[Gold] Adding {config.goldDropped} gold to player {player.gameObject.name}'s save file.");
                saveFileData.AddGold(config.goldDropped);
            } else
            {
                Debug.LogWarning($"[Gold] Failed to add gold to player {player.gameObject.name}'s save file. SaveFileData is null.");
            }
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