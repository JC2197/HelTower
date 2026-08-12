using UnityEngine;

/// <summary>
/// Represents an active damage over time effect on an entity
/// </summary>
[System.Serializable]
public class DamageOverTimeEffect
{
    public DamageOverTimeConfig config;
    public float remainingDuration;
    public int currentStacks;
    public GameObject source;
    public float tickTimer;
    
    public DamageOverTimeEffect(DamageOverTimeConfig config, GameObject source)
    {
        this.config = config;
        this.source = source;
        this.remainingDuration = config.duration;
        this.currentStacks = 1;
        this.tickTimer = config.tickInterval;
    }
    
    public bool Update(float deltaTime, System.Action<float, DamageTypeData, GameObject> onDamageTick)
    {
        remainingDuration -= deltaTime;
        
        if (remainingDuration <= 0f)
        {
            return true;
        }
        
        tickTimer -= deltaTime;
        if (tickTimer <= 0f)
        {
            float damageThisTick = config.damagePerTick * currentStacks;
            DamageTypeData damageType = config.GetDamageType();
            onDamageTick?.Invoke(damageThisTick, damageType, source);
            tickTimer = config.tickInterval;
        }
        
        return false;
    }
    
    public void StackOrRefresh(DamageOverTimeConfig newConfig)
    {
        switch (newConfig.stackingBehavior)
        {
            case StackingBehavior.Stack:
                if (currentStacks < newConfig.maxStacks)
                {
                    currentStacks++;
                }
                if (newConfig.refreshDurationOnStack)
                {
                    remainingDuration = newConfig.duration;
                }
                break;
                
            case StackingBehavior.Refresh:
                remainingDuration = newConfig.duration;
                currentStacks = 1;
                break;
                
            case StackingBehavior.Extend:
                remainingDuration += newConfig.duration;
                remainingDuration = Mathf.Min(remainingDuration, newConfig.maxDuration);
                break;
                
            case StackingBehavior.KeepLongest:
                if (newConfig.duration > remainingDuration)
                {
                    remainingDuration = newConfig.duration;
                }
                break;
        }
    }
    
    public float GetTotalDamagePerSecond()
    {
        return (config.damagePerTick / config.tickInterval) * currentStacks;
    }
}
