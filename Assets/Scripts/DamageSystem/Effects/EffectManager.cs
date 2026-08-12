using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages all active effects (buffs, debuffs, DoTs) on an entity.
/// Unified system for all temporary status effects.
/// </summary>
public class EffectManager : MonoBehaviour
{
    [Header("References")]
    public IDamageable damageable;

    [Header("Active Effects")]
    [SerializeField] private List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    private Dictionary<string, GameObject> activeParticles = new Dictionary<string, GameObject>();

    void Awake()
    {
        if (damageable == null)
        {
            damageable = GetComponent<IDamageable>();

        }
        if (damageable == null)
        {
            damageable = GetComponentInParent<IDamageable>();
        }
        Debug.Log($"[EffectManager] Awake on {gameObject.name}. damageable found? {damageable != null}");

        // Find the icon display in the health bar
        Debug.Log($"[EffectManager] Awake on {gameObject.name}. Looking for WorldHealthBar...");
        WorldHealthBar healthBar = GetComponentInChildren<WorldHealthBar>();
        if (healthBar != null)
        {
            Debug.Log($"[EffectManager] Found WorldHealthBar on {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[EffectManager] No WorldHealthBar found in children of {gameObject.name}");
        }
    }

    void Update()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = activeEffects[i];

            // Update effect
            effect.config.OnUpdate(gameObject, Time.deltaTime);

            // Handle DoT ticking with smooth damage accumulation
            if (effect.config is DamageOverTimeConfig dotConfig)
            {
                // Accumulate smooth damage
                effect.smoothDamageAccumulator += Time.deltaTime;

                // Apply damage smoothly every 0.1s for animations
                const float SMOOTH_INTERVAL = 0.1f;
                if (effect.smoothDamageAccumulator >= SMOOTH_INTERVAL)
                {
                    float damagePerSecond = dotConfig.damagePerTick / dotConfig.tickInterval;
                    float smoothDamage = damagePerSecond * effect.smoothDamageAccumulator * effect.currentStacks;
                    // Apply source attacker's damage-type bonus (e.g. BleedingDamageBonus)
                    float finalSmooth = DamageCalculator.CalculateFinalDamage(smoothDamage, dotConfig.damageTypeName, effect.source);

                    // Apply damage WITHOUT floater (silent damage for smooth HP bar animation)
                    if (damageable != null)
                    {
                        damageable.TakeDamage(finalSmooth, dotConfig.damageTypeName, suppressFloater: true);
                    }

                    effect.smoothDamageAccumulator = 0f;
                }

                // Handle tick interval for floaters and particles
                effect.tickTimer -= Time.deltaTime;
                if (effect.tickTimer <= 0f)
                {
                    // Display floater at tick interval
                    DisplayDamageFloater(dotConfig, effect);
                    effect.tickTimer = dotConfig.tickInterval;
                }
            }

            // Update duration
            if (effect.config.duration > 0)
            {
                effect.remainingDuration -= Time.deltaTime;

                if (effect.remainingDuration <= 0f)
                {
                    RemoveEffect(effect);
                }
            }
        }
    }

    /// <summary>
    /// Applies an effect (buff, debuff, DoT) to this entity
    /// </summary>
    public void ApplyEffect(EffectConfig config, GameObject source)
    {
        if (config == null) return;

        Debug.Log($"[EffectManager] ApplyEffect called: {config.effectName} on {gameObject.name}. Is DoT? {config is DamageOverTimeConfig}");

        // Check if target is valid
        if (!config.CanTarget(gameObject, source))
        {
            Debug.Log($"Cannot apply {config.effectName} - invalid target");
            return;
        }

        // Check for existing effect
        ActiveEffect existingEffect = activeEffects.FirstOrDefault(e => e.config.effectID == config.effectID);

        if (existingEffect != null)
        {
            StackOrRefreshEffect(existingEffect, config);
        }
        else
        {
            ActiveEffect newEffect = new ActiveEffect(config, source);
            activeEffects.Add(newEffect);

            // Debug logging for DoT effects

            SpawnVisualEffects(config);
            config.OnApply(gameObject, source);
            if (config.applySound != null)
            {
                AudioManager.Instance.PlaySpatialSound(config.applySound, transform.position, 1f, Random.Range(0.9f, 1.1f));
            }

            Debug.Log($"Applied {config.effectName} to {gameObject.name}");
        }
    }

    /// <summary>
    /// Removes a specific effect by ID
    /// </summary>
    public void RemoveEffect(string effectID)
    {
        ActiveEffect effect = activeEffects.FirstOrDefault(e => e.config.effectID == effectID);
        if (effect != null)
        {
            RemoveEffect(effect);
        }
    }

    /// <summary>
    /// Removes all effects that can be cleansed (prioritized by cleanse priority)
    /// </summary>
    public void Cleanse(int count = -1)
    {
        List<ActiveEffect> cleansableEffects = activeEffects
            .Where(e => e.config.canBeCleansed)
            .OrderByDescending(e => e.config.cleansePriority)
            .ToList();

        int removed = 0;
        foreach (var effect in cleansableEffects)
        {
            if (count > 0 && removed >= count) break;

            RemoveEffect(effect);
            removed++;
        }

        if (removed > 0)
        {
            Debug.Log($"Cleansed {removed} effects from {gameObject.name}");
        }
    }

    /// <summary>
    /// Removes all buffs
    /// </summary>
    public void RemoveAllBuffs()
    {
        List<ActiveEffect> buffs = activeEffects.Where(e => e.config.isBuff).ToList();
        foreach (var buff in buffs)
        {
            RemoveEffect(buff);
        }
    }

    /// <summary>
    /// Removes all debuffs
    /// </summary>
    public void RemoveAllDebuffs()
    {
        List<ActiveEffect> debuffs = activeEffects.Where(e => !e.config.isBuff).ToList();
        foreach (var debuff in debuffs)
        {
            RemoveEffect(debuff);
        }
    }

    /// <summary>
    /// Checks if entity has a specific effect active
    /// </summary>
    public bool HasEffect(string effectID)
    {
        return activeEffects.Any(e => e.config.effectID == effectID);
    }

    /// <summary>
    /// Gets a specific active effect
    /// </summary>
    public ActiveEffect GetEffect(string effectID)
    {
        return activeEffects.FirstOrDefault(e => e.config.effectID == effectID);
    }

    /// <summary>
    /// Gets all active effects
    /// </summary>
    public List<ActiveEffect> GetActiveEffects()
    {
        return new List<ActiveEffect>(activeEffects);
    }

    /// <summary>
    /// Gets all active buffs
    /// </summary>
    public List<ActiveEffect> GetActiveBuffs()
    {
        return activeEffects.Where(e => e.config.isBuff).ToList();
    }

    /// <summary>
    /// Gets all active debuffs
    /// </summary>
    public List<ActiveEffect> GetActiveDebuffs()
    {
        return activeEffects.Where(e => !e.config.isBuff).ToList();
    }

    /// <summary>
    /// Gets total stat modifier from all active buffs/debuffs
    /// </summary>
    // public float GetTotalStatModifier(string statID, out float additive, out float multiplicative)
    // {
    //     additive = 0f;
    //     multiplicative = 1f;

    //     foreach (var effect in activeEffects)
    //     {
    //         if (effect.config is StatBuffConfig statBuff)
    //         {
    //             ModifierType modType;
    //             float value = statBuff.GetStatModifier(statID, out modType);

    //             switch (modType)
    //             {
    //                 case ModifierType.Flat:
    //                     additive += value * effect.currentStacks;
    //                     break;
    //                 case ModifierType.Percentage:
    //                     multiplicative *= (1f + value) * effect.currentStacks;
    //                     break;
    //             }
    //         }
    //     }

    //     return additive + multiplicative;
    // }

    /// <summary>
    /// Returns true when any active effect blocks movement.
    /// </summary>
    public bool HasAnyMovementBlockingEffect()
    {
        return activeEffects.Any(e => e.config != null && e.config.BlocksMovement);
    }

    /// <summary>
    /// Returns true when any active effect blocks ability usage.
    /// </summary>
    public bool HasAnyAbilityBlockingEffect()
    {
        return activeEffects.Any(e => e.config != null && e.config.BlocksAbilityUsage);
    }

    /// <summary>
    /// Returns the first active effect which blocks ability usage.
    /// Useful when gameplay needs to explain why an action is blocked.
    /// </summary>
    public EffectConfig GetFirstAbilityBlockingEffect()
    {
        ActiveEffect activeEffect = activeEffects.FirstOrDefault(e => e.config != null && e.config.BlocksAbilityUsage);
        return activeEffect?.config;
    }

    /// <summary>
    /// Returns movement speed multiplier from active slows/buffs.
    /// Uses the strongest movement penalty currently active.
    /// </summary>
    public float GetMovementSpeedMultiplier()
    {
        float multiplier = 1f;

        foreach (ActiveEffect effect in activeEffects)
        {
            if (effect.config == null) continue;

            multiplier = Mathf.Min(multiplier, Mathf.Clamp01(effect.config.MovementSpeedMultiplier));
        }

        return Mathf.Clamp01(multiplier);
    }

    /// <summary>
    /// Checks if currently invulnerable
    /// </summary>
    public bool IsInvulnerable()
    {
        return activeEffects.Any(e => e.config != null && e.config.GrantsInvulnerability);
    }

    private void StackOrRefreshEffect(ActiveEffect existingEffect, EffectConfig newConfig)
    {
        switch (newConfig.stackingBehavior)
        {
            case StackingBehavior.Stack:
                if (existingEffect.currentStacks < newConfig.maxStacks)
                {
                    existingEffect.currentStacks++;
                }
                if (newConfig.refreshDurationOnStack && newConfig.duration > 0)
                {
                    existingEffect.remainingDuration = newConfig.duration;
                }
                break;

            case StackingBehavior.Refresh:
                if (newConfig.duration > 0)
                {
                    existingEffect.remainingDuration = newConfig.duration;
                }
                existingEffect.currentStacks = 1;
                break;

            case StackingBehavior.Extend:
                if (newConfig.duration > 0)
                {
                    existingEffect.remainingDuration += newConfig.duration;
                    existingEffect.remainingDuration = Mathf.Min(existingEffect.remainingDuration, newConfig.maxDuration);
                }
                break;

            case StackingBehavior.KeepLongest:
                if (newConfig.duration > existingEffect.remainingDuration)
                {
                    existingEffect.remainingDuration = newConfig.duration;
                }
                break;
        }
    }

    private void DisplayDamageFloater(DamageOverTimeConfig dotConfig, ActiveEffect effect)
    {
        // Only display floater and particles, damage is already being applied smoothly
        if (damageable != null)
        {
            float rawTick = dotConfig.damagePerTick * effect.currentStacks;
            float displayDamage = DamageCalculator.CalculateFinalDamage(rawTick, dotConfig.damageTypeName, effect.source);
            DamageTypeData damageType = dotConfig.GetDamageType();

            Debug.Log($"[EffectManager] Showing DoT floater: {displayDamage} damage (interval: {dotConfig.tickInterval}s, stacks: {effect.currentStacks})");

            // Show floater with the tick damage amount (even though damage was applied smoothly)
            if (damageable is IDamageFloaterSource floaterSource)
            {
                floaterSource.ShowDamageFloater(displayDamage, damageType != null ? damageType.damageTypeName : "Physical");
            }

            // Notify the DoT config that a damage tick occurred (for particles)
            dotConfig.OnDamageTick(gameObject, displayDamage);
        }
    }

    private void RemoveEffect(ActiveEffect effect)
    {
        if (!activeEffects.Contains(effect)) return;

        effect.config.OnRemove(gameObject);
        activeEffects.Remove(effect);

        if (activeParticles.ContainsKey(effect.config.effectID))
        {
            Destroy(activeParticles[effect.config.effectID]);
            activeParticles.Remove(effect.config.effectID);
        }

        if (effect.config.expireSound != null)
        {
            AudioManager.Instance.PlaySpatialSound(effect.config.expireSound, transform.position, 1f, Random.Range(0.9f, 1.1f));
        }

        Debug.Log($"Removed {effect.config.effectName} from {gameObject.name}");
    }

    private void SpawnVisualEffects(EffectConfig config)
    {
        if (config.particleEffect != null && !activeParticles.ContainsKey(config.effectID))
        {
            GameObject particles = Instantiate(config.particleEffect, transform);
            particles.transform.localPosition = config.particleOffset;

            // Get the target's sprite renderer and collider for sizing
            SpriteRenderer targetRenderer = GetComponent<SpriteRenderer>();
            Collider2D targetCollider = GetComponent<Collider2D>();
            int targetSortingOrder = targetRenderer != null ? targetRenderer.sortingOrder : 0;
            string targetSortingLayer = targetRenderer != null ? targetRenderer.sortingLayerName : "Default";

            // Calculate bounds based on sprite or collider
            Vector3 targetBounds = Vector3.one;
            if (targetRenderer != null && targetRenderer.sprite != null)
            {
                // Use sprite bounds
                Bounds spriteBounds = targetRenderer.bounds;
                targetBounds = new Vector3(spriteBounds.size.x, spriteBounds.size.y, spriteBounds.size.z);
            }
            else if (targetCollider != null)
            {
                // Fallback to collider bounds
                Bounds colliderBounds = targetCollider.bounds;
                targetBounds = new Vector3(colliderBounds.size.x, colliderBounds.size.y, colliderBounds.size.z);
            }

            // Set sorting order and shape bounds for ALL particle systems in the hierarchy (including children)
            ParticleSystem[] allParticleSystems = particles.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in allParticleSystems)
            {
                // Adjust shape module to match target bounds
                var shape = ps.shape;
                if (shape.enabled)
                {
                    if (shape.shapeType == ParticleSystemShapeType.SingleSidedEdge)
                    {
                        shape.scale = new Vector3(targetBounds.x, targetBounds.y, 1f);
                    }
                    else
                    {
                        shape.shapeType = ParticleSystemShapeType.Sprite;
                        if (targetRenderer != null && targetRenderer.sprite != null)
                        {
                            shape.sprite = targetRenderer.sprite;
                        }
                    }
                }

                ps.Play();
            }

            activeParticles[config.effectID] = particles;
        }
    }

    void OnDestroy()
    {
        foreach (var particles in activeParticles.Values)
        {
            if (particles != null) Destroy(particles);
        }
        activeParticles.Clear();
    }

    /// <summary>
    /// Runtime instance of an active effect
    /// </summary>
    [System.Serializable]
    public class ActiveEffect
    {
        public EffectConfig config;
        public GameObject source;
        public float remainingDuration;
        public int currentStacks;
        public float tickTimer; // For DoT effects
        public float smoothDamageAccumulator; // For smooth damage application between ticks

        public ActiveEffect(EffectConfig config, GameObject source)
        {
            this.config = config;
            this.source = source;
            this.remainingDuration = config.duration;
            this.currentStacks = 1;

            if (config is DamageOverTimeConfig dotConfig)
            {
                this.tickTimer = dotConfig.tickInterval;
            }
        }
    }
}
