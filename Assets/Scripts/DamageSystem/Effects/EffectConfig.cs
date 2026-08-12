using UnityEngine;

/// <summary>
/// Base class for all effects (buffs, debuffs, DoTs, etc.)
/// </summary>
public abstract class EffectConfig : ScriptableObject
{
    [Header("Effect Identity")]
    [Tooltip("Display name for UI")]
    public string effectName = "Effect";
    
    [Tooltip("Internal unique ID")]
    public string effectID = "effect_default";
    
    [Tooltip("Is this a buff (positive) or debuff (negative)?")]
    public bool isBuff = true;
    
    [Header("Target Settings")]
    [Tooltip("Can be applied to allies?")]
    public bool canTargetAllies = false;
    
    [Tooltip("Can be applied to enemies?")]
    public bool canTargetEnemies = true;
    
    [Tooltip("Can be applied to self?")]
    public bool canTargetSelf = false;
    
    [Header("Duration")]
    [Tooltip("Duration of effect in seconds (0 = instant, -1 = permanent)")]
    public float duration = 5f;
    
    [Header("Stacking")]
    [Tooltip("How this effect behaves when reapplied")]
    public StackingBehavior stackingBehavior = StackingBehavior.Refresh;
    
    [Tooltip("Maximum number of stacks")]
    public int maxStacks = 1;
    
    [Tooltip("Refresh duration when adding a new stack")]
    public bool refreshDurationOnStack = true;
    
    [Tooltip("Maximum total duration (only for Extend behavior)")]
    public float maxDuration = 15f;
    
    [Header("Visual Effects")]
    [Tooltip("Particle effect to spawn on target")]
    public GameObject particleEffect;
    
    [Tooltip("Particle spawn offset")]
    public Vector3 particleOffset = Vector3.zero;
    
    [Tooltip("Color tint to apply to entity sprite")]
    public Color entityTint = Color.white;
    
    [Tooltip("UI icon")]
    public Sprite icon;
    
    [Header("Audio")]
    [Tooltip("Sound when effect is applied")]
    public AudioClip applySound;
    
    [Tooltip("Sound when effect expires")]
    public AudioClip expireSound;
    
    [Header("Gameplay")]
    [Tooltip("Can this effect be cleansed/dispelled?")]
    public bool canBeCleansed = true;
    
    [Tooltip("Priority for cleanse (higher = removed first)")]
    public int cleansePriority = 0;

    // Effect semantics live on the effect itself so gameplay code can ask
    // "what does this effect mean?" without hard-coding effect types elsewhere.
    public virtual bool IsStunned => false;
    public virtual bool IsRooted => false;
    public virtual bool IsSilenced => false;
    public virtual bool BlocksMovement => IsStunned || IsRooted;
    public virtual bool BlocksAbilityUsage => IsStunned || IsSilenced;
    public virtual float MovementSpeedMultiplier => 1f;
    public virtual bool GrantsInvulnerability => false;
    
    /// <summary>
    /// Called when effect is applied
    /// </summary>
    public abstract void OnApply(GameObject target, GameObject source);
    
    /// <summary>
    /// Called every frame while effect is active
    /// </summary>
    public abstract void OnUpdate(GameObject target, float deltaTime);
    
    /// <summary>
    /// Called when effect is removed
    /// </summary>
    public abstract void OnRemove(GameObject target);
    
    /// <summary>
    /// Can this effect be applied to the target?
    /// </summary>
    public bool CanTarget(GameObject target, GameObject source)
    {
        Debug.Log($"[EffectConfig] CanTarget check for {effectName}: target={target.name}, source={source.name}, canTargetSelf={canTargetSelf}, canTargetEnemies={canTargetEnemies}, canTargetAllies={canTargetAllies}");
        
        if (target == source)
        {
            Debug.Log($"[EffectConfig] Target is source, returning canTargetSelf={canTargetSelf}");
            return canTargetSelf;
        }
        
        // Check if ally or enemy (you'll need faction/team system)
        bool isAlly = IsAlly(target, source);
        Debug.Log($"[EffectConfig] IsAlly={isAlly}, target.tag={target.tag}, source.tag={source.tag}");
        
        if (isAlly)
        {
            Debug.Log($"[EffectConfig] Is ally, returning canTargetAllies={canTargetAllies}");
            return canTargetAllies;
        }
        else
        {
            Debug.Log($"[EffectConfig] Is enemy, returning canTargetEnemies={canTargetEnemies}");
            return canTargetEnemies;
        }
    }
    
    protected virtual bool IsAlly(GameObject target, GameObject source)
    {
        // If either has no tag, they should not be considered allies
        // Untagged entities are treated as hostile/neutral by default
        if (target.tag == "Untagged" || source.tag == "Untagged")
            return false;
        
        // Check for explicit ally tags
        if (target.CompareTag("Player") && source.CompareTag("Player"))
            return true;
        if (target.CompareTag("Ally") && source.CompareTag("Ally"))
            return true;
        
        // Otherwise check if tags match
        return target.CompareTag(source.tag);
    }
    
    /// <summary>
    /// Create a runtime copy with overridden duration
    /// </summary>
    public virtual EffectConfig WithDuration(float newDuration)
    {
        EffectConfig copy = Instantiate(this);
        copy.duration = newDuration;
        
        // Ensure targeting settings are preserved (Instantiate doesn't call OnEnable)
        copy.canTargetEnemies = this.canTargetEnemies;
        copy.canTargetAllies = this.canTargetAllies;
        copy.canTargetSelf = this.canTargetSelf;
        
        return copy;
    }
}
