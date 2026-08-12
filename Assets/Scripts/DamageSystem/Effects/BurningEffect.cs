using UnityEngine;

/// <summary>
/// Burning effect - fire damage over time.
/// Defines the visual appearance and behavior of burning.
/// Damage and duration are configured per-ability in EffectData.
/// </summary>
[CreateAssetMenu(fileName = "Burning Effect", menuName = "Effects/Burning Effect")]
public class BurningEffect : DamageOverTimeConfig
{
    [Header("Burning Visual Settings")]
    [Tooltip("Fire particle effect spawned on damage ticks")]
    public GameObject fireParticle;
    
    [Tooltip("Color tint applied to burning entities")]
    public Color burningTint = new Color(1f, 0.8f, 0.5f, 1f);
    
    [Header("Burning Behavior")]
    [Tooltip("Can burning spread to nearby enemies?")]
    public bool canSpread = false;
    
    [Tooltip("Spread radius (if enabled)")]
    public float spreadRadius = 1.5f;
    
    [Tooltip("Spread chance per tick")]
    [Range(0f, 1f)]
    public float spreadChance = 0.15f;
    
    [Tooltip("Should burning increase in damage over time?")]
    public bool increasesOverTime = false;
    
    [Tooltip("Damage multiplier increase per tick (if increasesOverTime is true)")]
    public float damageIncreasePerTick = 0.05f;
    
    private float currentDamageMultiplier = 1f;

    private void OnEnable()
    {
        // Set defaults for burning
        effectName = "Burning";
        effectID = "burning";
        damageTypeName = "Burning";
        tickInterval = 0.5f; // Burning ticks twice per second for fast damage
        canBeCleansed = true;
        canTargetEnemies = true;
        canTargetAllies = false;
        isBuff = false;
    }

    protected override void OnDotApplied(GameObject target, GameObject source)
    {
        // Reset damage multiplier
        currentDamageMultiplier = 1f;
        
        // Apply burning tint to entity
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = burningTint;
        }
        
        base.OnDotApplied(target, source);
    }

    public override void OnDamageTick(GameObject target, float damage)
    {
        base.OnDamageTick(target, damage);
        
        // Increase damage over time
        if (increasesOverTime)
        {
            currentDamageMultiplier += damageIncreasePerTick;
            // Note: The actual damage increase is handled in EffectManager
            // This is just tracking the multiplier for potential future use
        }
        
        // Attempt to spread burning to nearby enemies
        if (canSpread && Random.value < spreadChance)
        {
            SpreadBurning(target);
        }
    }

    private void SpreadBurning(GameObject source)
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
                // Apply burning
                effectManager.ApplyEffect(this, source);
                Debug.Log($"Burning spread from {source.name} to {col.gameObject.name}!");
                break; // Only spread to one target per tick
            }
        }
    }

    protected override void OnDotRemoved(GameObject target)
    {
        // Reset multiplier
        currentDamageMultiplier = 1f;
        
        // Restore original color
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.white;
        }
        
        base.OnDotRemoved(target);
        Debug.Log($"{target.name} is no longer Burning");
    }
}
