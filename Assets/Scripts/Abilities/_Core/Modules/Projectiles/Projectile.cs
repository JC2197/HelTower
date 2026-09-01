using UnityEngine;
using UnityEngine.Rendering.Universal;
using FishNet;
using FishNet.Object;
using System.Collections.Generic;
using JoeConticello.VisualEffects;

public enum ProjectileType
{
    Physical,
    Magic,
    Elemental
}
public enum ProjectileBehavior
{
    Straight,      // Flies straight in direction
    Lobbed,        // Arc trajectory affected by gravity
    Homing,        // Tracks target
    Boomerang,     // Returns to sender
    Spiral,        // Spirals around center axis
    Wave,          // Sine wave pattern
    Dropped        // Falls straight down
}
public abstract class Projectile : NetworkBehaviour
{
    // Configuration fields - populated at runtime from ProjectileConfig via InitializeFromConfig()
    protected float speed;
    protected float scale = 1f;
    protected bool useLifetime;
    protected float lifetime;
    protected float maxRange;
    protected ProjectileBehavior behavior;
    protected bool usesWeaponDamage;
    protected float percentWeaponDamage;
    protected float damage;
    protected string damageTypeName;
    protected bool dealsDamageOverTime;
    protected float damagePerTick;
    protected float dotInterval;
    protected float dotDuration;
    protected ParticleSystem dotParticleEffectPrefab;
    protected bool startParticlesFromFeet = false;
    protected float homingStrength;
    protected Transform homingTarget;
    protected bool homingTargetAcquired = false; // Track if we've attempted to find a homing target
    protected Vector3 homingSearchCenter; // Cursor position for active, owner position for autocast
    protected bool isAutocast = false; // Whether this projectile was fired via autocast
    protected float waveAmplitude;
    protected float waveFrequency;
    protected float spiralRadius;
    protected float spiralSpeed;
    protected AnimationCurve boomerangDistanceCurve;
    protected float lobbedArcHeight; // auto-copied from ProjectileConfig via InitializeFromConfig reflection

    // Lobbed runtime state — set by SetLobbedTarget(), not from config
    private Vector3 _lobbedStartPos;
    private Vector3 _lobbedTargetPos;
    private float _lobbedFlightTime;
    private bool _lobbedTargetSet = false;
    private bool _lobbedLanded = false;
    protected bool _lobbedIsDescending = false;
    protected float _lobbedCurrentVerticalVelocity = 0f;
    protected float _lobbedCalculatedGravity = 0f;
    protected bool freezeRotation;
    protected float spinSpeed;
    private float _accumulatedSpinAngle = 0f;
    protected bool hasPierce;
    protected int pierceCount;
    protected int currentPierceCount = 0;
    protected GameObject hitVisualPrefab;
    protected AudioClip hitSound;
    protected GameObject destroyVisualPrefab;
    protected AudioClip destroySound;
    protected bool hasKnockback;
    protected float knockbackForce;
    protected bool hasPull;
    protected float pullForce;
    protected bool hasChaining;
    protected float chainRange;
    protected int maxChains;
    protected int currentChainCount = 0;
    protected bool justChained = false; // Track if projectile just chained in latest hit
    protected System.Collections.Generic.HashSet<Enemy> chainedEnemies = new System.Collections.Generic.HashSet<Enemy>(); // Track already-hit enemies to prioritize unique targets
    protected Color hitFlashColor = Color.white;

    protected LayerMask hitLayers; // Populated from config, not serialized
    protected LayerMask canPierceLayers;
    protected LayerMask destroyOnLayers;
    protected EffectData onHitEffects; // NEW: Effect data from ProjectileConfig
    protected LifeStealConfig lifeSteal; // Life steal config — sourced from hitbox in InitializeFromConfig
    protected HitboxConfig hitbox; // Shared hitbox config — reused for damage/effects/knockback on the authority
    protected GameObject owner; // NEW: Owner of projectile for damage bonus calculation
    protected string abilityName; // NEW: Name of ability that spawned this projectile
    protected System.Collections.Generic.List<string> abilityTags; // NEW: Tags of ability that spawned this projectile
    protected float damageMultiplier = 1f; // Charge damage multiplier (not crit — crit is per-hit in DamageCalculator)
    protected AbilityDataConfig parentConfig; // Parent ability config for centralized hit visuals

    // Muzzle flash config - populated from ProjectilePrefab overrides in Awake,
    // or from InitializeFromConfig on server. Clients use prefab overrides for ObserversRpc.
    protected GameObject muzzleFlashPrefab;
    protected bool enableMuzzleLight = false;
    protected Color muzzleLightColor = Color.yellow;
    protected float muzzleLightIntensity = 3f;
    protected float muzzleLightRange = 2f;
    protected float muzzleLightDuration = 0.1f;

    // Protected variables for inheritance
    protected Vector3 direction;
    protected Vector3 velocity;
    protected float currentLifetime;
    protected SpriteRenderer spriteRenderer;
    protected Rigidbody2D rb;
    protected ProjectileType projectileType;
    protected bool isDestroying = false;
    protected Animator animator;
    protected Collider2D projectileCollider;
    // Property to get current damage type data
    private ParticleSystem trailChildInstance;
    // For wave/spiral behaviors
    protected float behaviorTime = 0f;
    protected Vector3 originalDirection;
    protected Vector3 startPosition;


    public DamageTypeData DamageTypeData
    {
        get
        {
            DamageTypeRegistry.Initialize();
            return DamageTypeRegistry.GetDamageType(damageTypeName);
        }
    }

    /// <summary>
    /// True when this instance is the authority for game logic (collision, damage, lifetime).
    /// - Single-player (not network-spawned): always true
    /// - Multiplayer server: true (authoritative)
    /// - Multiplayer client: false (collision/damage handled server-side)
    /// </summary>
    protected bool IsAuthority => !IsSpawned || IsServerStarted;

    /// <summary>
    /// Set to true on non-server clients after RpcClientInitialize is received.
    /// Enables local movement simulation for smooth visuals without waiting for
    /// NetworkTransform tick updates.
    /// </summary>
    private bool _clientSimulating = false;

    /// <summary>
    /// When true this is a cosmetic-only "predictive" clone spawned locally on the owner
    /// for zero-latency visual feedback while the authoritative ServerRpc round-trip completes.
    /// Predictive projectiles move normally but never deal damage or trigger hits.
    /// They auto-destroy after PredictiveMaxLifetime seconds.
    /// </summary>
    private bool _isPredictive = false;
    private float _predictiveAge = 0f;
    private const float PredictiveMaxLifetime = 0.5f;

    protected virtual void Awake()
    {
        // Projectiles are fast and short-lived — NetworkTickSmoother is not useful here
        // and requires a "graphical child" target transform that is never set on projectile
        // prefabs, causing FishNet to log an error and fail to initialize the smoother.
        // Destroy it immediately if present so the prefab works correctly over the network.
        var tickSmoother = GetComponent<FishNet.Component.Transforming.Beta.NetworkTickSmoother>();
        if (tickSmoother != null) Destroy(tickSmoother);

        // Initialize damage type registry
        DamageTypeRegistry.Initialize();

        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        projectileCollider = GetComponent<Collider2D>();

        rb = GetComponent<Rigidbody2D>();

        // Find the trail child if present
        trailChildInstance = GetComponentInChildren<ParticleSystem>();

        // Pre-populate VFX fields from ProjectilePrefab serialized overrides.
        // These Inspector-assigned assets are available on ALL instances (server + clients)
        // because they are part of the prefab. The server will overwrite them later via
        // InitializeFromConfig, but clients keep these so ObserversRpc VFX works.
        if (this is ProjectilePrefab ppf)
        {
            if (ppf.overrideDestroyEffects)
            {
                if (ppf.destroyVisualPrefabOverride != null) destroyVisualPrefab = ppf.destroyVisualPrefabOverride;
                if (ppf.destroySoundOverride != null) destroySound = ppf.destroySoundOverride;
            }
            if (ppf.overrideHitEffects)
            {
                if (ppf.hitVisualPrefabOverride != null) hitVisualPrefab = ppf.hitVisualPrefabOverride;
                if (ppf.hitSoundOverride != null) hitSound = ppf.hitSoundOverride;
                hitFlashColor = ppf.hitFlashColorOverride;
            }
            if (ppf.overrideMuzzleFlash)
            {
                if (ppf.muzzleFlashPrefabOverride != null) muzzleFlashPrefab = ppf.muzzleFlashPrefabOverride;
                if (ppf.overrideMuzzleLight)
                {
                    enableMuzzleLight = true;
                    muzzleLightColor = ppf.muzzleLightColorOverride;
                    muzzleLightIntensity = ppf.muzzleLightIntensityOverride;
                    muzzleLightRange = ppf.muzzleLightRangeOverride;
                    muzzleLightDuration = ppf.muzzleLightDurationOverride;
                }
            }
        }
    }

    protected virtual void Start()
    {
        InitializeProjectile();
        OnProjectileInitialized();

        // Only set these if they haven't been set by Initialize() already
        if (useLifetime && currentLifetime <= 0)
        {
            currentLifetime = lifetime;
        }
        if (maxRange > 0 && startPosition == Vector3.zero)
        {
            startPosition = transform.position;
        }

        // Safety-net: if no lifetime or range limit is configured, destroy after 2s
        // so projectiles (and their attached trail particles) never leak indefinitely.
        // Uses DestroyProjectile() (via coroutine) so networked projectiles are properly
        // Despawn()ed on all clients instead of only being destroyed locally.
        if (!useLifetime && maxRange <= 0f && !_isPredictive)
        {
            StartCoroutine(FallbackDestroyCoroutine(2f));
        }
    }

    private System.Collections.IEnumerator FallbackDestroyCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isDestroying)
            DestroyProjectile();
    }

    protected virtual void Update()
    {
        if (isDestroying) return;

        // Predictive (cosmetic-only) projectile spawned locally on the owner for
        // zero-latency feedback.  Runs movement but never collides or deals damage.
        if (_isPredictive)
        {
            _predictiveAge += Time.deltaTime;
            if (_predictiveAge >= PredictiveMaxLifetime)
            {
                Destroy(gameObject);
                return;
            }
            MoveProjectile();
            behaviorTime += Time.deltaTime;
            return;
        }

        if (IsAuthority)
        {
            // Server / single-player: full authoritative logic.
            MoveProjectile();

            UpdateProjectileSpecific();
            if (useLifetime)
                UpdateLifetime();
            else
                UpdateRange();
            ApplyPullEffect();
            behaviorTime += Time.deltaTime;
        }
        else if (_clientSimulating)
        {
            // Non-server client: simulate movement locally so the bullet looks smooth
            // between NetworkTransform ticks. Collision, damage and lifetime are still
            // handled exclusively on the server — this is visuals-only.
            MoveProjectile();
            behaviorTime += Time.deltaTime;
        }
    }

    protected virtual void ApplyPullEffect()
    {
        if (!hasPull || projectileCollider == null) return;

        // Use the projectile's own collider to find overlapping enemies
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(hitLayers);
        filter.useTriggers = true;
        List<Collider2D> results = new List<Collider2D>();
        projectileCollider.Overlap(filter, results);

        foreach (Collider2D enemyCollider in results)
        {
            Enemy enemy = enemyCollider.GetComponentInParent<Enemy>();
            if (enemy == null) continue;

            // Pull toward the projectile's center (local 0,0)
            Vector2 pullDirection = (transform.position - enemy.transform.position).normalized;
            enemy.ApplyKnockback(pullDirection * pullForce * Time.deltaTime);
        }
    }

    protected virtual void InitializeProjectile()
    {
        // Override in derived classes
    }

    protected virtual void OnProjectileInitialized()
    {
        // Override in derived classes for setup after initialization
    }

    protected virtual void UpdateProjectileSpecific()
    {
        // Override in derived classes for specific behavior
    }

    protected virtual void MoveProjectile()
    {
        if (direction == Vector3.zero) return;

        switch (behavior)
        {
            case ProjectileBehavior.Straight:
                MoveStraight();
                break;
            case ProjectileBehavior.Lobbed:
                MoveLobbed();
                break;
            case ProjectileBehavior.Homing:
                MoveHoming();
                break;
            case ProjectileBehavior.Wave:
                MoveWave();
                break;
            case ProjectileBehavior.Spiral:
                MoveSpiral();
                break;
            case ProjectileBehavior.Boomerang:
                MoveBoomerang();
                break;
            case ProjectileBehavior.Dropped:
                MoveDropped();
                break;
        }

        // Rotation override: applied after movement so individual behaviors don't need to know about it.
        if (freezeRotation && spinSpeed == 0f)
        {
            transform.rotation = Quaternion.identity;
        }

        // Spin on own axis. Homing/Lobbed/Boomerang reset transform.rotation to direction every
        // frame, so a simple Rotate() call would never accumulate. We track the spin angle
        // separately for those behaviors and add it as an offset on top of the direction angle.
        if (spinSpeed != 0f)
        {
            if (behavior == ProjectileBehavior.Homing ||
                behavior == ProjectileBehavior.Lobbed ||
                behavior == ProjectileBehavior.Boomerang)
            {
                _accumulatedSpinAngle += spinSpeed * Time.deltaTime;
                float dirAngle = transform.rotation.eulerAngles.z;
                transform.rotation = Quaternion.Euler(0f, 0f, dirAngle + _accumulatedSpinAngle);
            }
            else
            {
                transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            }
        }
    }

    protected virtual void MoveStraight()
    {
        velocity = direction * speed;
        transform.position += velocity * Time.deltaTime;
    }

    protected virtual void MoveLobbed()
    {
        if (!_lobbedTargetSet)
        {
            MoveStraight();
            return;
        }

        if (_lobbedLanded) return;

        float u = _lobbedFlightTime > 0f ? Mathf.Clamp01(behaviorTime / _lobbedFlightTime) : 0.5f;

        // ── Parabolic arc ────────────────────────────────────────────────────────────
        
        Vector3 groundPos = Vector3.Lerp(_lobbedStartPos, _lobbedTargetPos, u);
        float arcOffset = 4f * lobbedArcHeight * u * (1f - u);
        transform.position = groundPos + Vector3.up * arcOffset;

        // ── Dynamic Sprite Scaling ──────────────────────────────────────────────
        float maxScaleMultiplier = 1.6f;
        float currentScaleBonus = 4f * (maxScaleMultiplier - 1f) * u * (1f - u);
        float targetProgressScale = scale * (1f + currentScaleBonus);
        transform.localScale = new Vector3(targetProgressScale, targetProgressScale, 1f);

        // ── Tangent rotation ─────────────────────────────────────────────────────────
        Vector3 horizontal = _lobbedTargetPos - _lobbedStartPos;
        float arcDeriv = 4f * lobbedArcHeight * (1f - 2f * u);
        Vector3 tangent = horizontal + Vector3.up * arcDeriv;

        if (tangent.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        // ── Landing ──────────────────────────────────────────────────────────────────
        if (u >= 1f)
        {
            _lobbedLanded = true;
            if (IsAuthority)
                TriggerLobbedLanding();
        }
    }

    /// <summary>
    /// Called once when a lobbed projectile reaches its target position.
    /// Snaps to exact target, sweeps for enemies at the landing zone,
    /// processes hits, then destroys the projectile.
    /// </summary>
    private void TriggerLobbedLanding()
    {
        transform.position = _lobbedTargetPos;

        // Re-enable collider so OnTriggerEnter2D can fire on subsequent physics steps,
        // and so OverlapCircleAll treats the collider geometry correctly.
        if (projectileCollider != null)
            projectileCollider.enabled = true;

        // Manual overlap sweep — OnTriggerEnter2D won't fire for colliders already
        // overlapping when the trigger is first enabled.
        float landingRadius = 0.5f;
        if (projectileCollider is CircleCollider2D cc)
            landingRadius = Mathf.Max(0.1f, cc.radius * transform.localScale.x);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, landingRadius, hitLayers);
        foreach (Collider2D hit in hits)
        {
            if (isDestroying) break;
            HandleCollision(hit);
        }

        // If no hit caused a destroy, trigger the destroy-VFX ourselves (e.g. grenade lands on empty ground).
        if (!isDestroying)
            DestroyProjectile(fromHit: false, angle: transform.rotation.eulerAngles.z);
    }

    protected virtual void MoveHoming()
    {
        // Re-acquire target if current one is dead or destroyed
        if (homingTarget != null)
        {
            Enemy targetEnemy = homingTarget.GetComponent<Enemy>();
            if (targetEnemy != null && !targetEnemy.IsAlive)
            {
                homingTarget = null;
            }
        }

        // Search from projectile's current position every frame until a target is found
        if (homingTarget == null)
        {
            homingTarget = FindHomingTarget();
        }

        if (homingTarget != null)
        {
            Vector3 targetDirection = (homingTarget.position - transform.position).normalized;
            // Use a fixed turn rate for smooth tracking
            direction = Vector3.Lerp(direction, targetDirection, 10f * Time.deltaTime).normalized;
        }
        // If no target found, fly straight in current direction
        velocity = direction * speed;
        transform.position += velocity * Time.deltaTime;

        // Rotate to face direction
        if (direction != Vector3.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    /// <summary>
    /// Find the nearest enemy within homingStrength radius of the projectile's current position.
    /// Projectiles always search from themselves so each can independently acquire a target
    /// after travelling in their initial launch direction.
    /// </summary>
    protected virtual Transform FindHomingTarget()
    {
        // Always search from the projectile itself
        Vector3 searchCenter = transform.position;
        float searchRadius = homingStrength > 0 ? homingStrength : 5f;

        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(searchCenter, searchRadius);

        Transform closestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D col in nearbyColliders)
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            if (!enemy.IsAlive) continue;

            float distance = Vector3.Distance(searchCenter, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = enemy.transform;
            }
        }

        return closestTarget;
    }

    protected virtual void MoveWave()
    {
        // TODO: Implement wave pattern
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0);
        float wave = Mathf.Sin(behaviorTime * waveFrequency * Mathf.PI * 2) * waveAmplitude;

        velocity = direction * speed;
        Vector3 waveOffset = perpendicular * wave * Time.deltaTime;
        transform.position += (velocity * Time.deltaTime) + waveOffset;
    }

    protected virtual void MoveSpiral()
    {
        // Spiral outward around the launch origin/caster. The projectile's radial distance
        // increases over time while its launch direction rotates around the start position,
        // producing a large outward spiral centered on the caster instead of a corkscrew
        // around the projectile's own travel path.
        Vector3 forward = originalDirection.sqrMagnitude > 0.0001f ? originalDirection.normalized : direction.normalized;
        float angle = behaviorTime * spiralSpeed * Mathf.PI * 2f;
        float radius = spiralRadius + (speed * behaviorTime);
        Vector3 spiralDirection = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg) * forward;
        Vector3 nextPosition = startPosition + (spiralDirection * radius);

        velocity = (nextPosition - transform.position) / Mathf.Max(Time.deltaTime, 0.0001f);
        transform.position = nextPosition;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            float facingAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(facingAngle, Vector3.forward);
        }
    }

    protected virtual void MoveBoomerang()
    {
        float t = lifetime > 0f ? Mathf.Clamp01(behaviorTime / lifetime) : 0f;

        // Curve is normalized 0-1. Outbound: Y scales along launch direction by maxRange.
        // Return: Y (1→0) lerps from the turnaround point toward the owner's live position.
        float normalizedDist = boomerangDistanceCurve != null
            ? Mathf.Clamp01(boomerangDistanceCurve.Evaluate(t))
            : t;

        Vector3 turnaroundPos = startPosition + direction * maxRange;
        Vector3 prevPos = transform.position;

        if (t <= 0.5f)
        {
            transform.position = startPosition + direction * (normalizedDist * maxRange);
        }
        else
        {
            // 1 - normalizedDist goes 0→1 as the curve descends back to 0.
            Vector3 returnTarget = owner != null ? owner.transform.position : startPosition;
            transform.position = Vector3.Lerp(turnaroundPos, returnTarget, 1f - normalizedDist);
        }

        // Face the direction of travel this frame. MoveProjectile overrides if freezeRotation is set.
        Vector3 delta = transform.position - prevPos;
        if (delta.sqrMagnitude > 0.00001f)
        {
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        if (t >= 1f && IsAuthority)
            DestroyProjectile();
    }

    protected virtual void MoveDropped()
    {
        // Simple drop behavior
        velocity = Vector3.down * speed;
        transform.position += velocity * Time.deltaTime;
    }

    protected virtual void UpdateLifetime()
    {
        if (!useLifetime) return;

        currentLifetime -= Time.deltaTime;
        if (currentLifetime <= 0f && IsAuthority)
        {
            DestroyProjectile();
        }
    }

    protected virtual void UpdateRange()
    {
        // Check if projectile has exceeded max range (0 = no range limit)
        if (maxRange > 0f && startPosition != Vector3.zero)
        {
            float distanceTraveled = Vector3.Distance(startPosition, transform.position);
            if (distanceTraveled >= maxRange && IsAuthority)
            {
                DestroyProjectile();
            }
        }
    }


    /// <summary>
    /// Initialize projectile from a ProjectileConfig
    /// </summary>
    public virtual void InitializeFromConfig(ProjectileConfig configData)
    {
        if (configData == null) return;

        // Reflect all public config fields whose name matches a field on Projectile.
        // This ensures no new config fields are ever silently missed.
        // Only exception: muzzleFlashPrefab is conditional (clients keep prefab-baked value).
        var projectileType = typeof(Projectile);
        foreach (var configField in typeof(ProjectileConfig).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (configField.Name == "muzzleFlashPrefab") continue;

            var projField = projectileType.GetField(configField.Name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            if (projField != null && projField.FieldType == configField.FieldType)
                projField.SetValue(this, configField.GetValue(configData));
        }

        // Muzzle flash: server uses resolved config value; clients keep prefab-baked value
        if (configData.muzzleFlashPrefab != null)
            muzzleFlashPrefab = configData.muzzleFlashPrefab;

        // Source the shared hitbox block into the projectile's runtime mirror fields.
        // The mirror fields are used throughout movement/collision/client-visual logic, while the
        // hitbox object itself is reused for the authoritative hit processing (see ProcessHit).
        hitbox = configData.hitbox;
        if (hitbox != null)
        {
            damage = hitbox.damage;
            damageTypeName = hitbox.damageTypeName;
            usesWeaponDamage = hitbox.useWeaponDamage;
            percentWeaponDamage = hitbox.percentWeaponDamage;
            hitLayers = hitbox.hitLayers;
            hasKnockback = hitbox.knockback != null && hitbox.knockback.enabled;
            knockbackForce = hitbox.knockback != null ? hitbox.knockback.force : 0f;
            hasPull = hitbox.pull != null && hitbox.pull.enabled;
            pullForce = hitbox.pull != null ? hitbox.pull.force : 0f;
            onHitEffects = hitbox.onHitEffects;
            lifeSteal = hitbox.lifeSteal;
            if (hitbox.effects != null)
            {
                hitVisualPrefab = hitbox.effects.hitEffectPrefab;
                hitSound = hitbox.effects.hitSound;
                hitFlashColor = hitbox.effects.hitFlashColor;
            }
        }

        Debug.Log($"[Projectile.InitializeFromConfig] usesWeaponDamage={usesWeaponDamage}, damage={damage}, hasChaining={hasChaining}, maxChains={maxChains}, chainRange={chainRange}");
        Debug.Log($"[Projectile.InitializeFromConfig] hasPierce={hasPierce}, pierceCount={pierceCount}");
        Debug.Log($"[Projectile.InitializeFromConfig] hitLayers={hitLayers.value}, canPierceLayers={canPierceLayers.value}, destroyOnLayers={destroyOnLayers.value}");

        // Always use kinematic Rigidbody for 2D top-down projectiles
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        // Apply scale — affects sprite and hitbox together.
        if (hitbox != null)
        {
            float sx = hitbox.scaleX > 0f ? hitbox.scaleX : 1f;
            float sy = hitbox.scaleY > 0f ? hitbox.scaleY : 1f;
            transform.localScale = new Vector3(sx, sy, 1f);
        }
    }

    /// <summary>
    /// Maximum elapsed time we allow for latency compensation.
    /// Prevents extremely laggy players from making projectiles teleport
    /// unreasonable distances on other players' screens.
    /// </summary>
    private const float MAX_PASSED_TIME = 0.3f;

    public virtual void Initialize(Vector3 startPosition, Vector3 dir, float projectileSpeed = -1f, float passedTime = 0f)
    {
        transform.position = startPosition;
        direction = dir.normalized;
        originalDirection = direction; // Store for behaviors that need it
        this.startPosition = startPosition; // Store start position

        if (projectileSpeed > 0)
            speed = projectileSpeed;

        // Rotate to face direction
        if (direction != Vector3.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        velocity = direction * speed;

        // NOTE: Fast-forward via passedTime is DISABLED. For the fast-moving, short-range
        // projectiles in this game the position leap puts the projectile past most of its
        // useful range on observer clients, making it appear to "start near the end."
        // The predictive clone already gives the owner zero-latency feedback and the
        // client-side simulation (_clientSimulating) keeps observers smooth.

        // Mark that we've been manually initialized (don't let Start override our direction)
        currentLifetime = lifetime;
        _accumulatedSpinAngle = 0f;
    }

    /// <summary>
    /// Marks this instance as a cosmetic-only predictive clone.
    /// Called on the locally-instantiated (non-networked) copy that the owner spawns
    /// immediately for zero-latency visual feedback while the ServerRpc round-trip completes.
    /// The clone moves normally but deals no damage and auto-destroys after PredictiveMaxLifetime.
    /// </summary>
    public void SetupAsPredictive()
    {
        _isPredictive = true;
        // Belt-and-suspenders: disable the collider so physics triggers cannot fire
        // even in edge cases where the flag check is bypassed.
        if (projectileCollider != null) projectileCollider.enabled = false;

        // Safety-net: guarantee destruction even if Update() stops running
        // (e.g., the object gets disabled). Adds a small margin above the
        // normal PredictiveMaxLifetime so Update() has a chance to fire first.
        Destroy(gameObject, PredictiveMaxLifetime + 0.5f);
    }

    /// <summary>
    /// Sent to all non-server clients immediately after the server calls Initialize().
    /// Gives clients the spawn parameters so they can simulate projectile movement locally,
    /// producing smooth visuals at full frame-rate instead of choppy NetworkTransform ticks.
    /// BufferLast=true ensures late-joining clients still receive it.
    /// The tick parameter is the client tick at which the projectile was originally fired,
    /// allowing each observer to compute its own passedTime for latency compensation.
    /// </summary>
    [ObserversRpc(BufferLast = true, ExcludeServer = true)]
    public void RpcClientInitialize(Vector3 pos, Vector3 dir, float spd, uint tick)
    {
        // Start client-side simulation from the original spawn position.
        // No fast-forward: for fast, short-range projectiles the position leap
        // put the projectile near the end of its range on observer screens.
        Initialize(pos, dir, spd);
        _clientSimulating = true;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (isDestroying) return;
        // Predictive clones are cosmetic-only — they never register hits
        if (_isPredictive) return;
        // Only the authority processes collisions and damage
        if (!IsAuthority) return;
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            HandleCollision(other);
        }
    }

    public void SetDamage(float newDamage) => damage = newDamage;

    public float GetDamage()
    {
        Debug.Log($"[Projectile.GetDamage] usesWeaponDamage={usesWeaponDamage}, owner={(owner != null ? owner.name : "NULL")}, percentWeaponDamage={percentWeaponDamage}, base damage={damage}");

        if (usesWeaponDamage && owner != null)
        {
            Debug.Log($"[Projectile.GetDamage] Entering weapon damage logic");

            // Read damage from the player's class-defined WeaponConfig.
            WeaponConfig weapon = PlayerUtil.GetWeapon(owner);
            Debug.Log($"[Projectile.GetDamage] Fallback to WeaponConfig: {(weapon != null ? weapon.weaponName : "NULL")}");

            if (weapon != null)
            {
                // Roll damage between weapon's min and max (base Tier I values)
                float weaponDamage = UnityEngine.Random.Range(
                    weapon.weaponDamageMin,
                    weapon.weaponDamageMax + 1 // +1 to include max value
                );

                // Apply percentage modifier
                float finalDamage = weaponDamage * (percentWeaponDamage / 100f);
                Debug.Log($"[Projectile.GetDamage] Returning fallback weapon damage: {weaponDamage} * {percentWeaponDamage}% = {finalDamage}");
                return finalDamage;
            }
        }

        Debug.Log($"[Projectile.GetDamage] Returning base ability damage: {damage}");
        return damage;
    }

    /// <summary>
    /// Get the damage type to use (weapon damage type if useWeaponDamageType, otherwise ability damage type)
    /// </summary>
    public string GetEffectiveDamageType()
    {
        // If projectile is configured to use weapon damage type and has an owner
        if (usesWeaponDamage && owner != null)
        {
            // Read the damage type from the player's class-defined WeaponConfig.
            WeaponConfig weapon = PlayerUtil.GetWeapon(owner);
            if (weapon != null && !string.IsNullOrEmpty(weapon.weaponDamageType))
            {
                return weapon.weaponDamageType;
            }
        }

        // Otherwise use projectile's damage type
        return damageTypeName;
    }

    protected virtual void HandleCollision(Collider2D other)
    {
        Vector3 impactPosition = transform.position;
        int otherLayer = other.gameObject.layer;
        bool shouldDestroy = ((1 << otherLayer) & destroyOnLayers) != 0;
        bool countsPierce = ((1 << otherLayer) & canPierceLayers) != 0;
        Debug.Log($"[Projectile.HandleCollision] Collided with {other.name} on layer {LayerMask.LayerToName(otherLayer)} (shouldDestroy={shouldDestroy}, countsPierce={countsPierce})");
        Debug.Log($"[Projectile.HandleCollision] Collided with {other.name}, calling GetDamage()...");
        float damageToApply = GetDamage();
        Debug.Log($"[Projectile.HandleCollision] GetDamage() returned: {damageToApply}");

        ProcessHit(other, damageToApply);
        OnHitTarget(other);

        // Broadcast hit VFX to ALL observers including the server/host.
        // Removing ExcludeServer ensures the host uses the same SpawnLocalEffect path as clients,
        // avoiding issues with locally-instantiated prefabs that have a NetworkObject component.
        if (IsSpawned)
        {
            NetworkObject ownerNob = owner != null ? owner.GetComponent<NetworkObject>() : null;
            RpcCreateHitEffect(impactPosition, ownerNob, parentConfig?.abilityName);
        }
        else
        {
            CreateHitEffect(impactPosition, other); // single-player fallback
            // Centralized hit visual from AbilityDataConfig (shared across all ability types)
            HitVisualHelper.SpawnHitVisual(parentConfig, impactPosition, other, transform.rotation);
        }

        // If this layer should destroy the projectile, destroy immediately (unless we just chained)
        Debug.Log($"[Projectile.HandleCollision] Post-OnHitTarget: justChained={justChained}, shouldDestroy={shouldDestroy}, countsPierce={countsPierce}");
        if (shouldDestroy && !justChained)
        {
            {
                DestroyProjectile(fromHit: true);
            }
        }
        // If this layer counts toward pierce limit, increment pierce count (unless we just chained)
        if (countsPierce && !justChained)
        {
            Debug.Log($"[Projectile.HandleCollision] Incrementing pierce count for {other.name}");

            currentPierceCount++;
            Debug.Log($"[Projectile.HandleCollision] Pierce count for {other.name}: {currentPierceCount}/{pierceCount}");
            Debug.Log($"[Projectile.HandleCollision] hasPierce={hasPierce}, pierceCount={pierceCount}, currentPierceCount={currentPierceCount}");
            // Pierce count represents how many times it can pierce (pass through)
            // pierceCount=0 means no piercing (destroy on first hit)
            // pierceCount=1 means hit once and destroy
            // pierceCount=7 means hit 7 enemies total
            if (!hasPierce || pierceCount == 0 || (currentPierceCount >= pierceCount && pierceCount > 0))
            {
                Debug.Log($"[Projectile.HandleCollision] Pierce limit reached for {other.name}, destroying projectile");
                DestroyProjectile(fromHit: true);
            }
        }

        // Safety check: If we have chaining enabled but didn't chain (exhausted or no target),
        // and we don't have pierce enabled, destroy the projectile.
        // This handles cases where layer masks might not be properly configured.
        if (!justChained && hasChaining && !hasPierce && !isDestroying)
        {
            Debug.Log($"[Projectile.HandleCollision] Chaining projectile couldn't chain and has no pierce — destroying. currentChainCount={currentChainCount}, maxChains={maxChains}");
            DestroyProjectile(fromHit: true);
        }
    }

    protected virtual void ProcessHit(Collider2D target, float damage)
    {
        Debug.Log($"[Projectile.ProcessHit] Hitting {target.name} with damage={damage}");

        // Get effective damage type (weapon damage type if configured, otherwise ability damage type)
        string effectiveDamageType = GetEffectiveDamageType();
        Debug.Log($"[Projectile.ProcessHit] Effective damage type: {effectiveDamageType}");

        // Try to damage the target using the ScriptableObject system
        // Use GetComponentInParent to support enemies with colliders on child GameObjects
        IDamageable damageable = target.GetComponentInParent<IDamageable>();

        // Reusable hitbox damage processing (trait scaling, crit, life steal, healing). The
        // charge-multiplied, weapon-resolved base damage and the effective damage type are passed
        // as overrides so the projectile's own charge/weapon logic is preserved.
        if (hitbox != null)
        {
            hitbox.ApplyDamage(target, owner, owner, owner, transform.position, abilityName, abilityTags, parentConfig,
                damage * damageMultiplier, effectiveDamageType);
        }

        if (damageable != null && dealsDamageOverTime && damagePerTick > 0)
        {
            GameObject dotObject = new GameObject($"DoT_{effectiveDamageType}");
            dotObject.transform.SetParent(target.transform);
            DotEffect dotEffect = dotObject.AddComponent<DotEffect>();
            dotEffect.Initialize(damageable, gameObject, effectiveDamageType, damagePerTick, dotInterval, dotDuration, dotParticleEffectPrefab, startParticlesFromFeet);
        }

        // Reusable on-hit status effects (pass owner so triggered abilities are attributed correctly)
        hitbox?.ApplyOnHitEffects(target.gameObject, gameObject, owner);

        // Reusable knockback (radial — away from the projectile)
        if (hitbox != null)
            hitbox.ApplyKnockback(target, gameObject, (target.transform.position - transform.position).normalized);

        // Handle projectile chaining
        justChained = false; // Reset flag
        Debug.Log($"[Projectile.OnHitTarget] Reset justChained=false");
        Debug.Log($"[Projectile.Chain] hasChaining={hasChaining}, currentChainCount={currentChainCount}, maxChains={maxChains}");
        // Track the current target so we don't chain back to it
        Enemy hitEnemy = target.GetComponentInParent<Enemy>();
        if (hitEnemy != null) chainedEnemies.Add(hitEnemy);
        if (hasChaining && currentChainCount < maxChains)
        {
            // Find nearest valid target within chain range
            Collider2D nextTarget = FindNextChainTarget(target);
            if (nextTarget != null)
            {
                // Track the next target before redirecting
                Enemy nextEnemy = nextTarget.GetComponentInParent<Enemy>();
                if (nextEnemy != null) chainedEnemies.Add(nextEnemy);
                // Redirect projectile to new target
                ChainToTarget(nextTarget);
                currentChainCount++;
                justChained = true; // Mark that we chained successfully
                Debug.Log($"[Projectile.Chain] SUCCESS — chaining to {nextTarget.name} (chain {currentChainCount}/{maxChains})");
            }
            else
            {
                Debug.Log($"[Projectile.Chain] FAILED — no valid Enemy chain target found within range={chainRange} from pos={transform.position}");
            }
        }

    }

    protected virtual Collider2D FindNextChainTarget(Collider2D currentTarget)
    {
        // Search all colliders in range — use no layer mask so tag-based filtering is the authority.
        // hitLayers may not include every enemy sub-collider layer, causing missed targets.
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, chainRange);
        // Separate candidates into unique (not yet hit) and already-hit, to prefer unique targets
        Collider2D uniqueClosest = null;
        float uniqueClosestDist = float.MaxValue;
        Collider2D fallbackClosest = null;
        float fallbackClosestDist = float.MaxValue;

        foreach (Collider2D col in nearbyColliders)
        {
            // Skip the exact collider we just hit
            if (col == currentTarget)
            {
                continue;
            }

            // Require Enemy tag on self or root
            Enemy enemyComponent = col.GetComponentInParent<Enemy>();
            if (enemyComponent == null)
            {
                continue;
            }

            // Skip dead enemies
            if (!enemyComponent.IsAlive)
            {
                continue;
            }

            bool alreadyHit = chainedEnemies.Contains(enemyComponent);

            float distance = Vector2.Distance(transform.position, enemyComponent.transform.position);
            Debug.Log($"[Projectile.FindNextChainTarget] Candidate: {enemyComponent.name} dist={distance:F2} alreadyHit={alreadyHit}");

            if (!alreadyHit)
            {
                if (distance < uniqueClosestDist)
                {
                    uniqueClosestDist = distance;
                    uniqueClosest = col;
                }
            }
            else
            {
                if (distance < fallbackClosestDist)
                {
                    fallbackClosestDist = distance;
                    fallbackClosest = col;
                }
            }
        }

        // Prefer a unique (not-yet-hit) target; fall back to already-hit if none available
        Collider2D closestTarget = uniqueClosest ?? fallbackClosest;
        float closestDistance = uniqueClosest != null ? uniqueClosestDist : fallbackClosestDist;
        return closestTarget;
    }

    protected virtual void ChainToTarget(Collider2D target)
    {
        // Use enemy root transform for accurate positioning (child colliders may be offset)
        Enemy enemyRoot = target.GetComponentInParent<Enemy>();
        Vector3 targetPosition = enemyRoot != null ? enemyRoot.transform.position : target.bounds.center;

        Vector3 oldDirection = direction;
        direction = (targetPosition - transform.position).normalized;

        Debug.Log($"[Projectile.ChainToTarget] {gameObject.name} → {target.name} | from={transform.position} to={targetPosition} | dir {oldDirection} → {direction}");

        // Update rotation to face new target
        if (direction != Vector3.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        // Reset velocity for new direction
        velocity = direction * speed;

        // For homing projectiles, update the homing target
        if (behavior == ProjectileBehavior.Homing)
        {
            //Find Closest Enemy Collider with Enemy Root Transform for homing target
            homingTarget = enemyRoot != null ? enemyRoot.transform : target.transform;
        }
    }

    protected virtual void OnHitTarget(Collider2D target)
    {
        // Override in derived classes for specific hit effects
    }

    protected virtual void CreateHitEffect(Vector3 position, Collider2D target)
    {

        // Get target's sorting order for rendering hit effects in front
        int targetSortingOrder = 0;
        string targetSortingLayer = "Default";
        SpriteRenderer targetRenderer = target.GetComponent<SpriteRenderer>();
        if (targetRenderer != null)
        {
            targetSortingOrder = targetRenderer.sortingOrder;
            targetSortingLayer = targetRenderer.sortingLayerName;
        }

        if (hitVisualPrefab != null)
        {
            GameObject effect = Instantiate(hitVisualPrefab, position, transform.rotation);

            // Set sorting order for sprite renderer
            SpriteRenderer effectRenderer = effect.GetComponent<SpriteRenderer>();
            if (effectRenderer != null)
            {
                effectRenderer.sortingLayerName = targetSortingLayer;
                effectRenderer.sortingOrder = targetSortingOrder + 1;
            }

            // Calculate destruction time based on visual effects
            float maxDuration = 0f;

            // Check particle systems
            ParticleSystem[] effectParticles = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in effectParticles)
            {
                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sortingLayerName = targetSortingLayer;
                    renderer.sortingOrder = targetSortingOrder + 10000;
                }

                // Set particle start rotation to match projectile rotation
                var main = ps.main;
                main.startRotation = transform.eulerAngles.z * Mathf.Deg2Rad;

                // Calculate total duration (main duration + max particle lifetime)
                float duration = main.duration + main.startLifetime.constantMax;
                if (duration > maxDuration)
                {
                    maxDuration = duration;
                }
            }

            // Check for Animator component and animation clip lengths
            Animator animator = effect.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip.length > maxDuration)
                    {
                        maxDuration = clip.length;
                    }
                }
            }

            // Destroy after longest visual effect finishes, minimum 0.5s, maximum 5s
            float destroyDelay = Mathf.Clamp(maxDuration, 0.5f, 5f);
            Destroy(effect, destroyDelay);

            Debug.Log($"[Projectile] Spawned hit visual '{hitVisualPrefab.name}' at {position}, will destroy after {destroyDelay:F2}s (maxDuration={maxDuration:F2}s)");
        }

        // Play hit sound
        if (hitSound != null)
        {
            AudioManager.Instance?.PlaySpatialSound(hitSound, position);
        }
    }

    protected virtual void PlayDestructionAnimation()
    {
        if (isDestroying) return;

        // Stop projectile movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        // Disable collider
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        // DestroyProjectile handles isDestroying flag, VFX, and network-aware destruction
        DestroyProjectile();
    }

    protected virtual void CreateDestroyEffect(Vector3 position, float angle = 0f)
    {
        Debug.Log($"[Projectile] CreateDestroyEffect called at position {position}");

        // Spawn destroy animation prefab if configured
        if (destroyVisualPrefab != null)
        {
            Debug.Log($"[Projectile] Spawning destroy visual: {destroyVisualPrefab.name}");

            GameObject effect = Instantiate(destroyVisualPrefab, position, Quaternion.Euler(0f, 0f, angle));
            Debug.Log($"[Projectile] Destroy effect spawned successfully: {effect.name}");
            float destroyDelay = AutoDestroyEffect.CalculateLifetime(effect);
            AutoDestroyEffect.SetupAutoDestroy(effect);

            Debug.Log($"[Projectile] Destroy visual will be cleaned up after {destroyDelay:F2}s");
        }
        else
        {
            Debug.LogWarning($"[Projectile] No destroyVisualPrefab assigned!");
        }


        // Play destroy sound
        if (destroySound != null)
        {
            AudioManager.Instance?.PlaySpatialSound(destroySound, position, 1f, Random.Range(0.9f, 1.1f));
        }
    }

    protected virtual void OnProjectileDestroy()
    {
        // Detach all children (including trail) so they survive this object's destruction.
        // Trail is handled specifically by DetachAndDestroyTrail() which runs on ALL clients
        // via the ObserversRpc before Despawn. For single-player, it runs here.
        hitbox?.OnDestroy(gameObject, owner ?? gameObject);
        DetachAndDestroyTrail();
        transform.DetachChildren();
    }

    /// <summary>
    /// Detach the trail particle system from the projectile, stop emission, and schedule
    /// a delayed destroy so existing particles can finish rendering.
    /// Called on ALL clients (via RpcCreateDestroyEffect in network mode, or
    /// OnProjectileDestroy in single-player) so the trail fades out instead of vanishing.
    /// </summary>
    private void DetachAndDestroyTrail()
    {
        if (trailChildInstance == null) return;
        trailChildInstance.transform.SetParent(null);
        trailChildInstance.Stop();
        Destroy(trailChildInstance.gameObject, trailChildInstance.main.startLifetime.constantMax);
        trailChildInstance = null; // prevent double-processing
    }

    public virtual void DestroyProjectile(bool fromHit = false, float angle = 0f)
    {
        if (isDestroying) return;
        isDestroying = true;

        Debug.Log($"[Projectile] DestroyProjectile called for {gameObject.name} at position {transform.position}");
        OnProjectileDestroy();

        if (IsSpawned)
        {
            // Broadcast destroy VFX to ALL observers (including host) before despawning.
            // RPC is queued before Despawn so it is sent/received prior to the despawn message.
            // When called from a hit, spawnEffect=false so only trail cleanup happens (hit VFX already played).
            // ObserversRpc can only be invoked by the server; a non-authoritative copy (e.g. a
            // late/duplicate call from a non-server client) would otherwise log a FishNet warning
            // and silently fail to reach observers.
            if (IsServerStarted)
            {
                NetworkObject ownerNob = owner != null ? owner.GetComponent<NetworkObject>() : null;
                RpcCreateDestroyEffect(transform.position, !fromHit, angle, ownerNob, parentConfig?.abilityName);
            }

            if (IsServerStarted)
            {
                Debug.Log($"[Projectile] Server despawning networked projectile: {gameObject.name}");
                Despawn(); // NetworkBehaviour convenience method
            }
            // Clients do nothing — the server's Despawn handles cleanup on all clients
        }
        else
        {
            // Single-player or non-networked projectile
            Debug.Log($"[Projectile] Destroying local projectile: {gameObject.name}");
            if (!fromHit)
                CreateDestroyEffect(transform.position, angle);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Broadcast hit VFX to ALL observers including the server/host. Both sides use
    /// SpawnLocalEffect, avoiding any issues with locally-instantiated NetworkObjects.
    /// The Projectile's own hitVisualPrefab/parentConfig fields are only populated on the machine
    /// that locally called InitializeFromConfig (server or the predictive owner) — pure observers
    /// never ran it, so we also resolve the centralized ability visual via the owner's ability
    /// config (identical asset on every client) rather than relying on those fields there.
    /// </summary>
    [ObserversRpc]
    private void RpcCreateHitEffect(Vector3 position, NetworkObject ownerNob, string abilityName)
    {
        if (hitSound != null)
            AudioManager.Instance?.PlaySpatialSound(hitSound, position, 1f, Random.Range(0.9f, 1.1f));
        GameObject hitPrefab = hitVisualPrefab;
        if (hitPrefab == null && ownerNob != null)
        {
            AbilityDataConfig abilityConfig = NetworkVisualEffects.ResolveAbilityConfig(ownerNob, abilityName);
            hitPrefab = abilityConfig?.projectileConfig?.hitbox.effects.hitEffectPrefab;
        }

        if (hitPrefab != null)
            SpawnLocalEffect(hitPrefab, position, transform.rotation);
    }

    /// <summary>
    /// Broadcast destroy VFX to ALL observers including the server/host. Called before
    /// Despawn() so the RPC is queued first and arrives before the despawn message.
    /// destroyVisualPrefab is only populated on the machine that spawned this projectile locally,
    /// so pure observers resolve the same prefab via the owner's ability config instead.
    /// </summary>
    [ObserversRpc]
    private void RpcCreateDestroyEffect(Vector3 position, bool spawnEffect, float angle, NetworkObject ownerNob, string abilityName)
    {
        // Detach trail on ALL clients before the Despawn message arrives.
        // On the server, OnProjectileDestroy already ran and set trailChildInstance = null,
        // so this is a no-op there. On remote clients, this is the only chance to detach
        // the trail before Despawn destroys the entire hierarchy.
        DetachAndDestroyTrail();

        if (!spawnEffect)
            return;

        GameObject destroyPrefab = destroyVisualPrefab;
        if (destroyPrefab == null && ownerNob != null)
        {
            AbilityDataConfig abilityConfig = NetworkVisualEffects.ResolveAbilityConfig(ownerNob, abilityName);
            destroyPrefab = abilityConfig?.projectileConfig?.destroyVisualPrefab;
        }

        if (destroyPrefab != null)
            SpawnLocalEffect(destroyPrefab, position, Quaternion.Euler(0f, 0f, angle));

        if (destroySound != null)
            AudioManager.Instance?.PlaySpatialSound(destroySound, position, 1f, Random.Range(0.9f, 1.1f));
    }

    /// <summary>
    /// Spawn a visual effect locally. Used by clients via ObserversRpc.
    /// Handles auto-destruction timing based on particle/animation duration.
    /// </summary>
    private void SpawnLocalEffect(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        HitVisualHelper.SpawnEffect(prefab, position, rotation);
    }

    // Public setters
    public void SetSpeed(float newSpeed) => speed = newSpeed;
    public void SetLifetime(float newLifetime) => lifetime = newLifetime;
    public void SetMaxRange(float newMaxRange) => maxRange = newMaxRange;
    // Implement Homing to find closest enemy and travel toward it. homingTarget is set on hit for chaining, but can also be set externally for homing projectiles that start with a target.
    public void SetHomingTarget(Transform target) => homingTarget = target;

    /// <summary>
    /// Set homing search parameters. Call before Initialize().
    /// </summary>
    /// <param name="cursorPosition">World position of cursor when ability was cast</param>
    /// <param name="autocast">True if ability was fired via autocast (uses owner position instead)</param>
    public void SetHomingInfo(Vector3 cursorPosition, bool autocast)
    {
        homingSearchCenter = cursorPosition;
        isAutocast = autocast;
    }

    public void SetPierceCount(int count) => pierceCount = count;


    // Public method to set damage type at runtime
    public void SetDamageType(string newDamageTypeName)
    {
        damageTypeName = newDamageTypeName;
    }

    // Set the owner of the projectile
    public void SetOwner(GameObject newOwner)
    {
        owner = newOwner;
        Debug.Log($"[Projectile.SetOwner] Owner set to: {(owner != null ? owner.name : "NULL")}");

        // Apply the owner's ProjectileChain stat on top of the ability's maxChains.
        // Only add chain bonus if the ability already has chaining enabled in config.
        if (owner != null && hasChaining)
        {
            PlayerController pc = owner.GetComponent<PlayerController>();
            if (pc != null)
            {
                int chainBonus = 0;

                // Primary: read from AllStats (fully trait-merged runtime value).
                bool hasInAllStats = pc.AllStats != null && pc.AllStats.HasStat("ProjectileChain");
                if (hasInAllStats)
                {
                    chainBonus = Mathf.RoundToInt(pc.AllStats.GetStat("ProjectileChain"));
                    Debug.Log($"[Projectile.SetOwner] ProjectileChain from AllStats = {chainBonus}");
                }
                else
                {
                    // Fallback: stat may not be in AllStats yet if DoRecalculateStatsWithTraits
                    // hasn't run since the Chaining trait was unlocked (e.g. fired same frame).
                    // Ask the trait manager directly for the flat modifier sum.
                    var ctm = pc.GetComponent<CharacterTraitManager>();
                    if (ctm != null)
                    {
                        chainBonus = Mathf.RoundToInt(ctm.GetFlatModifier("ProjectileChain"));
                        Debug.Log($"[Projectile.SetOwner] ProjectileChain from TraitManager fallback = {chainBonus}");
                    }
                    else
                    {
                        Debug.Log($"[Projectile.SetOwner] No CharacterTraitManager found on owner");
                    }
                }

                if (chainBonus > 0)
                {
                    maxChains += chainBonus;
                    Debug.Log($"[Projectile.SetOwner] ProjectileChain stat +{chainBonus} → maxChains={maxChains}");
                }
                else
                {
                    Debug.Log($"[Projectile.SetOwner] ProjectileChain stat is 0 — no chain bonus applied. hasInAllStats={hasInAllStats}");
                }
            }
            else
            {
                Debug.Log($"[Projectile.SetOwner] Owner has no PlayerController — skipping chain bonus");
            }
        }
    }

    /// <summary>Set the top-level ability config for centralized hit visuals.</summary>
    public void SetParentConfig(AbilityDataConfig dataConfig)
    {
        parentConfig = dataConfig;
    }

    /// <summary>
    /// Configures a lobbed projectile to arc from its spawn position to <paramref name="targetWorldPos"/>.
    /// Must be called on the server after Initialize() so the flight time is correct.
    /// Automatically broadcasts to clients via RpcSetLobbedTarget for synchronized visuals.
    /// </summary>
    /// <param name="targetWorldPos">World-space landing position (enemy root or cursor position).</param>
    public void SetLobbedTarget(Vector3 targetWorldPos)
    {


        _lobbedStartPos = transform.position;
        _lobbedTargetPos = targetWorldPos;
        _lobbedTargetSet = true;
        _lobbedLanded = false;
        behaviorTime = 0f;

        if (projectileCollider != null)
            projectileCollider.enabled = false;

        // Calculate horizontal distance (ignoring Y/Z if you purely want ground distance)
        float distance = Vector3.Distance(_lobbedStartPos, _lobbedTargetPos);
        float executionSpeed = speed > 0f ? speed : 10f;

        float baseStraightTime = distance / executionSpeed;
        if (baseStraightTime <= 1f) baseStraightTime = 1f;

        float heightFactor = lobbedArcHeight > 0 ? lobbedArcHeight : 1f;

        // FIX 1: Consistent Air Time. Time = Distance / Speed.
        // If distance is 5f and speed is 10, flight time is exactly 0.5 seconds.
        _lobbedFlightTime = baseStraightTime * heightFactor;



        // Broadcast landing target to clients so their visual arcs match.
        if (IsSpawned && IsServerStarted)
            RpcSetLobbedTarget(_lobbedStartPos, _lobbedTargetPos, _lobbedFlightTime);
    }

    /// <summary>
    /// Syncs lobbed-projectile target position to all non-server clients so their
    /// parabola arc matches the server's authoritative trajectory.
    /// </summary>
    [ObserversRpc(BufferLast = true, ExcludeServer = true)]
    private void RpcSetLobbedTarget(Vector3 spawnPos, Vector3 targetPos, float flightTime)
    {
        _lobbedStartPos = spawnPos;
        _lobbedTargetPos = targetPos;
        _lobbedFlightTime = flightTime;
        _lobbedTargetSet = true;
        _lobbedLanded = false;

        // Keep collider disabled during flight on clients too (cosmetic parity).
        if (projectileCollider != null)
            projectileCollider.enabled = false;
    }

    // Set ability info for tag-based damage modifiers
    public void SetAbilityInfo(string name, System.Collections.Generic.List<string> tags)
    {
        abilityName = name;
        abilityTags = tags;
    }

    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = multiplier;
    }

    /// <summary>
    /// Helper function to convert LayerMask to readable layer names for debugging
    /// </summary>
    protected string GetLayerNames(LayerMask mask)
    {
        if (mask.value == 0) return "None";
        if (mask.value == -1) return "Everything";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) != 0)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(LayerMask.LayerToName(i));
            }
        }
        return sb.Length > 0 ? sb.ToString() : "None";
    }
}