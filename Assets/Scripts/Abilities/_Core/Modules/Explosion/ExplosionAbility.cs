using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Explosion ability - instant area damage with knockback, visual indicator, and effects
/// </summary>
public class ExplosionAbility : MonoBehaviour, ISubAbility
{
    private ExplosionConfig config;
    private GameObject owner;
    private string abilityName;
    private System.Collections.Generic.List<string> abilityTags;
    private float sizeMultiplier = 1f;
    private float combinedScale = 1f;
    private AbilityDataConfig parentConfig;
    protected HitboxConfig hitbox;
    // singleTargetMode: the one enemy collider resolved at cast time, followed/attached
    // through the delay window (if any) and hit directly when the effect fires.
    private Collider2D singleTarget;
    private bool destroyTriggersApplied;
    private static readonly List<Collider2D> hitboxColliders = new List<Collider2D>();
    private static readonly List<Collider2D> overlapResults = new List<Collider2D>();
    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        owner = context.owner;
        abilityName = context.AbilityName;
        abilityTags = context.AbilityTags;
    }

    /// <summary>
    /// Initialize and trigger the explosion
    /// </summary>
    public void Initialize(ExplosionConfig explosionConfig, float sizeMultiplier = 1f)
    {
        config = explosionConfig;
        this.sizeMultiplier = sizeMultiplier;

        CalculateScaleDimensions();
        EstablishCastOriginAndTransform();
        ResolveSingleTargetTracking();
        ScheduleLifecycleSafetyNet();
        // Target tracking resolution
        if (config.salvos)
        {
            ExecuteSalvoSequenceFlow();
        }
        else
        {
            ExecuteFlatMultiCastFallbackFlow();
        }
    }
    private void CalculateScaleDimensions()
    {
        float baseScale = config.hitbox != null && config.hitbox.scaleX > 0f ? config.hitbox.scaleX : 1f;
        combinedScale = baseScale * sizeMultiplier;
    }
    
    private IEnumerator ExecuteExplosionAfterDelays(float salvoStartDelay, Vector3 position)
    {
        if (salvoStartDelay > 0f)
            yield return new WaitForSeconds(salvoStartDelay);

        GameObject delayEffect = SpawnDelayEffect(position);
        if (config.timeDelay > 0f)
            yield return new WaitForSeconds(config.timeDelay);

        if (delayEffect != null)
        {
            delayEffect.SetActive(false);
            Destroy(delayEffect);
        }

        Vector3 explosionPosition = config.singleTargetMode && singleTarget != null && !config.salvoOffset
            ? singleTarget.transform.position
            : position;
        ExecuteExplosionEffectsAndDamage(explosionPosition);
    }

    private GameObject SpawnDelayEffect(Vector3 position)
    {
        if (config.timeDelay <= 0f || config.delayEffectPrefab == null)
            return null;

        bool followsTarget = config.singleTargetMode && singleTarget != null && !config.salvoOffset;
        Transform parent = followsTarget ? singleTarget.transform : transform;
        Vector3 spawnPosition = followsTarget ? singleTarget.transform.position : position;
        GameObject instance = Instantiate(config.delayEffectPrefab, spawnPosition, Quaternion.identity, parent);

        SetParticleScalingMode(instance);
        float scaleX = config.hitbox != null && config.hitbox.scaleX > 0f ? config.hitbox.scaleX : 1f;
        float scaleY = config.hitbox != null && config.hitbox.scaleY > 0f ? config.hitbox.scaleY : 1f;
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale,
            new Vector3(scaleX * sizeMultiplier, scaleY * sizeMultiplier, 1f));
        SetIndicatorParticleLifetime(instance, config.timeDelay);
        return instance;
    }

    private void ExecuteExplosionEffectsAndDamage(Vector3? overridePosition = null)
    {
        Vector3 targetPos = overridePosition ?? transform.position;
        Vector3 originalPosition = transform.position;
        transform.position = targetPos;
        GameObject hitboxInstance = SpawnHitboxPrefab(targetPos);
        if (hitboxInstance == null)
        {
            transform.position = originalPosition;
            return;
        }

        if (config.singleTargetMode)
        {
            TriggerSingleTargetHit();
        }
        else
        {
            TriggerExplosion(hitboxInstance);
        }

        config.hitbox.OnDestroy(hitboxInstance, owner ?? gameObject);
        Destroy(hitboxInstance, GetEffectDuration(hitboxInstance));
        transform.position = originalPosition;
    }

    private GameObject SpawnHitboxPrefab(Vector3 position)
    {
        if (config?.hitbox?.prefab == null)
        {
            Debug.LogError("[ExplosionAbility] Hitbox prefab is not assigned.");
            return null;
        }

        GameObject instance = Instantiate(config.hitbox.prefab, position, Quaternion.identity);
        float scaleX = config.hitbox.scaleX > 0f ? config.hitbox.scaleX : 1f;
        float scaleY = config.hitbox.scaleY > 0f ? config.hitbox.scaleY : 1f;
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale,
            new Vector3(scaleX * sizeMultiplier, scaleY * sizeMultiplier, 1f));

        if (config.singleTargetMode && singleTarget != null && !config.salvoOffset)
            instance.transform.SetParent(singleTarget.transform, true);

        return instance;
    }

    private void EstablishCastOriginAndTransform()
    {
        Vector3 castDestination = transform.position == Vector3.zero ? InputUtility.GetMouseWorldPosition() : transform.position;
        if (parentConfig.castAtFeet)
        {
            transform.position = owner.transform.position;
        }
        else
        {
            transform.position = castDestination;
        }
    }

    private void ResolveSingleTargetTracking()
    {
        if (!config.singleTargetMode)
            return;

        if (parentConfig.autocast)
        {
            float searchRadius = config.singleTargetSearchRadius > 0f ? config.singleTargetSearchRadius : (config.activationRange > 0f ? config.activationRange : 3f);
            singleTarget = FindNearestDamageableCollider(transform.position, searchRadius * combinedScale);
            if (singleTarget == null)
            {
                Debug.LogWarning($"[ExplosionAbility] singleTargetMode: no living target found within {searchRadius * combinedScale:F1} units of {transform.position} — ability will fizzle.");
            }
        }
        else if (CursorManager.Instance?.TargetedOrganism != null)
        {
            singleTarget = CursorManager.Instance.TargetedOrganism.GetComponentInChildren<Collider2D>();
        }
    }
    private void ScheduleLifecycleSafetyNet()
    {
        float lastSalvoDelay = config.salvos ? Mathf.Max(0, config.salvoAmount - 1) * config.salvoDelay : 0f;
        Destroy(gameObject, lastSalvoDelay + config.timeDelay + 2f);
    }

    private Vector3 GetTargetForwardDirection(Vector3 playerOrigin, Vector3 mouseWorldPos)
    {
        Vector3 direction = (mouseWorldPos - playerOrigin).normalized;
        return direction == Vector3.zero ? Vector3.up : direction;
    }


    private void ExecuteSalvoSequenceFlow()
    {
        TriggerExplosionGroup(transform.position, stepIndex: 0, delayOverride: 0f);
        Vector3 mouseWorldPos = InputUtility.GetMouseWorldPosition();
        Vector3 playerOrigin = owner != null ? owner.transform.position : transform.position;
        Vector3 pathForwardDirection = GetTargetForwardDirection(playerOrigin, mouseWorldPos);

        for (int i = 1; i < config.salvoAmount; i++)
        {
            float totalDelayToSalvo = i * config.salvoDelay;
            Vector3 salvoBaseCenter = CalculateSalvoBaseCenter(i, pathForwardDirection, playerOrigin);
            TriggerExplosionGroup(salvoBaseCenter, i, delayOverride: totalDelayToSalvo);
        }
    }

    private Vector3 CalculateSalvoBaseCenter(int stepIndex, Vector3 pathForwardDirection, Vector3 playerOrigin)
    {
        if (!config.salvoOffset) return transform.position;
        Vector3 direction = Vector3.zero;
        if (config.salvoOffsetMouse)
        {
            direction = pathForwardDirection;
        }
        else if (config.salvoOffsetTarget && singleTarget != null)
        {
            direction = (singleTarget.transform.position - playerOrigin).normalized;
            if (direction == Vector3.zero) direction = pathForwardDirection;
        }
        else if (config.salvoRadial)
        {
            float angle = (360f / config.salvoAmount) * stepIndex;
            direction = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
        }
        if (direction == Vector3.zero) return transform.position;
        float currentDistance = config.salvoOffsetDistance * stepIndex;
        return transform.position + direction * currentDistance;

    }

    private void ExecuteFlatMultiCastFallbackFlow()
    {
        TriggerExplosionGroup(transform.position, stepIndex: 0, delayOverride: -1f);
    }

    private void TriggerExplosionGroup(Vector3 baseCenter, int stepIndex, float delayOverride)
    {
        int totalCasts = Mathf.Max(1, config.multiCastAmount);
        for (int i = 0; i < totalCasts; i++)
        {
            Vector3 targetExplosionPos = baseCenter;
            if ((i > 0 || config.salvoRandom) && config.salvoOffset)
            {
                targetExplosionPos = GetRandomPositionInRadius(baseCenter, config.salvoOffsetDistance);
            }
            float salvoStartDelay = Mathf.Max(0f, delayOverride);
            if (salvoStartDelay > 0f || config.timeDelay > 0f)
            {
                StartCoroutine(ExecuteExplosionAfterDelays(salvoStartDelay, targetExplosionPos));
            }
            else
            {
                ExecuteExplosionEffectsAndDamage(targetExplosionPos);
            }
        }
    }



    private Vector3 GetRandomPositionInRadius(Vector3 center, float radius)
    {
        Vector2 randomPoint = Random.insideUnitCircle * radius;
        return center + new Vector3(randomPoint.x, randomPoint.y, 0f);
    }

    private void OnDestroy()
    {
        if (destroyTriggersApplied)
            return;

        config?.hitbox?.OnDestroy(gameObject, owner ?? gameObject);
        destroyTriggersApplied = true;
    }

    /// <summary>
    /// Returns a destroy delay for a VFX GameObject based on its particle and animator durations.
    /// Falls back to 5 seconds if no duration can be determined.
    /// </summary>
    private static float GetEffectDuration(GameObject instance)
    {
        float maxDuration = 0f;
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            float d = main.duration + main.startLifetime.constantMax;
            if (d > maxDuration) maxDuration = d;
        }
        Animator anim = instance.GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
                if (clip.length > maxDuration) maxDuration = clip.length;
        }
        return Mathf.Clamp(maxDuration, 0.5f, 10f);
    }

    private static void SetParticleScalingMode(GameObject instance)
    {
        foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particleSystem.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    private static void SetIndicatorParticleLifetime(GameObject instance, float duration)
    {
        Transform indicator = instance.transform.Find("Indicator");
        if (indicator == null)
            return;

        ParticleSystem particleSystem = indicator.GetComponent<ParticleSystem>();
        if (particleSystem == null)
            return;

        var main = particleSystem.main;
        main.startLifetime = duration;
    }



    private void TriggerExplosion(GameObject hitboxInstance)
    {
        HashSet<Collider2D> hits = GetHitsFromPrefab(hitboxInstance);
        foreach (Collider2D hit in hits)
        {
            if (config.hitbox.IsNegativeTarget(hit.gameObject))
            {
                config.hitbox.ApplyDamage(hit, owner, owner, owner, transform.position, abilityName, abilityTags, parentConfig);
                config.hitbox.ApplyKnockback(hit, owner, transform.position);
                config.hitbox.ApplyPull(hit, transform.position);
                config.hitbox.onHitEffects?.ApplyEffects(hit.gameObject, gameObject, owner, 1f, combinedScale);
                HitVisualHelper.SpawnHitVisual(parentConfig, hit.transform.position, hit.gameObject);
            }

            if (config.hitbox.IsPositiveTarget(hit.gameObject))
            {
                config.hitbox.ApplyHealing(hit, owner, owner, owner, hit.transform.position, abilityName, abilityTags, parentConfig);
                config.hitbox.ApplyBuffEffects(hit.gameObject, owner, owner);
                HitVisualHelper.SpawnHitVisual(parentConfig, hit.transform.position, hit.gameObject);
            }
        }
    }

    private HashSet<Collider2D> GetHitsFromPrefab(GameObject hitboxInstance)
    {
        var hits = new HashSet<Collider2D>();
        hitboxInstance.GetComponentsInChildren(true, hitboxColliders);
        Physics2D.SyncTransforms();

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = config.hitbox.GetCombinedHitLayers(),
            useTriggers = true
        };

        foreach (Collider2D sourceCollider in hitboxColliders)
        {
            if (sourceCollider == null || !sourceCollider.enabled)
                continue;

            overlapResults.Clear();
            Physics2D.OverlapCollider(sourceCollider, filter, overlapResults);
            foreach (Collider2D result in overlapResults)
            {
                if (result != null && !result.transform.IsChildOf(hitboxInstance.transform))
                    hits.Add(result);
            }
        }

        hitboxColliders.Clear();
        overlapResults.Clear();
        return hits;
    }
    /// <summary>
    /// Point-and-click / single-target path: applies the shared hitbox damage/knockback/pull/
    /// on-hit pipeline to the ONE target resolved at cast time (<see cref="singleTarget"/>), and
    /// attaches the hitbox prefab directly to that enemy instead of spawning at a fixed world position.
    /// </summary>
    private void TriggerSingleTargetHit()
    {
        // The target may have died or been destroyed during the delay window — Unity's
        // overloaded null check on a destroyed Collider2D reference correctly evaluates true.
        if (singleTarget == null)
        {
            Debug.LogWarning("[ExplosionAbility] singleTargetMode: target no longer valid at fire time — skipping hit.");
            return;
        }

        Debug.Log($"[ExplosionAbility] singleTargetMode firing on '{singleTarget.name}' at {singleTarget.transform.position}");

        bool canNegative = config.hitbox.IsNegativeTarget(singleTarget.gameObject);
        bool canPositive = config.hitbox.IsPositiveTarget(singleTarget.gameObject);
        if (!canNegative && !canPositive)
        {
            Debug.LogWarning($"[ExplosionAbility] singleTargetMode: '{singleTarget.name}' is not in a valid hit layer.");
            return;
        }

        if (canNegative)
        {
            // Reusable hitbox damage (trait scaling, crit, weapon damage, life steal, hit flash)
            config.hitbox.ApplyDamage(singleTarget, owner, owner, owner, singleTarget.transform.position, abilityName, abilityTags, parentConfig);

            // Reusable knockback / pull (no-op unless configured)
            config.hitbox.ApplyKnockback(singleTarget, owner, transform.position);
            config.hitbox.ApplyPull(singleTarget, transform.position);

            // Reusable EffectData on-hit effects (CC, DoT, triggered abilities), scaled to size
            config.hitbox.onHitEffects?.ApplyEffects(singleTarget.gameObject, gameObject, owner, 1f, combinedScale);
        }

        if (canPositive)
        {
            config.hitbox.ApplyHealing(singleTarget, owner, owner, owner, singleTarget.transform.position, abilityName, abilityTags, parentConfig);
            config.hitbox.ApplyBuffEffects(singleTarget.gameObject, owner, owner);
        }

        // Centralized hit visual from AbilityDataConfig
        HitVisualHelper.SpawnHitVisual(parentConfig, singleTarget.transform.position, singleTarget.gameObject);

        Debug.Log($"[ExplosionAbility] singleTargetMode hit '{singleTarget.name}' (base damage: {config.hitbox.damage} {config.hitbox.damageTypeName})");
    }

    /// <summary>
    /// Finds the nearest living IDamageable collider within radius of origin, matching the
    /// hitbox's hit layers. Used by singleTargetMode to resolve one guaranteed target without
    /// relying on any collider/overlap gameplay hit-detection belonging to the ability itself.
    /// </summary>
    private Collider2D FindNearestDamageableCollider(Vector3 origin, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius, config.hitbox.GetCombinedHitLayers());

        Collider2D closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
                continue;

            float sqrDist = ((Vector2)hit.transform.position - (Vector2)origin).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = hit;
            }
        }

        return closest;
    }

    private void ApplyExplosionEffects(GameObject target)
    {
        // This method is for additional status effects
        // Root, slow, stun, burn, poison, etc.
        // Would integrate with your status effect system
    }
}
