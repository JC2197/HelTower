using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Context object containing all information about a damage event.
/// Passed through the damage pipeline to allow trait effects to modify or react to damage.
/// </summary>
public class DamageContext
{
    /// <summary>
    /// The GameObject dealing the damage (e.g., player)
    /// </summary>
    public GameObject Attacker { get; set; }
    
    /// <summary>
    /// The GameObject receiving the damage (e.g., enemy)
    /// </summary>
    public GameObject Target { get; set; }
    
    /// <summary>
    /// The IDamageable component on the target
    /// </summary>
    public IDamageable Damageable { get; set; }
    
    /// <summary>
    /// Name of the ability dealing damage
    /// </summary>
    public string AbilityName { get; set; }
    
    /// <summary>
    /// Tags of the ability (e.g., "Projectile", "Fire", "Area")
    /// </summary>
    public List<string> AbilityTags { get; set; }
    
    /// <summary>
    /// Base damage before any modifications
    /// </summary>
    public float BaseDamage { get; set; }
    
    /// <summary>
    /// Final damage after all modifications (updated by processor)
    /// </summary>
    public float FinalDamage { get; set; }
    
    /// <summary>
    /// Type of damage (Fire, Lightning, Physical, etc.)
    /// </summary>
    public string DamageType { get; set; }
    
    /// <summary>
    /// Position where the hit occurred
    /// </summary>
    public Vector3 HitPosition { get; set; }
    
    /// <summary>
    /// Critical hit multiplier (1.0 = no crit)
    /// </summary>
    public float CritMultiplier { get; set; } = 1f;
    
    /// <summary>
    /// Whether this was a critical hit
    /// </summary>
    public bool IsCritical => CritMultiplier > 1f;
    
    /// <summary>
    /// Flash color for hit feedback
    /// </summary>
    public Color HitFlashColor { get; set; } = Color.white;
    
    /// <summary>
    /// Additional damage instances to apply (from AddDamage effects)
    /// </summary>
    public List<AdditionalDamage> AdditionalDamages { get; private set; } = new List<AdditionalDamage>();
    
    /// <summary>
    /// Status effects to apply after damage (from ApplyStatusEffect effects)
    /// </summary>
    public List<PendingStatusEffect> PendingStatusEffects { get; private set; } = new List<PendingStatusEffect>();
    
    /// <summary>
    /// Total healing to apply to attacker (from OnHitHeal effects)
    /// </summary>
    public float AttackerHealing { get; set; } = 0f;
    
    /// <summary>
    /// Resources to grant to attacker (from OnHitGainResource effects)
    /// </summary>
    public Dictionary<string, float> AttackerResources { get; private set; } = new Dictionary<string, float>();
    
    /// <summary>
    /// Fires after the full damage pipeline (main hit + all queued extra damages) resolves
    /// on a critical hit.  Subscribe to react to crits: spawn VFX, proc on-crit traits,
    /// grant resources, etc.
    /// </summary>
    public static event System.Action<DamageContext> OnCriticalHit;

    /// <summary>
    /// Raises the OnCriticalHit event when IsCritical.
    /// </summary>
    internal static void RaiseOnCriticalHit(DamageContext context)
    {
        Debug.Log($"[DamageContext] ⚡ CRITICAL HIT — attacker='{context.Attacker?.name}' " +
                  $"target='{context.Target?.name}' " +
                  $"type={context.DamageType} " +
                  $"base={context.BaseDamage:F1} final={context.FinalDamage:F1} " +
                  $"critMult={context.CritMultiplier:F2}x " +
                  $"ability='{context.AbilityName}'");

        if (context.AdditionalDamages.Count > 0)
        {
            foreach (var ad in context.AdditionalDamages)
                Debug.Log($"[DamageContext]   └ extra {ad.DamageType}: {ad.Amount:F1} (also critted)");
        }

        OnCriticalHit?.Invoke(context);
    }

    /// <summary>
    /// Add additional damage of a specific type.
    /// Multiple calls with the same <paramref name="damageType"/> are merged into one entry
    /// so the target receives a single consolidated hit (e.g. 3× Inflame +3 Fire → one +9 Fire).
    /// </summary>
    public void AddExtraDamage(string damageType, float amount, Color? flashColor = null)
    {
        if (amount <= 0) return;

        // Merge into an existing entry of the same type if one already exists.
        for (int i = 0; i < AdditionalDamages.Count; i++)
        {
            if (AdditionalDamages[i].DamageType == damageType)
            {
                var existing = AdditionalDamages[i];
                existing.Amount += amount;
                AdditionalDamages[i] = existing;
                return;
            }
        }

        // First entry for this type — add a new slot.
        AdditionalDamages.Add(new AdditionalDamage
        {
            DamageType = damageType,
            Amount     = amount,
            FlashColor = flashColor ?? Color.white
        });
    }
    
    /// <summary>
    /// Queue a status effect to apply after damage
    /// </summary>
    public void QueueStatusEffect(StatusEffectType type, float duration, float value, EffectConfig config = null)
    {
        PendingStatusEffects.Add(new PendingStatusEffect
        {
            EffectType = type,
            Duration = duration,
            Value = value,
            Config = config
        });
    }
    
    /// <summary>
    /// Add healing to apply to attacker
    /// </summary>
    public void AddAttackerHealing(float amount)
    {
        AttackerHealing += amount;
    }
    
    /// <summary>
    /// Add resource gain for attacker
    /// </summary>
    public void AddAttackerResource(string resourceType, float amount)
    {
        if (string.IsNullOrEmpty(resourceType) || amount <= 0)
            return;
        
        if (AttackerResources.ContainsKey(resourceType))
            AttackerResources[resourceType] += amount;
        else
            AttackerResources[resourceType] = amount;
    }
    
    /// <summary>
    /// Create a DamageContext from common parameters
    /// </summary>
    public static DamageContext Create(
        GameObject attacker,
        GameObject target,
        float baseDamage,
        string damageType,
        string abilityName = null,
        List<string> abilityTags = null,
        Vector3? hitPosition = null,
        float critMultiplier = 1f)
    {
        return new DamageContext
        {
            Attacker = attacker,
            Target = target,
            Damageable = target?.GetComponent<IDamageable>(),
            BaseDamage = baseDamage,
            FinalDamage = baseDamage,
            DamageType = damageType,
            AbilityName = abilityName ?? "",
            AbilityTags = abilityTags ?? new List<string>(),
            HitPosition = hitPosition ?? (target != null ? target.transform.position : Vector3.zero),
            CritMultiplier = critMultiplier
        };
    }
}

/// <summary>
/// Represents additional damage to be dealt alongside the main damage
/// </summary>
public struct AdditionalDamage
{
    public string DamageType;
    public float Amount;
    public Color FlashColor;
}

/// <summary>
/// Represents a status effect to be applied after damage
/// </summary>
public struct PendingStatusEffect
{
    public StatusEffectType EffectType;
    public float Duration;
    public float Value;
    public EffectConfig Config;
}
