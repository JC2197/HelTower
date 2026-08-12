using UnityEngine;
using System.Collections.Generic;
using FishNet;
using JoeConticello.VisualEffects;

/// <summary>
/// Runtime controller for summoned pets. Extends Pet to inherit follow behavior,
/// and adds autonomous combat: finds the closest enemy and attacks on an attack speed timer.
/// Supports melee and projectile sub-abilities.
/// </summary>
public class SummonedPet : Pet
{
    private SummonConfig config;
    private GameObject ownerObject;
    private AbilityDataConfig parentAbilityConfig;
    private AbilityDataConfig rawParentAbilityConfig;
    private GameObject currentTarget;
    private float lastAttackTime;
    private float attackCooldown;
    private float attackAnimEndTime;
    private float pendingAttackFireTime = -1f;  // world-time at which the attack payload fires
    private Vector2 pendingAttackDirection;      // direction captured when attack animation started
    private float spawnTime;
    private bool isActive;
    private Animator petAnimator;
    private string currentAnim;
    private bool isAttacking;
    private bool isChasing;

    private AIPathfinding _pathfinding;

    public SummonConfig Config => config;
    public new GameObject Owner => ownerObject;

    /// <summary>
    /// Initialize the summoned pet with its config and owning player.
    /// </summary>
    public void Initialize(SummonConfig summonConfig, GameObject caster, AbilityDataConfig parentConfig = null, AbilityDataConfig rawParentConfig = null)
    {
        config = summonConfig;
        ownerObject = caster;
        parentAbilityConfig = parentConfig;
        rawParentAbilityConfig = rawParentConfig;
        ownerTransform = caster.transform;
        spawnTime = Time.time;
        isActive = true;
        isAttacking = false;

        ApplyRuntimeConfig();

        _pathfinding = gameObject.AddComponent<AIPathfinding>();
        _pathfinding.Initialize(config.pathfindingObstacleLayers, config.obstacleAvoidanceStrength,
            debug: config.debugDrawPathfindingRays);

        // Health setup via Organism stat container
        // maxHealth == 0 means invulnerable — skip to avoid ModifyHealth(0) triggering Die().
        if (config.maxHealth > 0)
        {
            if (statContainer != null)
            {
                statContainer.SetStat("MaxHealth", config.maxHealth);
            }
            try
            {
                ModifyHealth(MaxHealth);
            }
            catch (System.NullReferenceException)
            {
                Debug.LogWarning($"[SummonedPet] '{gameObject.name}': ModifyHealth skipped — SyncVar not network-ready.");
            }
        }

        lastAttackTime = -attackCooldown; // Ready to attack immediately

        petAnimator = GetComponent<Animator>();

        CharacterTraitManager traitManager = ownerObject != null ? ownerObject.GetComponent<CharacterTraitManager>() : null;
        if (traitManager != null)
        {
            traitManager.OnTraitsChanged -= RefreshConfigFromOwner;
            traitManager.OnTraitsChanged += RefreshConfigFromOwner;
        }

        // Set tag so other systems can identify summons
        gameObject.tag = "Summon";

        Debug.Log($"[SummonedPet] Initialized '{gameObject.name}' — health={MaxHealth}, attackSpeed={config.attackSpeed}, attackRange={config.attackRange}, detectionRange={config.detectionRange}, attackType={config.attackType}");
    }

    private void ApplyRuntimeConfig()
    {
        if (config == null)
            return;

        followDistance = config.followDistance;
        stopDistance = config.stopDistance;
        followSpeed = config.moveSpeed;
        idleAnimation = config.idleAnimation;
        moveAnimation = config.moveAnimation;
        attackCooldown = config.attackSpeed > 0 ? 1f / config.attackSpeed : 1f;
    }

    private void RefreshConfigFromOwner()
    {
        if (ownerObject == null || rawParentAbilityConfig == null)
            return;

        var accumulatedOverrides = AbilityModifierRuntime.AccumulateOverridesFromOwner(ownerObject, rawParentAbilityConfig);
        AbilityDataConfig effectiveParent = AbilityModifierRuntime.BuildEffectiveAbilityConfig(rawParentAbilityConfig, accumulatedOverrides);
        parentAbilityConfig = effectiveParent ?? rawParentAbilityConfig;

        if (parentAbilityConfig.summonConfig != null)
        {
            config = CloneSubConfig(parentAbilityConfig.summonConfig);
            ApplyRuntimeConfig();
            Debug.Log($"[SummonedPet] Refreshed runtime config for '{gameObject.name}' from owner trait changes.");
        }
    }

    protected override void HandleUpdate()
    {
        if (!isActive) return;

        // Check lifetime
        if (config.lifetime > 0 && Time.time >= spawnTime + config.lifetime)
        {
            Debug.Log($"[SummonedPet] Lifetime expired for '{gameObject.name}'");
            HandleDeath();
            return;
        }

        // Combat: find target and attack
        UpdateCombat();

        // Fire pending attack payload when its scheduled time arrives.
        if (pendingAttackFireTime >= 0f && Time.time >= pendingAttackFireTime)
        {
            FireAttackPayload(pendingAttackDirection);
            pendingAttackFireTime = -1f;
        }

        // Non-seek summons always follow the player (they attack while moving).
        // Seek summons only follow when not actively chasing or attacking.
        if (!config.seekBehavior || (!isAttacking && !isChasing))
        {
            base.HandleUpdate();
        }
    }

    private void UpdateCombat()
    {
        // Validate current target
        if (currentTarget == null || !IsValidTarget(currentTarget))
        {
            currentTarget = FindNearestEnemy();
        }

        if (currentTarget == null)
        {
            isAttacking = false;
            isChasing = false;
            return;
        }

        float distToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);

        // Seek summons chase to within attackRange, then stop and attack.
        // Non-seek summons follow the player and cannot reposition toward enemies, so they fire
        // at any enemy within detectionRange regardless of movement state.
        if (config.seekBehavior)
        {
            if (distToTarget > config.attackRange)
            {
                MoveTowardTarget(currentTarget.transform.position);
                isChasing = true;
                isAttacking = false;
                return;
            }
        }
        else
        {
            // Non-seek: use detectionRange as the fire threshold so attacks fire while following.
            if (distToTarget > config.detectionRange)
            {
                isChasing = false;
                isAttacking = false;
                return;
            }
        }

        // In attack range — attack on cooldown.
        // Seek summons stop to attack; non-seek summons keep moving with the player.
        isAttacking = true;
        isChasing = false;
        if (config.seekBehavior && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Face the target
        FacePosition(currentTarget.transform.position);

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            PerformAttack();
            lastAttackTime = Time.time;
        }
        else if (Time.time >= attackAnimEndTime)
        {
            // Attack animation finished, no attack yet due to cooldown — return to idle.
            PlaySummonAnimation(config.idleAnimation);
        }
    }

    private void MoveTowardTarget(Vector3 targetPos)
    {
        Vector2 preferredDir = ((Vector2)targetPos - (Vector2)transform.position).normalized;
        Vector2 direction = CalculateBestMovementDirection(preferredDir);
        if (rb != null)
        {
            rb.linearVelocity = direction * config.moveSpeed;
        }

        FacePosition(targetPos);
        PlaySummonAnimation(config.moveAnimation);
    }

    /// <summary>
    /// Returns the steered movement direction, delegating to the AIPathfinding component.
    /// </summary>
    private Vector2 CalculateBestMovementDirection(Vector2 preferredDirection)
    {
        if (_pathfinding == null || preferredDirection == Vector2.zero) return preferredDirection;
        return _pathfinding.GetSteeringDirectionFromPreferred(preferredDirection);
    }

    private void FacePosition(Vector3 pos)
    {
        if (petSpriteRenderer != null)
        {
            bool shouldFaceLeft = pos.x < transform.position.x;
            petSpriteRenderer.flipX = shouldFaceLeft;
        }
    }

    private void PerformAttack()
    {
        if (currentTarget == null) return;

        // Force replay: clear currentAnim so PlaySummonAnimation never skips a re-trigger.
        currentAnim = null;
        float clipLength = GetAnimClipLength(config.attackAnimation);
        PlaySummonAnimation(config.attackAnimation);
        attackAnimEndTime = Time.time + clipLength;

        // Snapshot direction now (target may move) and schedule payload at the configured frame.
        pendingAttackDirection = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized;
        pendingAttackFireTime = Time.time + clipLength * Mathf.Clamp01(config.attackTriggerNormalizedTime);
    }

    private void FireAttackPayload(Vector2 direction)
    {
        switch (config.attackType)
        {
            case SummonAttackType.Melee:
                PerformMeleeAttack(direction);
                break;
            case SummonAttackType.Projectile:
                PerformProjectileAttack(direction);
                break;
            case SummonAttackType.Beam:
                PerformBeamAttack();
                break;
        }
    }

    private void PerformMeleeAttack(Vector2 direction)
    {
        if (config.meleeConfig == null)
        {
            Debug.LogWarning($"[SummonedPet] MeleeConfig is null on '{gameObject.name}'");
            return;
        }

        MeleeConfig meleeConfig = CloneSubConfig(config.meleeConfig);

        // Damage and type are always driven by the parent SummonConfig — single source of truth.
        meleeConfig.hitbox.damage = config.damage;
        meleeConfig.hitbox.damageTypeName = config.damageTypeName;
        // Life steal is driven by the parent SummonConfig and heals the player owner.
        meleeConfig.hitbox.lifeSteal = config.lifeSteal;

        MeleeAbility meleeAbility = gameObject.AddComponent<MeleeAbility>();
        meleeAbility.SetContext(new SubAbilityContext
        {
            rawParentConfig = rawParentAbilityConfig,
            parentConfig = parentAbilityConfig,
            owner = gameObject,
            statOwner = ownerObject
        });
        meleeAbility.PerformAttack(meleeConfig, direction);

        Debug.Log($"[SummonedPet] Melee attack — target={currentTarget?.name}, direction={direction}");
    }

    private void PerformProjectileAttack(Vector2 direction)
    {
        if (config.projectileConfig == null)
        {
            Debug.LogWarning($"[SummonedPet] ProjectileConfig is null on '{gameObject.name}'");
            return;
        }

        ProjectileConfig projConfig = CloneSubConfig(config.projectileConfig);

        // Clone the hitbox so these summon-driven overrides never mutate the shared config.
        projConfig.hitbox = projConfig.hitbox.Clone();

        // Damage and type are always driven by the parent SummonConfig — single source of truth.
        projConfig.hitbox.damage = config.damage;
        projConfig.hitbox.damageTypeName = config.damageTypeName;
        // Life steal is driven by the parent SummonConfig and heals the player owner.
        projConfig.hitbox.lifeSteal = config.lifeSteal;

        Vector3 spawnPos = transform.position;

        // Pass gameObject as muzzleFlashEntity so the flash is not parented to the owner's weapon
        // (which rotates toward the mouse cursor and would make the flash face the wrong direction).
        ProjectileSpawner.SpawnProjectiles(
            projConfig,
            spawnPos,
            (Vector3)direction,
            ownerObject,
            1f,
            parentConfig: parentAbilityConfig,
            muzzleFlashEntity: gameObject
        );

        Debug.Log($"[SummonedPet] Projectile attack — target={currentTarget?.name}, direction={direction}");
    }

    private void PerformBeamAttack()
    {
        if (config.beamConfig == null)
        {
            Debug.LogWarning($"[SummonedPet] BeamConfig is null on '{gameObject.name}'");
            return;
        }

        BeamAbilityConfig beamCfg = CloneSubConfig(config.beamConfig);
        // Life steal is driven by the parent SummonConfig and heals the player owner.
        beamCfg.lifeSteal = config.lifeSteal;
        // Force auto-target so the beam finds the nearest enemy without cursor input.
        beamCfg.targetingMode = BeamTargetingMode.AutoTargetEnemy;
        beamCfg.canHoldToFire = false;

        // Build a minimal throwaway AbilityDataConfig so BeamAbility.Initialize can read beamConfig.
        // autocast=true puts the beam into single-shot auto-stop mode (no hold-to-fire or button input).
        AbilityDataConfig tempConfig = ScriptableObject.CreateInstance<AbilityDataConfig>();
        tempConfig.beamConfig = beamCfg;
        tempConfig.autocast = true;

        BeamAbility beam = gameObject.AddComponent<BeamAbility>();
        beam.SetContext(new SubAbilityContext
        {
            rawParentConfig = rawParentAbilityConfig,
            parentConfig = parentAbilityConfig,
            owner = gameObject,
            statOwner = ownerObject
        });
        beam.Initialize(tempConfig);
        beam.Activate();

        float cleanupDelay = Mathf.Max(0.5f, beamCfg.singleShotDuration + 0.2f);
        Destroy(beam, cleanupDelay);
        Destroy(tempConfig, cleanupDelay);

        Debug.Log($"[SummonedPet] Beam attack — target={currentTarget?.name}");
    }

    private static T CloneSubConfig<T>(T source) where T : class, new()
    {
        T copy = new T();
        foreach (var field in typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            field.SetValue(copy, field.GetValue(source));
        }

        return copy;
    }

    private GameObject FindNearestEnemy()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, config.detectionRange, LayerMask.GetMask("Enemy"));

        GameObject nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Collider2D col in colliders)
        {
            if (col.gameObject == gameObject || col.gameObject == ownerObject) continue;

            // Skip dead organisms
            Organism organism = col.GetComponent<Organism>();
            if (organism != null && !organism.IsAlive) continue;

            float dist = Vector2.Distance(transform.position, col.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = col.gameObject;
            }
        }

        return nearest;
    }

    private bool IsValidTarget(GameObject target)
    {
        if (target == null) return false;

        // Check alive
        Organism organism = target.GetComponent<Organism>();
        if (organism != null && !organism.IsAlive) return false;

        // Check within detection range
        float dist = Vector2.Distance(transform.position, target.transform.position);
        return dist <= config.detectionRange;
    }

    private void PlaySummonAnimation(string animName)
    {
        if (petAnimator == null || string.IsNullOrEmpty(animName)) return;
        if (animName == currentAnim) return;
        petAnimator.Play(animName, 0);
        currentAnim = animName;
    }

    private float GetAnimClipLength(string animName)
    {
        if (petAnimator == null || petAnimator.runtimeAnimatorController == null) return 0.5f;
        foreach (var clip in petAnimator.runtimeAnimatorController.animationClips)
            if (clip.name == animName) return clip.length;
        return 0.5f; // fallback if clip not found
    }

    protected override void HandleDeath()
    {
        isActive = false;
        Debug.Log($"[SummonedPet] '{gameObject.name}' died");

        // Spawn death visual effect
        if (config != null && config.deathEffectPrefab != null)
        {
            GameObject effect = Object.Instantiate(config.deathEffectPrefab, transform.position, Quaternion.identity);
            AutoDestroyEffect.SetupAutoDestroy(effect, 3f);
        }

        Destroy(gameObject, 0.1f);
    }

    private void OnDestroy()
    {
        CharacterTraitManager traitManager = ownerObject != null ? ownerObject.GetComponent<CharacterTraitManager>() : null;
        if (traitManager != null)
            traitManager.OnTraitsChanged -= RefreshConfigFromOwner;
    }

    protected new void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, config != null ? config.detectionRange : 8f);

        // Attack range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, config != null ? config.attackRange : 1.5f);

        // Current target line
        if (currentTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}
