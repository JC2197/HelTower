using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Stun effect - completely disables all actions and movement
/// More severe than root - prevents attacks and abilities as well
/// </summary>
[CreateAssetMenu(fileName = "Stun Effect", menuName = "Effects/Stun Effect")]
public class StunEffect : EffectConfig
{
    [Header("Stun Settings")]
    [Tooltip("Visual effect for stun (e.g., stars around head)")]
    public GameObject stunVisualEffect;
    
    [Tooltip("Color tint applied to stunned entities")]
    public Color stunTint = new Color(0.8f, 0.8f, 0.8f, 1f);

    public override bool IsStunned => true;
    public override float MovementSpeedMultiplier => 1f;
    
    private static Dictionary<GameObject, SpriteRenderer> stunnedSprites = new Dictionary<GameObject, SpriteRenderer>();
    private static Dictionary<GameObject, Color> originalSpriteColors = new Dictionary<GameObject, Color>();
    private static Dictionary<GameObject, GameObject> activeStunVfx = new Dictionary<GameObject, GameObject>();
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

    private GameObject ResolveEffectTarget(GameObject target)
    {
        Organism organism = ResolveOrganism(target);
        return organism != null ? organism.gameObject : target;
    }

    private Rigidbody2D ResolveRigidbody(GameObject target)
    {
        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = target.GetComponentInParent<Rigidbody2D>();
        }
        return rb;
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
        GameObject effectTarget = ResolveEffectTarget(target);

        Organism organism = ResolveOrganism(effectTarget);
        if (organism != null && organism.AllStats != null && organism.AllStats.HasStat("MoveSpeed"))
        {
            float currentMoveSpeed = organism.AllStats.GetStat("MoveSpeed");
            float moveSpeedDelta = -currentMoveSpeed;
            organism.AllStats.ModifyStat("MoveSpeed", moveSpeedDelta);
            organism.RefreshMoveSpeedFromStats();
            appliedMoveSpeedDeltas[effectTarget] = moveSpeedDelta;
        }

        Rigidbody2D rb = ResolveRigidbody(effectTarget);
        if (rb != null)
        {
            // Immediately stop all movement
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // Apply visual tint
        SpriteRenderer sr = ResolveSpriteRenderer(effectTarget);
        if (sr != null)
        {
            if (!originalSpriteColors.ContainsKey(effectTarget))
            {
                originalSpriteColors[effectTarget] = sr.color;
            }
            stunnedSprites[effectTarget] = sr;
            sr.color = stunTint;
        }
        
        // Spawn stun visual effect
        if (stunVisualEffect != null)
        {
            if (activeStunVfx.TryGetValue(effectTarget, out GameObject existingVfx) && existingVfx != null)
            {
                Destroy(existingVfx);
            }

            GameObject vfx = Instantiate(stunVisualEffect, effectTarget.transform.position + particleOffset, Quaternion.identity);
            Transform parent = rb != null ? rb.transform : effectTarget.transform;
            vfx.transform.SetParent(parent);
            activeStunVfx[effectTarget] = vfx;
        }
        
        Debug.Log($"{effectTarget.name} has been Stunned for {duration} seconds!");
    }
    
    public override void OnUpdate(GameObject target, float deltaTime)
    {
        // Continuously zero out velocity and prevent any actions
        GameObject effectTarget = ResolveEffectTarget(target);
        Rigidbody2D rb = ResolveRigidbody(effectTarget);
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
    
    public override void OnRemove(GameObject target)
    {
        GameObject effectTarget = ResolveEffectTarget(target);

        Organism organism = ResolveOrganism(effectTarget);
        if (organism != null && organism.AllStats != null && appliedMoveSpeedDeltas.TryGetValue(effectTarget, out float appliedDelta))
        {
            organism.AllStats.ModifyStat("MoveSpeed", -appliedDelta);
            organism.RefreshMoveSpeedFromStats();
        }

        appliedMoveSpeedDeltas.Remove(effectTarget);
        
        // Restore original color
        if (stunnedSprites.TryGetValue(effectTarget, out SpriteRenderer sr) && sr != null)
        {
            if (originalSpriteColors.TryGetValue(effectTarget, out Color originalColor))
            {
                sr.color = originalColor;
            }
            else
            {
                sr.color = Color.white;
            }
        }

        stunnedSprites.Remove(effectTarget);
        originalSpriteColors.Remove(effectTarget);

        if (activeStunVfx.TryGetValue(effectTarget, out GameObject stunVfx) && stunVfx != null)
        {
            Destroy(stunVfx);
        }
        activeStunVfx.Remove(effectTarget);
        
        Debug.Log($"{effectTarget.name} is no longer Stunned");
    }
}
