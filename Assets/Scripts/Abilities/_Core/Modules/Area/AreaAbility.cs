using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;

/// <summary>
/// Area-based ability implementation that handles area effects, damage zones, and visual indicators.
/// Supports merging overlapping areas, damage auras, and complex area shapes (circle, rectangle, cone).
/// </summary>
public class AreaAbility : MonoBehaviour, ISubAbility
{
    // Configuration fields - populated at runtime from AreaConfig
    protected float duration;
    protected float damage;
    protected string damageTypeName;
    protected float damageInterval;
    protected bool dealsDamageOverTime;
    protected float damagePerSecond;
    protected float dotInterval;
    protected float dotDuration;
    protected bool hasDamageTick;
    protected ParticleSystem dotParticleEffectPrefab;
    protected GameObject hitEffectPrefab;
    protected AudioClip spawnSound;
    protected AudioClip hitSound;
    protected bool startParticlesFromFeet = false;
    protected Color hitFlashColor = Color.white;

    // Effects system
    protected EffectData onHitEffects;
    protected LifeStealConfig lifeSteal;

    // Shared hitbox (reused for authoritative hit processing; mirror fields above are sourced from it)
    protected HitboxConfig hitbox;

    // Aura behavior
    protected bool isAura = false;
    protected bool followCaster = true;
    protected float auraDelay = 0f;
    private float auraActivationTime = -1f;
    private bool auraActivated = false;
    private Transform casterTransform;

    // Layer configuration (set from config)
    protected LayerMask hitLayers = -1;

    // Fade-in configuration
    protected bool hasFadeIn = false;
    protected float fadeInDuration = 0.5f;
    private float fadeInStartTime;

    // Shared context from DataDrivenAbility
    protected AbilityDataConfig parentConfig;
    protected GameObject owner;

    private Collider2D spellCollider;
    private float spawnTime;
    private float nextDamageTime;
    private bool isDestroying = false;
    private bool destroyTriggersApplied = false;
    private HashSet<Collider2D> affectedTargets = new HashSet<Collider2D>();

    protected virtual void Awake()
    {
        // Check the root first, then fall back to children (prefabs often nest the collider).
        spellCollider = GetComponent<Collider2D>();
        if (spellCollider == null)
            spellCollider = GetComponentInChildren<Collider2D>();

        // If the collider is on a different GameObject than this component, Unity's
        // OnTriggerEnter2D callbacks won't reach us.  Attach a relay to that child.
        if (spellCollider != null && spellCollider.gameObject != gameObject)
        {
            var relay = spellCollider.gameObject.GetComponent<AreaAbilityTriggerRelay>();
            if (relay == null)
                relay = spellCollider.gameObject.AddComponent<AreaAbilityTriggerRelay>();
            relay.Target = this;
        }
    }

    /// <summary>
    /// Called by AreaAbilityTriggerRelay when the trigger is on a child object.
    /// </summary>
    internal void OnChildTriggerEnter(Collider2D other) => OnTriggerEnter2D(other);
    internal void OnChildTriggerExit(Collider2D other) => OnTriggerExit2D(other);

    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        owner = context.owner;
    }

    /// <summary>
    /// Initialize spell from AreaConfig at runtime
    /// </summary>
    public void InitializeFromConfig(AreaConfig config)
    {
        if (config == null)
        {
            Debug.LogError($"[AreaAbility.InitializeFromConfig] Config is null!");
            return;
        }

        // Source the shared hitbox and mirror its fields into the runtime mirrors used throughout.
        hitbox = config.hitbox;

        // Include both negative and positive target masks so one area can damage enemies
        // while healing/buffing allies in the same tick.
        hitLayers = config.hitbox.GetCombinedHitLayers();

        // Set fade-in settings
        hasFadeIn = config.hasFadeIn;
        fadeInDuration = config.fadeInDuration;

        duration = config.duration;
        isAura = config.isAura;
        followCaster = config.followCaster;
        auraDelay = config.auraDelay;
        damage = config.hitbox.damage;
        damageTypeName = config.hitbox.damageTypeName;
        damageInterval = config.damageInterval;
        hasDamageTick = config.hasDamageTick;
        dealsDamageOverTime = config.dealsDamageOverTime;
        damagePerSecond = config.damagePerSecond;
        dotInterval = config.dotInterval;
        dotDuration = config.dotDuration;
        dotParticleEffectPrefab = config.dotParticleEffectPrefab;
        startParticlesFromFeet = config.startParticlesFromFeet;
        hitEffectPrefab = config.hitbox.effects != null ? config.hitbox.effects.hitEffectPrefab : null;
        spawnSound = config.spawnSound;
        hitSound = config.hitbox.effects != null ? config.hitbox.effects.hitSound : null;
        hitFlashColor = config.hitbox.effects != null ? config.hitbox.effects.hitFlashColor : Color.white;

        // Store effects
        onHitEffects = config.hitbox.onHitEffects;
        lifeSteal = config.hitbox.lifeSteal;

        // Apply scale — the collider and indicator are children and scale with the transform.
        float sx = config.hitbox.scaleX > 0f ? config.hitbox.scaleX : 1f;
        float sy = config.hitbox.scaleY > 0f ? config.hitbox.scaleY : 1f;
        //also scale the particle effects in the area.
        transform.localScale = new Vector3(sx, sy, 1f);
        Debug.Log($"[AreaAbility.InitializeFromConfig] Applied scale: {transform.localScale}");

        // Particle system shapes use scalingMode=Local by default, meaning they are
        // independent of the parent transform scale. Scale them directly so the emission
        // area matches the ability's size.
        // Note: shape.scale is a uniform Vector3 multiplier that works for all shape types.
        // We intentionally do NOT touch shape.radius — artists size the radius on the prefab
        // and shape.scale already handles proportional scaling.
        // These particles are also detached on death (PersistParticleSystems) without
        // re-scaling, so the scaled values are preserved correctly through despawn.
        if (!Mathf.Approximately(sx, 1f) || !Mathf.Approximately(sy, 1f))
        {
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
            {
                var shape = ps.shape;
                if (shape.enabled)
                    shape.scale = Vector3.Scale(shape.scale, new Vector3(sx, sy, 1f));

                // Keep visual density proportional to area width by scaling emission with scaleX.
                if (!Mathf.Approximately(sx, 1f))
                {
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier *= sx;
                    emission.rateOverDistanceMultiplier *= sx;

                    int burstCount = emission.burstCount;
                    if (burstCount > 0)
                    {
                        ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[burstCount];
                        int copied = emission.GetBursts(bursts);
                        for (int i = 0; i < copied; i++)
                        {
                            var burst = bursts[i];
                            var count = burst.count;
                            switch (count.mode)
                            {
                                case ParticleSystemCurveMode.Constant:
                                    count.constant *= sx;
                                    break;
                                case ParticleSystemCurveMode.TwoConstants:
                                    count.constantMin *= sx;
                                    count.constantMax *= sx;
                                    break;
                                case ParticleSystemCurveMode.Curve:
                                case ParticleSystemCurveMode.TwoCurves:
                                    count.curveMultiplier *= sx;
                                    break;
                            }
                            burst.count = count;
                            bursts[i] = burst;
                        }
                        emission.SetBursts(bursts, copied);
                    }
                }
            }
        }

        // Lifetime rule requested for area particles:
        // duration == 0.1 -> set startLifetime, otherwise set system duration.
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            Debug.Log($"[AreaAbility.InitializeFromConfig] ParticleSystem main duration: {main.duration}");
            if (Mathf.Approximately(main.duration, 0.1f))
                main.startLifetime = duration;
            else
                main.duration = duration;
        }

        // Setup light if enabled
        if (config.hasLight)
        {
            GameObject lightObject = new GameObject("AreaLight");
            lightObject.transform.SetParent(transform);
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;
            lightObject.transform.localScale = Vector3.one;

            Light2D light = lightObject.AddComponent<Light2D>();
            light.color = config.lightColor;
            light.intensity = config.lightIntensity;
            light.pointLightOuterRadius = config.lightRadius;

            Debug.Log($"[AreaAbility.InitializeFromConfig] Added Light2D — color:{config.lightColor}, intensity:{config.lightIntensity}, radius:{config.lightRadius}");
        }
    }

    // Collider lives on the prefab — no runtime collider creation needed.

    public void Activate()
    {

        spawnTime = Time.time;

        // Initialize fade-in if enabled (independent of aura delay)
        if (hasFadeIn)
        {
            fadeInStartTime = Time.time;
            SetVisualAlpha(0f);
        }

        // For auras with delay, disable collider and hide ability effects
        if (isAura && auraDelay > 0)
        {
            auraActivated = false;

            // Disable collider during delay (no damage)
            if (spellCollider != null)
            {
                spellCollider.enabled = false;
            }

            // Hide child particles during delay
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Stop();
                ps.gameObject.SetActive(false);
            }

            return; // Skip the rest of activation until delay is over
        }

        // Initialize damage timing for area damage
        if (damageInterval > 0)
        {
            nextDamageTime = spawnTime + damageInterval;
        }

        // Enable collider
        if (spellCollider != null)
        {
            spellCollider.enabled = true;
        }

        // Detect enemies already inside the area and apply the first tick immediately.
        DetectCollidersInArea(applyInitialTick: true);


        // Play spawn sound
        if (spawnSound != null)
        {
            AudioSource.PlayClipAtPoint(spawnSound, transform.position);
        }
    }

    /// <summary>
    /// Set the caster transform for auras that follow the caster
    /// </summary>
    public void SetCaster(Transform caster)
    {
        casterTransform = caster;
    }

    protected virtual void Update()
    {
        if (isDestroying) return;

        // Follow caster if this is an aura and followCaster is enabled
        if (isAura && followCaster && casterTransform != null)
        {
            transform.position = casterTransform.position;
        }

        // Handle aura delay activation
        if (isAura && !auraActivated && auraDelay > 0)
        {
            if (Time.time >= spawnTime + auraDelay)
            {
                auraActivated = true;
                auraActivationTime = Time.time;

                // Enable collider
                if (spellCollider != null)
                {
                    spellCollider.enabled = true;
                }

                // Show and play child particles
                ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particles)
                {
                    ps.gameObject.SetActive(true);
                    ps.Play();
                }

                // Initialize damage timing
                if (damageInterval > 0)
                {
                    nextDamageTime = Time.time + damageInterval;
                }

                // Detect all colliders already in the area and apply the first tick immediately.
                DetectCollidersInArea(applyInitialTick: true);

            }
            return; // Don't process anything else until delay is over
        }

        // Handle fade-in
        if (hasFadeIn && Time.time < fadeInStartTime + fadeInDuration)
        {
            float fadeProgress = (Time.time - fadeInStartTime) / fadeInDuration;
            SetVisualAlpha(fadeProgress);
        }
        else if (hasFadeIn && Time.time >= fadeInStartTime + fadeInDuration)
        {
            // Ensure we're at the configured alpha when fade-in completes
            SetVisualAlpha(1f);
            hasFadeIn = false; // Stop processing fade-in
        }

        // For auras, check if we need to fade out 1s before parent duration ends
        if (isAura && duration > 0)
        {
            float auraStartTime = auraActivationTime >= 0 ? auraActivationTime : spawnTime;
            float fadeOutStartTime = spawnTime + duration - 1f; // Start fade-out 1s before end

            if (Time.time >= fadeOutStartTime && Time.time < spawnTime + duration)
            {
                // Fade out in the last 1 second
                float fadeOutProgress = 1f - (spawnTime + duration - Time.time);
                SetVisualAlpha(1f - fadeOutProgress);
            }
            else if (Time.time >= spawnTime + duration)
            {
                DestroySpell();
                return;
            }
        }
        // Non-aura behavior: duration 0 = destroy after one frame; duration > 0 = time-based
        else if (!isAura)
        {
            if (duration == 0 && Time.time > spawnTime)
            {
                DestroySpell();
                return;
            }
            if (duration > 0 && Time.time >= spawnTime + duration)
            {
                DestroySpell();
                return;
            }
        }

        // Apply area damage on interval if configured
        // For auras with delay, only apply damage after activation
        if (damageInterval > 0 && Time.time >= nextDamageTime && (!isAura || auraActivated || auraDelay == 0))
        {
            if (hasDamageTick) PlayTickPulseEffect();
            ApplyDamageToAllInArea();
            nextDamageTime = Time.time + damageInterval;
        }
    }

    private void PlayTickPulseEffect()
    {
        // Tick pulse is driven by the spellPrefab's own particle systems.
        // AreaAbility does not instantiate separate particles on tick.
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (isDestroying) return;

        // For auras with delay, don't process triggers until activated
        if (isAura && !auraActivated && auraDelay > 0) return;

        if (hitbox == null)
            return;

        bool canNegative = hitbox.IsNegativeTarget(other.gameObject);
        bool canPositive = hitbox.IsPositiveTarget(other.gameObject);
        if (!canNegative && !canPositive)
            return;

        if (canNegative)
            hitbox.ApplyOnHitEffects(other.gameObject, gameObject, owner);

        if (canPositive)
            hitbox.ApplyBuffEffects(other.gameObject, gameObject, owner);

        // Apply immediate effects if damage interval is 0 (instant-only)
        bool hasNegativePayload = damage > 0f || hitbox.useWeaponDamage;
        bool hasPositivePayload = hitbox.positiveHealing > 0f;
        if (damageInterval == 0 && ((canNegative && hasNegativePayload) || (canPositive && hasPositivePayload)))
            ApplyDamage(other);

        affectedTargets.Add(other);

    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        affectedTargets.Remove(other);
    }

    private void ApplyDamageToAllInArea()
    {
        affectedTargets.RemoveWhere(t => t == null);

        foreach (var target in affectedTargets)
        {
            ApplyDamage(target);
        }
    }

    /// <summary>
    /// Detect all colliders currently in the area and add them to affectedTargets.
    /// Used when aura activates to detect enemies already inside.
    /// </summary>
    private void DetectCollidersInArea(bool applyInitialTick)
    {
        if (spellCollider == null) return;

        // Use ContactFilter2D to check only specified layers
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(hitLayers);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        // Get all overlapping colliders
        Collider2D[] results = new Collider2D[20]; // Max 20 targets
        int count = Physics2D.OverlapCollider(spellCollider, filter, results);

        // Add them to affectedTargets
        for (int i = 0; i < count; i++)
        {
            if (results[i] != null)
            {
                if (hitbox == null)
                    continue;

                bool canNegative = hitbox.IsNegativeTarget(results[i].gameObject);
                bool canPositive = hitbox.IsPositiveTarget(results[i].gameObject);
                if (!canNegative && !canPositive)
                    continue;

                affectedTargets.Add(results[i]);

                if (canNegative)
                    hitbox.ApplyOnHitEffects(results[i].gameObject, gameObject, owner);

                if (canPositive)
                    hitbox.ApplyBuffEffects(results[i].gameObject, gameObject, owner);

                bool hasNegativePayload = damage > 0f || hitbox.useWeaponDamage;
                bool hasPositivePayload = hitbox.positiveHealing > 0f;
                if (applyInitialTick && ((canNegative && hasNegativePayload) || (canPositive && hasPositivePayload)))
                    ApplyDamage(results[i]);
            }
        }
    }

    protected virtual void ApplyDamage(Collider2D target)
    {
        if (hitbox == null || target == null)
            return;

        bool canNegative = hitbox.IsNegativeTarget(target.gameObject);
        bool canPositive = hitbox.IsPositiveTarget(target.gameObject);
        if (!canNegative && !canPositive)
            return;

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;

        // Pass caster as attacker for thorns/reflect damage and stat/weapon lookups.
        GameObject attacker = casterTransform != null ? casterTransform.gameObject : gameObject;

        if (canNegative)
        {
            hitbox.ApplyDamage(target, attacker, attacker, owner ?? attacker, transform.position,
                parentConfig?.abilityName, parentConfig?.abilityTags?.GetAllTags(), parentConfig);

            if (dealsDamageOverTime && damagePerSecond > 0)
            {
                // Create new DoT effect object (allows multiple DoTs to stack)
                GameObject dotObject = new GameObject($"DoT_{damageTypeName}");
                dotObject.transform.SetParent(target.transform);
                DotEffect dotEffect = dotObject.AddComponent<DotEffect>();
                dotEffect.Initialize(damageable, gameObject, damageTypeName, damagePerSecond, dotInterval, dotDuration, dotParticleEffectPrefab, startParticlesFromFeet);
            }

            // Reusable knockback (radial — away from the area center) and pull (toward center).
            Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            hitbox.ApplyKnockback(target, gameObject, dir);
            hitbox.ApplyPull(target, transform.position);

            CreateHitEffect(target.transform.position);
        }

        if (canPositive)
        {
            hitbox.ApplyHealing(target, attacker, attacker, owner ?? attacker, transform.position,
                parentConfig?.abilityName, parentConfig?.abilityTags?.GetAllTags(), parentConfig);
            hitbox.ApplyBuffEffects(target.gameObject, gameObject, owner ?? attacker);
        }

    }

    protected virtual void CreateHitEffect(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        if (hitSound != null)
        {
            AudioManager.Instance.PlaySpatialSound(hitSound, position, 1f, Random.Range(0.9f, 1.1f));
        }

        // Centralized hit visual from AbilityDataConfig
        HitVisualHelper.SpawnHitVisual(parentConfig, position);
    }

    protected virtual void DestroySpell()
    {
        if (isDestroying) return;


        isDestroying = true;

        if (!destroyTriggersApplied)
        {
            hitbox?.OnDestroy(gameObject, owner ?? gameObject);
            destroyTriggersApplied = true;
        }

        // Disable collider
        if (spellCollider != null)
        {
            spellCollider.enabled = false;
        }

        // Detach and persist particle systems
        PersistParticleSystems();

        // Network-despawn if this is a FishNet-spawned NetworkObject; otherwise destroy locally.
        // Mirrors the pattern used by ChannelAbility.
        NetworkObject netObj = GetComponent<NetworkObject>();
        var nm = InstanceFinder.NetworkManager;
        if (netObj != null && netObj.IsSpawned && nm != null && nm.IsServerStarted)
            nm.ServerManager.Despawn(gameObject);
        else
            Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (!destroyTriggersApplied)
        {
            hitbox?.OnDestroy(gameObject, owner ?? gameObject);
            destroyTriggersApplied = true;
        }
    }

    private void PersistParticleSystems()
    {
        // Find all particle systems in children
        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps == null) continue;

            // Detach from parent so it won't be destroyed with the spell.
            // Reset to world scale 1 first — the parent transform scale was baked into
            // the particle shape; leaving a scaled transform after detach causes the
            // rendered particles to expand unexpectedly.
            ps.transform.SetParent(null);
            ps.transform.localScale = Vector3.one;

            // Stop emission but let existing particles finish their lifetime
            var emission = ps.emission;
            emission.enabled = false;

            // Calculate max lifetime to know when to destroy the system
            var main = ps.main;
            float maxLifetime = main.startLifetime.constantMax + main.startDelay.constantMax;

            // Destroy the particle system GameObject after all particles have died
            Destroy(ps.gameObject, maxLifetime + 1f);
        }
    }

    /// <summary>
    /// Set the alpha/opacity of all visual components (sprites, particles, glow)
    /// </summary>
    private void SetVisualAlpha(float alpha)
    {
        // Fade sprite renderer
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        // NOTE: Particle systems are intentionally excluded — overwriting startColor
        // collapses gradient/random modes and fights color-over-lifetime curves.
        // Particles handle their own fade via their authored modules.

        // Area indicator visuals were removed; only spell visuals are faded.
    }

    /// <summary>
    /// Particle shapes are authored on the prefab. This is a no-op kept for call-site compatibility.
    /// </summary>
    public void ConfigureParticles(AreaConfig config) { }
}

/// <summary>
/// Attached to a child collider object when AreaAbility lives on a parent.
/// Forwards Unity trigger callbacks up to the owning AreaAbility.
/// </summary>
public class AreaAbilityTriggerRelay : MonoBehaviour
{
    public AreaAbility Target;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Target != null) Target.OnChildTriggerEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (Target != null) Target.OnChildTriggerExit(other);
    }
}
