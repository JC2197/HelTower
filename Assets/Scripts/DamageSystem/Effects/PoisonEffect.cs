using UnityEngine;

/// <summary>
/// Poison effect - nature/elemental damage over time.
/// Defines the visual appearance and behavior of poisoning.
/// Damage and duration are configured per-ability in EffectData.
/// </summary>
[CreateAssetMenu(fileName = "Poison Effect", menuName = "Effects/Poison Effect")]
public class PoisonEffect : DamageOverTimeConfig
{
    [Header("Poison Visual Settings")]
    [Tooltip("Poison particle effect spawned on damage ticks")]
    public GameObject poisonParticle;
    
    [Tooltip("Color tint applied to poisoned entities")]
    public Color poisonTint = new Color(0.7f, 1f, 0.7f, 1f);
    
    [Header("Poison Behavior")]
    [Tooltip("Can poison spread to nearby enemies?")]
    public bool canSpread = false;
    
    [Tooltip("Spread radius (if enabled)")]
    public float spreadRadius = 2f;
    
    [Tooltip("Spread chance per tick")]
    [Range(0f, 1f)]
    public float spreadChance = 0.1f;

    private void OnEnable()
    {
        // Set defaults for poison
        effectName = "Poisoned";
        effectID = "poison";
        damageTypeName = "Poison";
        tickInterval = 1f; // Poison ticks every second
        canBeCleansed = true;
        canTargetEnemies = true;
        canTargetAllies = false;
        isBuff = false;
    }

    protected override void OnDotApplied(GameObject target, GameObject source)
    {
        // Apply poison tint to entity
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = poisonTint;
        }
        
        base.OnDotApplied(target, source);
    }

    public override void OnDamageTick(GameObject target, float damage)
    {
        base.OnDamageTick(target, damage);
        
        // Attempt to spread poison to nearby enemies
        if (canSpread && Random.value < spreadChance)
        {
            SpreadPoison(target);
        }
    }

    private void SpreadPoison(GameObject source)
    {
        // Find nearby enemies
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(source.transform.position, spreadRadius);
        
        foreach (Collider2D col in nearbyColliders)
        {
            if (col.gameObject == source) continue;
            
            // Check if the target has an EffectManager (check in children too)
            EffectManager effectManager = col.GetComponentInChildren<EffectManager>();
            if (effectManager != null)
            {
                // Apply a weaker version of the poison
                effectManager.ApplyEffect(this, source);
                Debug.Log($"Poison spread from {source.name} to {col.gameObject.name}!");
                break; // Only spread to one target per tick
            }
        }
    }

    protected override void OnDotRemoved(GameObject target)
    {
        // Restore original color
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.white;
        }
        
        base.OnDotRemoved(target);
        Debug.Log($"{target.name} is no longer Poisoned");
    }
}
