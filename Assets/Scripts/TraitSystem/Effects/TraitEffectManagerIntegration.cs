using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Example TraitEffect that integrates with the EffectManager system.
/// This shows how traits can grant buffs/effects when activated.
/// </summary>
[CreateAssetMenu(fileName = "Effect_TraitBuff", menuName = "Traits/Trait Buff Effect")]
public class TraitBuffEffect : TraitEffect
{
    [Header("Effects to Apply")]
    [Tooltip("Effects that are applied when this trait is activated")]
    public List<EffectConfig> effectsToApply = new List<EffectConfig>();
    
    [Header("Application")]
    [Tooltip("Apply effects once on activation, or continuously maintain them?")]
    public bool maintainEffects = true;
    
    private EffectManager effectManager;
    private List<string> appliedEffectIDs = new List<string>();
    
    public override void OnActivate()
    {
        effectManager = character.GetComponent<EffectManager>();
        if (effectManager == null)
        {
            Debug.LogWarning($"No EffectManager on {character.name} for trait {traitData.displayName}");
            return;
        }
        
        // Apply all effects
        foreach (var effect in effectsToApply)
        {
            if (effect != null)
            {
                effectManager.ApplyEffect(effect, character);
                
                if (maintainEffects)
                {
                    appliedEffectIDs.Add(effect.effectID);
                }
            }
        }
        
        Debug.Log($"Trait {traitData.displayName} applied {effectsToApply.Count} effects");
    }
    
    public override void OnDeactivate()
    {
        // Remove all effects that were applied by this trait
        if (effectManager != null && maintainEffects)
        {
            foreach (var effectID in appliedEffectIDs)
            {
                effectManager.RemoveEffect(effectID);
            }
        }
        
        appliedEffectIDs.Clear();
    }
    
    public override void Update()
    {
        // If maintaining effects, reapply if they've expired
        if (maintainEffects && effectManager != null)
        {
            foreach (var effect in effectsToApply)
            {
                if (effect != null && !effectManager.HasEffect(effect.effectID))
                {
                    effectManager.ApplyEffect(effect, character);
                }
            }
        }
    }
}

/// <summary>
/// Example: Trait that grants temporary buffs on specific triggers
/// Shows conditional effect application
/// </summary>
[CreateAssetMenu(fileName = "Effect_ConditionalBuff", menuName = "Traits/Conditional Buff Effect")]
public class ConditionalTraitBuffEffect : TraitEffect
{
    [Header("Trigger Settings")]
    [Tooltip("Effect to apply when trigger occurs")]
    public EffectConfig buffToApply;
    
    [Tooltip("Trigger condition")]
    public TriggerCondition condition = TriggerCondition.OnLowHealth;
    
    [Tooltip("Health threshold for OnLowHealth trigger")]
    [Range(0f, 1f)]
    public float healthThreshold = 0.3f;
    
    [Tooltip("Cooldown between trigger activations")]
    public float triggerCooldown = 10f;
    
    private EffectManager effectManager;
    private IDamageable damageable;
    private float lastTriggerTime = -999f;
    
    public override void OnActivate()
    {
        effectManager = character.GetComponent<EffectManager>();
        damageable = character.GetComponent<IDamageable>();
        
        if (effectManager == null || damageable == null)
        {
            Debug.LogWarning($"Missing components for conditional trait on {character.name}");
        }
    }
    
    public override void OnDeactivate()
    {
        // Cleanup if needed
    }
    
    public override void Update()
    {
        if (effectManager == null || damageable == null)
            return;
        
        // Check if we're on cooldown
        if (Time.time < lastTriggerTime + triggerCooldown)
            return;
        
        bool shouldTrigger = false;
        
        switch (condition)
        {
            case TriggerCondition.OnLowHealth:
                float healthPercent = character.GetHealthPercent();
                shouldTrigger = healthPercent <= healthThreshold;
                break;
            
            case TriggerCondition.OnFullHealth:
                shouldTrigger = character.IsAtFullHealth();
                break;
            
            // Add more conditions as needed
        }
        
        if (shouldTrigger && buffToApply != null)
        {
            effectManager.ApplyEffect(buffToApply, character);
            lastTriggerTime = Time.time;
            Debug.Log($"Conditional trait triggered: {traitData.displayName}");
        }
    }
}

public enum TriggerCondition
{
    OnLowHealth,
    OnFullHealth,
    OnKill,
    OnDamageTaken,
    OnAbilityUse
}

/// <summary>
/// Example: Trait that modifies how effects work on the character
/// Shows integration with EffectManager's stat modifier system
/// </summary>
[CreateAssetMenu(fileName = "Effect_EffectAmplifier", menuName = "Traits/Effect Amplifier")]
public class EffectAmplifierTraitEffect : TraitEffect
{
    [Header("Amplification Settings")]
    [Tooltip("Multiply buff durations by this amount")]
    public float buffDurationMultiplier = 1.5f;
    
    [Tooltip("Multiply buff effectiveness by this amount")]
    public float buffEffectivenessMultiplier = 1.25f;
    
    [Tooltip("Reduce debuff durations by this amount")]
    public float debuffDurationReduction = 0.5f;
    
    // Note: This would require modifications to EffectManager to support
    // You could implement this by:
    // 1. Adding a custom component that intercepts ApplyEffect calls
    // 2. Modifying effect durations/values before they're applied
    // 3. Or exposing modifier hooks in EffectManager
    
    public override void OnActivate()
    {
        var amplifier = character.GetComponent<EffectAmplifierComponent>();
        if (amplifier == null)
        {
            amplifier = character.AddComponent<EffectAmplifierComponent>();
        }
        
        amplifier.buffDurationMultiplier = buffDurationMultiplier;
        amplifier.buffEffectivenessMultiplier = buffEffectivenessMultiplier;
        amplifier.debuffDurationReduction = debuffDurationReduction;
    }
    
    public override void OnDeactivate()
    {
        var amplifier = character.GetComponent<EffectAmplifierComponent>();
        if (amplifier != null)
        {
            Object.Destroy(amplifier);
        }
    }
}

/// <summary>
/// Component that modifies effect application
/// </summary>
public class EffectAmplifierComponent : MonoBehaviour
{
    public float buffDurationMultiplier = 1f;
    public float buffEffectivenessMultiplier = 1f;
    public float debuffDurationReduction = 1f;
    
    // This could intercept effect applications and modify them
    // Implementation depends on how you want to hook into EffectManager
}
