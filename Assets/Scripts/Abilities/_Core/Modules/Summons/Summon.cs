using UnityEngine;
using System.Collections.Generic;
using FishNet;
using JoeConticello.VisualEffects;
using Unity.Mathematics;

public class Summon : Pet
{
    private SummonConfig config;
    private GameObject ownerObject;
    private AbilityDataConfig parentAbilityConfig;
    private AbilityDataConfig rawParentAbilityConfig;

    private GameObject currentTarget;
    private float spawnTime;
    private bool isActive;
    private AIPathfinding _pathfinding;
    protected Transform turretTransform;

    // Cache list of runtime modular ability components running on this entity
    private List<DataDrivenAbility> runtimeAbilities = new List<DataDrivenAbility>();
    public SummonConfig ActiveConfig => config;
    public new GameObject Owner => ownerObject;
    public void Initialize(SummonConfig summonConfig, GameObject owner, List<AbilityDataConfig> runtimeAbilityConfigs, AbilityDataConfig parentConfig = null, AbilityDataConfig rawParentConfig = null)
    {
        config = summonConfig;
        ownerObject = owner;
        parentAbilityConfig = parentConfig;
        rawParentAbilityConfig = rawParentConfig;
        ownerTransform = owner.transform;
        spawnTime = Time.time;
        isActive = true;

        ApplyRuntimeConfig(config);

        _pathfinding = gameObject.AddComponent<AIPathfinding>();
        _pathfinding.Initialize(config.pathfindingObstacleLayers, config.obstacleAvoidanceStrength, debug: config.debugDrawPathfindingRays);

        // --- NEW DATA DRIVEN ABILITY SYSTEM LINK ---
        // Dynamically spin up standard player ability runners directly on this minion body!
        foreach (AbilityDataConfig abilityConfig in runtimeAbilityConfigs)
        {
            if (abilityConfig == null) continue;

            DataDrivenAbility runtimeAbility = gameObject.AddComponent<DataDrivenAbility>();

            // Re-use your baseline data initialization engine safely!
            runtimeAbility.SetAbilityReference(new AbilityReference(abilityConfig));
            runtimeAbility.InitializeAbility();
            runtimeAbility.RebuildConfigModifiers();
            runtimeAbilities.Add(runtimeAbility);
        }

        if (config.isRotationalTurret && !string.IsNullOrEmpty(config.turretChildName))
        {
            turretTransform = FindChildRecursive(transform, config.turretChildName);
        }

        CharacterTraitManager traitManager = ownerObject != null ? ownerObject.GetComponent<CharacterTraitManager>() : null;
        if (traitManager != null)
        {
            traitManager.OnTraitsChanged -= RefreshConfigFromOwner;
            traitManager.OnTraitsChanged += RefreshConfigFromOwner;
        }

        gameObject.tag = "Summon";
    }

    protected override void HandleUpdate()
    {
        if (!isActive) return;

        if (config.lifetime > 0 && Time.time >= spawnTime + config.lifetime)
        {
            HandleDeath();
            return;
        }

        UpdateCombat();

        if (!config.seekBehavior || (!isChasing && !isAttacking))
        {
            base.HandleUpdate();
        }
    }

    private bool isAttacking = false;
    private bool isChasing = false;

    private void UpdateCombat()
    {
        if (currentTarget == null || !IsValidTarget(currentTarget))
        {
            currentTarget = FindNearestEnemy();
        }

        if (config.isRotationalTurret && turretTransform != null && currentTarget != null)
            RotateTurretToward(currentTarget.transform.position);

        if (currentTarget == null)
        {
            isAttacking = false;
            isChasing = false;
            return;
        }

        float distToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);

        if (config.seekBehavior)
        {
            if (distToTarget > config.attackRange)
            {
                MoveTowardTarget(currentTarget.transform.position);
                isChasing = true;
                isAttacking = false;
                return;
            }
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
        else if (distToTarget > config.detectionRange)
        {
            isChasing = false;
            isAttacking = false;
            return;
        }

        isAttacking = true;
        isChasing = false;

        FacePosition(currentTarget.transform.position);

        // --- NEW COMBAT PIPELINE EXECUTION ---
        // Cycle through every initialized capability component and let them process their own parameters!
        foreach (DataDrivenAbility ability in runtimeAbilities)
        {
            if (ability == null) continue;

            // Command the modular ability to fire at the targeted world vector position coordinate points
            // This safely automatically checks internal cooldowns, ammunition stats, and scales correctly!
            ability.TryUseAbilityAt(currentTarget.transform.position);
        }
    }

    private void MoveTowardTarget(Vector3 targetPos)
    {
        Vector2 preferredDir = ((Vector2)targetPos - (Vector2)transform.position).normalized;
        Vector2 direction = CalculateBestMovementDirection(preferredDir);
        if (rb != null) rb.linearVelocity = direction * config.moveSpeed;

        FacePosition(targetPos);
        PlayAnimation(config.moveAnimation);
    }
    private Vector2 CalculateBestMovementDirection(Vector2 preferredDirection)
    {
        if (_pathfinding == null || preferredDirection == Vector2.zero) return preferredDirection;
        return _pathfinding.GetSteeringDirectionFromPreferred(preferredDirection);
    }
    protected virtual void RotateTurretToward(Vector3 targetPosition)
    {
        Vector2 direction = (targetPosition - turretTransform.position).normalized;
        if (direction.sqrMagnitude <= 0f)
            return;

        turretTransform.right = direction;

        SpriteRenderer turretSprite = turretTransform.GetComponent<SpriteRenderer>();
        if (turretSprite != null)
            turretSprite.flipY = direction.x < 0f;
    }

    private void FacePosition(Vector3 pos)
    {
        if (petSpriteRenderer != null)
        {
            bool shouldFaceLeft = pos.x < transform.position.x;
            petSpriteRenderer.flipX = shouldFaceLeft;
        }
    }

    private GameObject FindNearestEnemy()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, config.detectionRange, LayerMask.GetMask("Enemy"));
        GameObject nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Collider2D col in colliders)
        {
            Organism organism = col.GetComponentInParent<Organism>();
            if (organism == null || organism.gameObject == gameObject || organism.gameObject == ownerObject || !organism.IsAlive)
                continue;

            float dist = Vector2.Distance(transform.position, organism.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = organism.gameObject;
            }
        }
        return nearest;
    }

    private bool IsValidTarget(GameObject target)
    {
        if (target == null) return false;
        Organism organism = target.GetComponentInParent<Organism>();
        return organism != null
            && organism.IsAlive
            && Vector2.Distance(transform.position, organism.transform.position) <= config.detectionRange;
    }

    public void ApplyRuntimeConfig(SummonConfig refreshedConfig)
    {
        if (config == null) return;
        config = refreshedConfig;
        followDistance = config.followDistance;
        stopDistance = config.stopDistance;
        followSpeed = config.moveSpeed;
        idleAnimation = config.idleAnimation;
        moveAnimation = config.moveAnimation;
        if (config.statContainer != null && statContainer != null)
        {
            float previousMaxHealth = MaxHealth; // Record current maximum health data ceiling

            // Source 22: Copy our upgraded trait values cleanly onto matching stats in the minion container!
            config.statContainer.CopyToStatContainer(statContainer);

            // 3. Proportional health adjustment handshake
            if (MaxHealth > 0f && previousMaxHealth > 0f && !Mathf.Approximately(previousMaxHealth, MaxHealth))
            {
                float healthRatio = (float)CurrentHealth / previousMaxHealth;
            
                Debug.Log($"[LiveStatSync] Restructured minion health limits mid-round. MaxHealth expanded from {previousMaxHealth} to {MaxHealth}.");
            }

            // 4. Recalculate movement dependencies instantly
            float updatedSpeed = statContainer.GetStat("MoveSpeed", 0f);
            if (updatedSpeed > 0f)
            {
                followSpeed = updatedSpeed; // Updates your base.Pet follow velocity parameter instantly!
            }
        }
    }

    private void RefreshConfigFromOwner()
    {
        if (ownerObject == null || rawParentAbilityConfig == null) return;

        var accumulatedOverrides = AbilityModifierRuntime.AccumulateOverridesFromOwner(ownerObject, rawParentAbilityConfig);
        AbilityDataConfig effectiveParent = AbilityModifierRuntime.BuildEffectiveAbilityConfig(rawParentAbilityConfig, accumulatedOverrides);
        parentAbilityConfig = effectiveParent ?? rawParentAbilityConfig;

        if (parentAbilityConfig.summonConfig != null)
        {
            config = CloneSubConfig(parentAbilityConfig.summonConfig);
            ApplyRuntimeConfig(config);

            // STEP A: Fetch the player character's capability manager
            CharacterAbilityManager playerAbilityManager = ownerObject.GetComponent<CharacterAbilityManager>();
            if (playerAbilityManager != null)
            {
                // Force your currently alive summons to update their C# configuration references mid-round!
                RefreshActiveAbilityConfigs(playerAbilityManager);
            }
            else
            {
                // Fallback if component lookups drop out
                foreach (var ability in runtimeAbilities)
                {
                    if (ability != null) ability.RebuildConfigModifiers();
                }
            }
            
            Debug.Log($"[LiveSync] Mid-round trait changes successfully cascaded to active summon sub-abilities.");
        }
    }

    public void RefreshActiveAbilityConfigs(CharacterAbilityManager playerAbilityManager)
    {
        if (playerAbilityManager == null || runtimeAbilities == null || runtimeAbilities.Count == 0) 
            return;

        Debug.Log($"[LiveSync] Synchronizing active sub-abilities for '{gameObject.name}' with player upgrades.");

        // 1. Fetch EVERY live data-driven capability running on the player root object
        var playerActiveAbilities = playerAbilityManager.GetComponents<DataDrivenAbility>();

        // 2. Loop through the active abilities running on this minion/pet entity body
        foreach (DataDrivenAbility minionAbility in runtimeAbilities)
        {
            if (minionAbility == null) continue;

            bool foundUpgrade = false;

            // 3. Scan the player's running components for a name match
            foreach (DataDrivenAbility playerAbility in playerActiveAbilities)
            {
                if (playerAbility != null && 
                    playerAbility.EffectiveAbilityConfig != null && 
                    string.Equals(playerAbility.AbilityName, minionAbility.AbilityName, System.StringComparison.OrdinalIgnoreCase))
                {
                    // CRITICAL RE-LINK HANDSHAKE: Force the pet's runner to point to the player's active runtime copy!
                    minionAbility.SetAbilityReference(new AbilityReference(playerAbility.EffectiveAbilityConfig));
                    
                    // Force the component to re-calculate its property paths (damage, projectile count, etc.)
                    minionAbility.RebuildConfigModifiers();

                    Debug.Log($"<color=green>[LiveSync] Successfully linked mid-round player upgrade onto active minion ability: '{minionAbility.AbilityName}'</color>");
                    foundUpgrade = true;
                    break;
                }
            }

            // Fallback: If player doesn't have it actively slotted/equipped, just tell the component to refresh itself
            if (!foundUpgrade)
            {
                minionAbility.RebuildConfigModifiers();
            }
        }
    }

    private static T CloneSubConfig<T>(T source) where T : class, new()
    {
        T copy = new T();
        foreach (var field in typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            object value = field.GetValue(source);
            // Deep-clone StatContainer fields so this summon owns its own stats instead of
            // aliasing (and later mutating) the shared ability config's container.
            if (field.FieldType == typeof(StatContainer) && value is StatContainer sharedStats)
                value = sharedStats.Clone();
            field.SetValue(copy, value);
        }
        return copy;
    }

    protected override void HandleDeath()
    {
        isActive = false;
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
        if (traitManager != null) traitManager.OnTraitsChanged -= RefreshConfigFromOwner;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }
}


