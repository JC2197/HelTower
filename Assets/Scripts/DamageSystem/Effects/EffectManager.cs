using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet;
/// <summary>
/// Manages all active effects (buffs, debuffs, DoTs) on an entity.
/// Unified system for all temporary status effects.
/// </summary>
public class EffectManager : NetworkBehaviour
{
    public event System.Action OnActiveEffectsChanged;
    [Header("References")]
    public IDamageable damageable;

    [Header("Active Effects")]
    [SerializeField] private List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    private Dictionary<string, GameObject> activeParticles = new Dictionary<string, GameObject>();
    private EffectRegistry effectRegistry;

    private bool IsNetworkActive => InstanceFinder.NetworkManager != null && NetworkObject != null;

    void Awake()
    {
        effectRegistry = Resources.Load<EffectRegistry>("EffectRegistry");
        if (effectRegistry == null)
        {
            Debug.LogError("[EffectManager] EffectRegistry not found in Resources.");
        }
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
            if (effect.config is DebuffEffect debuff)
            {
                // Accumulate smooth damage
                effect.smoothDamageAccumulator += Time.deltaTime;

                // Apply damage smoothly every 0.1s for animations
                const float SMOOTH_INTERVAL = 0.1f;
                if (effect.smoothDamageAccumulator >= SMOOTH_INTERVAL)
                {
                    float damagePerSecond = debuff.damagePerTick / debuff.tickInterval;
                    float smoothDamage = damagePerSecond * effect.smoothDamageAccumulator * effect.currentStacks;
                    // Apply source attacker's damage-type bonus (e.g. BleedingDamageBonus)
                    float finalSmooth = DamageCalculator.CalculateFinalDamage(smoothDamage, debuff.damageTypeName, effect.source);

                    // Apply damage WITHOUT floater (silent damage for smooth HP bar animation)
                    if (damageable != null)
                    {
                        damageable.TakeDamage(finalSmooth, debuff.damageTypeName, suppressFloater: true);
                    }

                    effect.smoothDamageAccumulator = 0f;
                }

                // Handle tick interval for floaters and particles
                effect.tickTimer -= Time.deltaTime;
                if (effect.tickTimer <= 0f)
                {
                    // Display floater at tick interval
                    DisplayDamageFloater(debuff, effect);
                    effect.tickTimer = debuff.tickInterval;
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
        if (!config.CanTarget(gameObject, source)) return;

        ActiveEffect existingEffect =
            activeEffects.FirstOrDefault(e => e.config.effectID == config.effectID);

        if (existingEffect != null)
        {
            StackOrRefreshEffect(existingEffect, config);
            return;
        }

        ActiveEffect newEffect = new ActiveEffect(config, source);
        activeEffects.Add(newEffect);
        OnActiveEffectsChanged?.Invoke();

        if (config.applySound != null)
        {
            AudioManager.Instance.PlaySpatialSound(config.applySound, transform.position, 1f, Random.Range(0.9f, 1.1f));
        }

        string particleName = config.particleEffect != null ? config.particleEffect.name : "NULL";
        Debug.Log($"[MeleeAbility][KillTrace] ApplyEffect on {gameObject.name}: effectID={config.effectID}, isServerStarted={IsServerStarted}, isNetworkActive={IsNetworkActive}, particleEffect={particleName}");

        if (IsServerInitialized)
            ObserversRpcStartEffect(config.effectID); // tells remote clients

        if (!IsNetworkActive || IsServerInitialized)
            StartEffectVisualsLocal(config, source); // shows locally (offline or host)

    }

    [ObserversRpc(RunLocally = false)]
    private void ObserversRpcStartEffect(string effectID)
    {
        Debug.Log($"[MeleeAbility][KillTrace] ObserversRpcStartEffect received on {gameObject.name}: effectID={effectID}, registryFound={effectRegistry != null}");

        EffectConfig config = effectRegistry.Get(effectID);
        if (config == null)
        {
            Debug.LogWarning($"[MeleeAbility][KillTrace] effectRegistry.Get returned NULL for effectID={effectID} on {gameObject.name} — check EffectRegistry has this effectID registered.");
            return;
        }

        StartEffectVisualsLocal(config, null);
    }

    /// <summary>
    /// Spawns the effect's particle (sized/sorted to fill the target's sprite) and runs the
    /// config's apply hook (e.g. burning tint). Runs on every machine that should see the effect.
    /// </summary>
    private void StartEffectVisualsLocal(EffectConfig config, GameObject source)
    {
        config.OnApply(gameObject, source);

        string particleName = config.particleEffect != null ? config.particleEffect.name : "NULL";
        Debug.Log($"[MeleeAbility][KillTrace] StartEffectVisualsLocal on {gameObject.name}: effectID={config.effectID}, particleEffect={particleName}, alreadyActive={activeParticles.ContainsKey(config.effectID)}");

        if (config.particleEffect == null || activeParticles.ContainsKey(config.effectID))
            return;

        SpriteRenderer targetSprite = GetComponent<SpriteRenderer>();
        if (targetSprite == null)
            targetSprite = GetComponentInParent<SpriteRenderer>();
        if (targetSprite == null)
            targetSprite = GetComponentInChildren<SpriteRenderer>(true);

        if (targetSprite == null || targetSprite.sprite == null)
        {
            Debug.Log($"[EffectManager] Skipping particle effect '{config.effectID}' on '{name}': target has no valid SpriteRenderer.");
            return;
        }

        GameObject particles = HitVisualHelper.SpawnEffect(
            config.particleEffect, transform.position, Quaternion.identity,
            parent: transform, localOffset: config.particleOffset,
            autoDestroy: false, spriteSizeTarget: targetSprite);
        activeParticles[config.effectID] = particles;
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

    public void ConsumeEffectStack(string effectID)
    {
        ActiveEffect effect = activeEffects.FirstOrDefault(e => e.config.effectID == effectID);
        if (effect == null)
            return;

        int oldStacks = effect.currentStacks;
        effect.currentStacks--;
        if (effect.currentStacks <= 0)
        {
            RemoveEffect(effect);
            return;
        }

        effect.config.OnStackChanged(gameObject, oldStacks, effect.currentStacks);
        OnActiveEffectsChanged?.Invoke();
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
        int oldStacks = existingEffect.currentStacks;

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

        if (existingEffect.currentStacks != oldStacks)
            existingEffect.config.OnStackChanged(gameObject, oldStacks, existingEffect.currentStacks);

        OnActiveEffectsChanged?.Invoke();
    }

    private void DisplayDamageFloater(DebuffEffect debuff, ActiveEffect effect)
    {
        // Only display floater and particles, damage is already being applied smoothly
        if (damageable != null)
        {
            float rawTick = debuff.damagePerTick * effect.currentStacks;
            float displayDamage = DamageCalculator.CalculateFinalDamage(rawTick, debuff.damageTypeName, effect.source);
            DamageTypeData damageType = debuff.GetDamageType();

            Debug.Log($"[EffectManager] Showing debuff floater: {displayDamage} damage (interval: {debuff.tickInterval}s, stacks: {effect.currentStacks})");

            // Show floater with the tick damage amount (even though damage was applied smoothly)
            if (damageable is IDamageFloaterSource floaterSource)
            {
                floaterSource.ShowDamageFloater(displayDamage, damageType != null ? damageType.damageTypeName : "Physical");
            }

            // Notify the DoT config that a damage tick occurred (for particles)
            debuff.OnDamageTick(gameObject, displayDamage);
        }
    }

    private void RemoveEffect(ActiveEffect effect)
    {
        if (!activeEffects.Contains(effect)) return;

        string effectID = effect.config.effectID;
        activeEffects.Remove(effect);
        OnActiveEffectsChanged?.Invoke();

        if (IsServerInitialized)
            ObserversRpcStopEffect(effectID); // tells remote clients

        if (!IsNetworkActive || IsServerInitialized)
        {
            // Unwind through the instance that applied the effect. A registry lookup returns the
            // shared asset, whose per-target deltas are empty, so stat buffs would never reverse.
            effect.config.OnRemove(gameObject);
            DestroyEffectParticles(effectID);
        }

        if (effect.config.expireSound != null)
        {
            AudioManager.Instance.PlaySpatialSound(effect.config.expireSound, transform.position, 1f, Random.Range(0.9f, 1.1f));
        }

        Debug.Log($"Removed {effect.config.effectName} from {gameObject.name}");
    }

    [ObserversRpc(RunLocally = false)]
    private void ObserversRpcStopEffect(string effectID)
    {
        StopEffectVisualsLocal(effectID);
    }

    /// <summary>
    /// Remote-client teardown: only the effect ID crosses the wire, so the shared asset is the
    /// best available handle. Local/server teardown goes through <see cref="RemoveEffect"/>.
    /// </summary>
    private void StopEffectVisualsLocal(string effectID)
    {
        EffectConfig config = effectRegistry != null ? effectRegistry.Get(effectID) : null;
        config?.OnRemove(gameObject);
        DestroyEffectParticles(effectID);
    }

    private void DestroyEffectParticles(string effectID)
    {
        if (activeParticles.TryGetValue(effectID, out GameObject particles))
        {
            if (particles != null) Destroy(particles);
            activeParticles.Remove(effectID);
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

            if (config is DebuffEffect debuff)
            {
                this.tickTimer = debuff.tickInterval;
            }
        }
    }

    public IReadOnlyList<ActiveEffect> ActiveEffects => activeEffects;
}
