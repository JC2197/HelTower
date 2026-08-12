using UnityEngine;

/// <summary>
/// Bleed effect - physical damage over time from slashing attacks.
/// Defines the visual appearance and behavior of bleeding.
/// Damage and duration are configured per-ability in EffectData.
/// </summary>
[CreateAssetMenu(fileName = "Bleed Effect", menuName = "Effects/Bleed Effect")]
public class BleedEffect : DamageOverTimeConfig
{
    [Header("Bleed Visual Settings")]
    [Tooltip("Blood particle effect spawned on damage ticks")]
    public GameObject bloodParticle;
    
    [Tooltip("Color tint applied to bleeding entities")]
    public Color bleedTint = new Color(1f, 0.7f, 0.7f, 1f);

    private void OnEnable()
    {
        // Set defaults for bleed
        effectName = "Bleeding";
        effectID = "bleed";
        damageTypeName = "Bleeding";
        tickInterval = 1f; // Bleed ticks every second
        canBeCleansed = true;
        canTargetEnemies = true;
        canTargetAllies = false;
        isBuff = false;
    }

    protected override void OnDotApplied(GameObject target, GameObject source)
    {
        // Apply bleed tint to entity
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = bleedTint;
        }
        
        base.OnDotApplied(target, source);
    }

    protected override void OnDotRemoved(GameObject target)
    {
        // Restore original color
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Organism organism = target.GetComponent<Organism>();
            if (organism != null)
            {
                // Try to restore original color from Organism if available
                // This is a simplified approach - you may need to store original color differently
                sr.color = Color.white;
            }
        }
        
        base.OnDotRemoved(target);
        Debug.Log($"{target.name} has stopped Bleeding");
    }
}
