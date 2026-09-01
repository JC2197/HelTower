using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Slow effect - reduces movement speed of any organism
/// Works by directly reducing the target organism's MoveSpeed stat while active.
/// </summary>
[CreateAssetMenu(fileName = "Slow Effect", menuName = "Effects/Slow Effect")]
public class SlowEffect : EffectConfig
{
    [Header("Slow Settings")]
    [Tooltip("Percentage of speed reduction (0.5 = 50% slow)")]
    [Range(0f, 1f)]
    public float slowAmount = 0.5f;
    
    [Tooltip("Visual effect to show slow")]
    public GameObject slowVisualEffect;

    // Slow is applied directly by mutating the target's MoveSpeed stat in OnApply/OnRemove.
    // Keep this neutral to avoid double-applying slowdown in movement code.
    public override float MovementSpeedMultiplier => 1f;
    
    // Track visual state per effected target.
    private static Dictionary<GameObject, SpriteRenderer> slowedSprites = new Dictionary<GameObject, SpriteRenderer>();
    private static Dictionary<GameObject, Color> originalSpriteColors = new Dictionary<GameObject, Color>();
    private static Dictionary<GameObject, float> appliedMoveSpeedDeltas = new Dictionary<GameObject, float>();

    private Organism ResolveOrganism(GameObject target)
    {
        Organism organism = target.GetComponent<Organism>();
        if (organism == null)
        {
            organism = target.GetComponentInParent<Organism>();
        }
        return organism;
    }
    
    private SpriteRenderer ResolveSpriteRenderer(GameObject target)
    {
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = target.GetComponentInParent<SpriteRenderer>();
        }
        return sr;
    }
    
    public override void OnApply(GameObject target, GameObject source)
    {
        Organism organism = ResolveOrganism(target);
        if (organism != null && organism.AllStats != null && organism.AllStats.HasStat("MoveSpeed"))
        {
            float currentMoveSpeed = organism.AllStats.GetStat("MoveSpeed");
            float moveSpeedDelta = -(currentMoveSpeed * slowAmount);
            organism.AllStats.ModifyStat("MoveSpeed", moveSpeedDelta);
            
            appliedMoveSpeedDeltas[target] = moveSpeedDelta;
        }

        SpriteRenderer sr = ResolveSpriteRenderer(target);
        if (sr != null)
        {
            if (!originalSpriteColors.ContainsKey(target))
            {
                originalSpriteColors[target] = sr.color;
            }
            slowedSprites[target] = sr;
            sr.color = entityTint;
        }

        Debug.Log($"{target.name} has been Slowed by {slowAmount * 100}% for {duration} seconds!");
    }
    
    public override void OnUpdate(GameObject target, float deltaTime)
    {
        // No per-frame work needed: speed is modified on apply and restored on remove.
    }
    
    public override void OnRemove(GameObject target)
    {
        Organism organism = ResolveOrganism(target);
        if (organism != null && organism.AllStats != null && appliedMoveSpeedDeltas.TryGetValue(target, out float appliedDelta))
        {
            organism.AllStats.ModifyStat("MoveSpeed", -appliedDelta);
            
        }

        // Restore original color
        if (slowedSprites.TryGetValue(target, out SpriteRenderer sr) && sr != null)
        {
            if (originalSpriteColors.TryGetValue(target, out Color originalColor))
            {
                sr.color = originalColor;
            }
            else
            {
                sr.color = Color.white;
            }
        }

        slowedSprites.Remove(target);
        originalSpriteColors.Remove(target);
        appliedMoveSpeedDeltas.Remove(target);
        
        Debug.Log($"{target.name} is no longer Slowed");
    }
}
