using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Root effect - prevents all movement for any organism
/// Works by zeroing velocity on the Rigidbody2D each frame
/// </summary>
[CreateAssetMenu(fileName = "Root Effect", menuName = "Effects/Root Effect")]
public class RootEffect : EffectConfig
{
    [Header("Root Settings")]
    [Tooltip("Should this also prevent dashing?")]
    public bool preventsDashing = true;

    public override bool IsRooted => true;

    // Root is applied directly by mutating MoveSpeed in OnApply/OnRemove.
    // Keep this neutral to avoid double-applying movement reduction.
    public override float MovementSpeedMultiplier => 1f;

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
    
    public override void OnApply(GameObject target, GameObject source)
    {
        GameObject effectTarget = ResolveEffectTarget(target);

        Organism organism = ResolveOrganism(effectTarget);
        if (organism != null && organism.AllStats != null && organism.AllStats.HasStat("MoveSpeed"))
        {
            float currentMoveSpeed = organism.AllStats.GetStat("MoveSpeed");
            float moveSpeedDelta = -currentMoveSpeed;
            organism.AllStats.ModifyStat("MoveSpeed", moveSpeedDelta);
            
            appliedMoveSpeedDeltas[effectTarget] = moveSpeedDelta;
        }

        Rigidbody2D rb = ResolveRigidbody(effectTarget);
        if (rb != null)
        {
            // Immediately stop all movement this frame.
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        Debug.Log($"{effectTarget.name} has been Rooted for {duration} seconds!");
    }
    
    public override void OnUpdate(GameObject target, float deltaTime)
    {
        // Keep velocity pinned to zero while rooted.
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
        }

        appliedMoveSpeedDeltas.Remove(effectTarget);

        Debug.Log($"{effectTarget.name} is no longer Rooted");
    }
}
