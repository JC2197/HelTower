using UnityEngine;

/// <summary>
/// Base class for all Damage Over Time effects.
/// Provides common functionality for Bleed, Poison, Burning, etc.
/// Extend this class to create specific DoT effects with custom visuals and behaviors.
/// Damage amount and duration are configured per-ability, not in the asset.
/// </summary>
public abstract class DamageOverTimeConfig : EffectConfig
{
    [Header("Damage Settings")]
    [Tooltip("Type of damage dealt (from DamageTypeDatabase)")]
    [DamageTypeDropdown]
    public string damageTypeName = "Physical";

    [Tooltip("Damage dealt per tick (can be overridden per-ability)")]
    public float damagePerTick = 5f;

    [Tooltip("Time between damage ticks (seconds)")]
    public float tickInterval = 1f;

    public DamageTypeData GetDamageType()
    {
        return DamageTypeDatabase.Instance?.GetDamageType(damageTypeName);
    }

    /// <summary>
    /// Calculate total damage over the full duration
    /// </summary>
    public float GetTotalDamage()
    {
        float totalTicks = Mathf.Floor(duration / tickInterval);
        return damagePerTick * totalTicks;
    }

    /// <summary>
    /// Calculate damage per second
    /// </summary>
    public float GetDamagePerSecond()
    {
        return damagePerTick / tickInterval;
    }

    
    public override void OnApply(GameObject target, GameObject source)
    {
        OnDotApplied(target, source);
    }

    public override void OnUpdate(GameObject target, float deltaTime)
    {
        OnDotUpdate(target, deltaTime);
    }

    public override void OnRemove(GameObject target)
    {
        OnDotRemoved(target);
    }

    /// <summary>
    /// Called when a damage tick is applied (from EffectManager) for displaying floater
    /// </summary>
    public virtual void OnDamageTick(GameObject target, float damage)
    {
        // Override in child classes if needed for custom tick behavior
    }

    /// <summary>
    /// Override this for custom behavior when DoT is first applied
    /// </summary>
    protected virtual void OnDotApplied(GameObject target, GameObject source)
    {
        Debug.Log($"{target.name} is affected by {effectName}! {damagePerTick} {damageTypeName} damage every {tickInterval}s for {duration}s");
    }

    /// <summary>
    /// Override this for custom per-frame update behavior
    /// </summary>
    protected virtual void OnDotUpdate(GameObject target, float deltaTime)
    {
        // Override in child classes for custom behavior
    }

    /// <summary>
    /// Override this for custom behavior when DoT expires
    /// </summary>
    protected virtual void OnDotRemoved(GameObject target)
    {
        Debug.Log($"{target.name} is no longer affected by {effectName}");
    }
    
    /// <summary>
    /// Create a runtime copy with overridden damage and duration
    /// </summary>
    public virtual DamageOverTimeConfig WithDamageAndDuration(float newDamage, float newDuration)
    {
        Debug.Log($"[DamageOverTimeConfig] BEFORE copy - Original {effectName}: canTargetEnemies={this.canTargetEnemies}, canTargetAllies={this.canTargetAllies}, canTargetSelf={this.canTargetSelf}");
        
        DamageOverTimeConfig copy = Instantiate(this);
        copy.damagePerTick = newDamage;
        copy.duration = newDuration;
        
        // Ensure targeting settings are preserved (Instantiate doesn't call OnEnable)
        copy.canTargetEnemies = this.canTargetEnemies;
        copy.canTargetAllies = this.canTargetAllies;
        copy.canTargetSelf = this.canTargetSelf;
        
        Debug.Log($"[DamageOverTimeConfig] AFTER copy - Copy {copy.effectName}: canTargetEnemies={copy.canTargetEnemies}, canTargetAllies={copy.canTargetAllies}, canTargetSelf={copy.canTargetSelf}");
        Debug.Log($"[DamageOverTimeConfig] WithDamageAndDuration called for {effectName}. NewDamage={newDamage}, NewDuration={newDuration}. Copy damage={copy.damagePerTick}, duration={copy.duration}");
        return copy;
    }
}
