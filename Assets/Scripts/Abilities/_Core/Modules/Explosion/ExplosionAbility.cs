using UnityEngine;
using System.Collections;

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
        float baseScale = config.hitbox != null && config.hitbox.scaleX > 0f ? config.hitbox.scaleX : 1f;
        this.combinedScale = baseScale * sizeMultiplier;
        if (combinedScale != 1f)
            Debug.Log($"[ExplosionAbility] Combined scale {combinedScale}x (hitbox.scaleX={baseScale}, sizeMultiplier={sizeMultiplier})");

        // If position hasn't been set (transform is at 0,0,0), calculate explosion position at mouse cursor
        // Otherwise, use the pre-set position (e.g., for traps)
        if (transform.position == Vector3.zero)
        {
            if (parentConfig.castAtFeet)
            {
                transform.position = owner.transform.position;
            }
            else
            {
                Vector3 explosionPosition = InputUtility.GetMouseWorldPosition();
                transform.position = explosionPosition;
            }

        }
        if (parentConfig.castAtFeet)
        {
            transform.position = owner.transform.position;
        }

        // Resolve the single target NOW (at cast time) rather than after any delay, so it
        // matches whichever enemy DataDrivenAbility's autocast/point-and-click targeting
        // already resolved this position from. The delay VFX below follows this same target.
        if (config.singleTargetMode)
        {
            float searchRadius = config.singleTargetSearchRadius > 0f
                ? config.singleTargetSearchRadius
                : (config.activationRange > 0f ? config.activationRange : 3f);
            singleTarget = FindNearestDamageableCollider(transform.position, searchRadius * combinedScale);

            if (singleTarget == null)
            {
                Debug.LogWarning($"[ExplosionAbility] singleTargetMode: no living target found within {searchRadius * combinedScale:F1} units of {transform.position} — ability will fizzle.");
            }
        }

        // Safety-net: guarantee destruction regardless of which visual path runs.
        Destroy(gameObject, config.timeDelay + 2f);

        // Trigger immediately or after a delay
        if (config.timeDelay > 0f)
        {
            StartCoroutine(DelayedExplosion());
        }
        else
        {
            FireExplosion();
        }

        Debug.Log($"[ExplosionAbility] Explosion triggered at {transform.position}");
    }

    private IEnumerator DelayedExplosion()
    {
        // Spawn delay prefab as a child so it inherits position and is destroyed with the parent.
        // In singleTargetMode, parent it to the resolved target instead so the windup indicator
        // follows the enemy rather than sitting at a fixed world position.
        if (config.delayEffectPrefab != null)
        {
            Transform delayParent = (config.singleTargetMode && singleTarget != null) ? singleTarget.transform : transform;
            Vector3 delayPos = (config.singleTargetMode && singleTarget != null) ? singleTarget.transform.position : transform.position;
            GameObject delayVfx = Instantiate(config.delayEffectPrefab, delayPos, Quaternion.identity, delayParent);
            if (combinedScale != 1f)
            {
                SetParticleScalingMode(delayVfx);
                delayVfx.transform.localScale *= combinedScale;
            }
            if (config.timeDelay > 0f)
                SetIndicatorParticleLifetime(delayVfx, config.timeDelay);
            Debug.Log($"[ExplosionAbility] Spawned delay effect prefab, waiting {config.timeDelay}s");
        }

        yield return new WaitForSeconds(config.timeDelay);

        // Children (delay prefab) are destroyed with the parent — no manual cleanup needed
        FireExplosion();
    }

    private void FireExplosion()
    {
        if (!destroyTriggersApplied)
        {
            config?.hitbox?.OnDestroy(gameObject, owner ?? gameObject);
            destroyTriggersApplied = true;
        }

        if (config.singleTargetMode)
        {
            // Point-and-click path: hit the one resolved target directly and attach the visual
            // to them — no area overlap/collider hit-detection involved.
            TriggerSingleTargetHit();
        }
        else
        {
            // Trigger explosion damage immediately
            TriggerExplosion();

            // Spawn explosion effect prefab, scaled to match the explosion area
            if (config.explosionEffectPrefab != null)
            {
                GameObject vfx = Instantiate(config.explosionEffectPrefab, transform.position, Quaternion.identity);
                if (combinedScale != 1f)
                {
                    SetParticleScalingMode(vfx);
                    vfx.transform.localScale *= combinedScale;
                }
                Destroy(vfx, GetEffectDuration(vfx));
                Debug.Log($"[ExplosionAbility] Spawned explosion effect (scale={combinedScale}x)");
            }

            // Play explosion sound
            if (config.explosionSound != null)
                AudioManager.Instance.PlaySpatialSound(config.explosionSound, transform.position, 1f, Random.Range(0.9f, 1.1f));
        }

        // Done — destroy this GO (and any remaining children)
        Destroy(gameObject, 0.1f);
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

    /// <summary>
    /// Ensures all ParticleSystems on the prefab instance use Hierarchy scaling mode so that
    /// scaling the root transform propagates correctly to particle size and emission shape.
    /// Must be called before applying the scale change.
    /// </summary>
    private static void SetParticleScalingMode(GameObject instance)
    {
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    /// <summary>
    /// Finds a child named "Indicator" on the prefab instance and sets its ParticleSystem
    /// startLifetime to <paramref name="duration"/> so the effect plays for exactly the delay period.
    /// </summary>
    private static void SetIndicatorParticleLifetime(GameObject instance, float duration)
    {
        Transform indicator = instance.transform.Find("Indicator");
        if (indicator == null) return;
        ParticleSystem ps = indicator.GetComponent<ParticleSystem>();
        if (ps == null) return;
        var main = ps.main;
        main.startLifetime = duration;
    }


    private void TriggerExplosion()
    {

        Collider2D[] negativeHits = GetNegativeHitsInExplosionArea();
        Collider2D[] positiveHits = GetPositiveHitsInExplosionArea();
        if (negativeHits.Length == 0 && positiveHits.Length == 0)
        {
            Collider2D[] allNearby = Physics2D.OverlapCircleAll(transform.position, config.dimensions.x);
            Debug.LogWarning($"[ExplosionAbility] No hits with layer mask! Found {allNearby.Length} colliders without layer filter:");
            foreach (var c in allNearby)
            {
                Debug.LogWarning($"  - {c.gameObject.name} on layer {LayerMask.LayerToName(c.gameObject.layer)} ({c.gameObject.layer})");
            }
        }

        foreach (Collider2D hit in negativeHits)
        {
            // Try to find IDamageable on the hit object, or in parent/children
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null)
                damageable = hit.GetComponentInChildren<IDamageable>();

            if (damageable == null)
            {
                Debug.LogWarning($"[ExplosionAbility] Hit {hit.gameObject.name} has no IDamageable component (checked self, parent, children)!");
                continue;
            }

            // Reusable hitbox damage (trait scaling, crit, weapon damage, life steal, healing, hit flash)
            config.hitbox.ApplyDamage(hit, owner, owner, owner, transform.position, abilityName, abilityTags, parentConfig);
            // Centralized hit visual from AbilityDataConfig
            HitVisualHelper.SpawnHitVisual(parentConfig, hit.transform.position, hit.gameObject);

            // Reusable knockback (radial — away from the explosion center)
            Vector2 radialDir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            config.hitbox.ApplyKnockback(hit, owner, transform.position);

            // Reusable pull (no-op unless configured)
            config.hitbox.ApplyPull(hit, transform.position);

            // Reusable EffectData on-hit effects (CC, DoT, triggered abilities), scaled to explosion size
            config.hitbox.onHitEffects?.ApplyEffects(hit.gameObject, gameObject, owner, 1f, combinedScale);
        }
        foreach (Collider2D hit in positiveHits)
        {
            config.hitbox.ApplyHealing(hit, owner, owner, owner, hit.transform.position, abilityName, abilityTags, parentConfig);
            config.hitbox.ApplyBuffEffects(hit.gameObject, owner, owner);
            HitVisualHelper.SpawnHitVisual(parentConfig, hit.transform.position, hit.gameObject);
        }
    }

    private Collider2D[] GetNegativeHitsInExplosionArea()
    {
        if (config.shape == ExplosionShape.Circle)
        {
            float radius = config.dimensions.x * combinedScale;
            return Physics2D.OverlapCircleAll(transform.position, radius, config.hitbox.hitLayers);
        }
        else // Rectangle
        {
            return Physics2D.OverlapBoxAll(transform.position, config.dimensions * combinedScale, 0f, config.hitbox.hitLayers);
        }
    }
    private Collider2D[] GetPositiveHitsInExplosionArea()
    {
        if (config.shape == ExplosionShape.Circle)
        {
            float radius = config.dimensions.x * combinedScale;
            return Physics2D.OverlapCircleAll(transform.position, radius, config.hitbox.positiveHitLayers);
        }
        else // Rectangle
        {
            return Physics2D.OverlapBoxAll(transform.position, config.dimensions * combinedScale, 0f, config.hitbox.positiveHitLayers);
        }
    }
    /// <summary>
    /// Point-and-click / single-target path: applies the shared hitbox damage/knockback/pull/
    /// on-hit pipeline to the ONE target resolved at cast time (<see cref="singleTarget"/>), and
    /// attaches explosionEffectPrefab directly to that enemy instead of spawning at a fixed
    /// world position. No area overlap or collider prefab is involved.
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

        // Attach the explosion visual directly to the target so it plays on them and is
        // destroyed afterward, rather than spawning at a fixed world position.
        if (config.explosionEffectPrefab != null)
        {
            GameObject vfx = Instantiate(config.explosionEffectPrefab, singleTarget.transform.position, Quaternion.identity, singleTarget.transform);
            if (combinedScale != 1f)
            {
                SetParticleScalingMode(vfx);
                vfx.transform.localScale = Vector3.one * combinedScale;
            }
            Destroy(vfx, GetEffectDuration(vfx));
            Debug.Log($"[ExplosionAbility] singleTargetMode: attached explosion effect to '{singleTarget.name}' (scale={combinedScale}x)");
        }

        if (config.explosionSound != null)
            AudioManager.Instance.PlaySpatialSound(config.explosionSound, transform.position, 1f, Random.Range(0.9f, 1.1f));

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
