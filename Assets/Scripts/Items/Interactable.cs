using UnityEngine;
using FishNet.Object;

/// <summary>
/// Base class for all interactable objects in the game.
/// Extend this class to create custom interactable behaviors.
/// 
/// MULTIPLAYER: Inherits from NetworkBehaviour to support networked interactions.
/// Interactables can check IsServerStarted to make interactions server-authoritative.
/// 
/// Collider Setup:
/// - Option 1: Assign a specific child GameObject with a trigger collider to 'interactionCollider'
/// - Option 2: Leave 'interactionCollider' null and it will auto-detect any trigger collider in children
/// - Non-trigger colliders are ignored (you can have solid colliders for physics)
/// - PlayerInteraction uses GetComponentInParent to find interactables from child colliders
/// </summary>
public abstract class Interactable : NetworkBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [SerializeField] protected string interactionMessage = "Interact";
    
    [Tooltip("Optional: Specific child GameObject with the trigger collider for interaction. If null, will auto-detect.")]
    [SerializeField] protected GameObject interactionCollider;
    
    [Tooltip("Should this interactable be controlled by arena clear events? (Enable after all enemies defeated)")]
    [SerializeField] protected bool controlledByArenaClear = false;

    [Tooltip("If true, this object cannot be interacted with again after the first interaction.")]
    [SerializeField] protected bool singleUse = false;
    
    [Tooltip("For debug visualization only. Actual detection range is controlled by PlayerInteraction component.")]
    [SerializeField] protected float interactionRange = 2f;
    [SerializeField] protected bool isCurrentlyInteractable = true;
    
    [Header("Debug")]
    [SerializeField] protected bool showDebugGizmos = true;
    [SerializeField] protected Color gizmoColor = Color.cyan;
    [Tooltip("Exact Animator state name to play when the object is idle (not interacted with).")]
    [SerializeField] private string idleAnimationState;
    [Tooltip("Exact Animator state name to play when the player interacts with this object.")]
    [SerializeField] private string onInteractAnimationState;
    protected bool isOn = false;
    protected Animator animator;
    protected virtual void Awake()
    {
        // If a specific interaction collider is assigned, validate it
        animator = GetComponent<Animator>();
        if (animator != null && !string.IsNullOrEmpty(idleAnimationState))
        {
            animator.Play(idleAnimationState, 0, 0f);
        }

        if (interactionCollider != null)
        {
            Debug.Log($"[Interactable] {gameObject.name}: Using assigned interactionCollider '{interactionCollider.name}'");
            Collider2D col = interactionCollider.GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogWarning($"[Interactable] {gameObject.name}: Assigned interactionCollider '{interactionCollider.name}' has no Collider2D component!");
            }
            else if (!col.isTrigger)
            {
                Debug.LogWarning($"[Interactable] {gameObject.name}: Assigned interactionCollider '{interactionCollider.name}' must have isTrigger=true!");
            }
        }
    }
    
    #region IInteractable Implementation
    
    /// <summary>
    /// Called when player interacts with this object.
    /// Override this to implement custom interaction behavior.
    /// </summary>
    public virtual void OnInteract(GameObject player)
    {
        isOn = !isOn;
        PlayToggleAnimation();
        if (singleUse)
            SetInteractable(false);
    }

    protected void PlayToggleAnimation()
    {
        if (animator == null) return;
        string state = isOn ? onInteractAnimationState : idleAnimationState;
        if (!string.IsNullOrEmpty(state))
            animator.Play(state, 0, 0f);
    }
    
    /// <summary>
    /// Returns the interaction prompt to display to the player.
    /// Override to customize the message format.
    /// </summary>
    public virtual string GetInteractionPrompt()
    {
        return interactionMessage;
    }
    
    /// <summary>
    /// Returns whether this object can currently be interacted with.
    /// Override to add custom interaction conditions.
    /// </summary>
    public virtual bool CanInteract()
    {
        return isCurrentlyInteractable && gameObject.activeInHierarchy;
    }
    
    public Transform GetTransform()
    {
        return transform;
    }
    
    /// <summary>
    /// Returns whether this interactable should be controlled by arena clear events.
    /// </summary>
    public bool IsControlledByArenaClear()
    {
        return controlledByArenaClear;
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Enable or disable interaction with this object
    /// </summary>
    public virtual void SetInteractable(bool interactable)
    {
        isCurrentlyInteractable = interactable;
    }
    
    /// <summary>
    /// Change the interaction message at runtime
    /// </summary>
    public virtual void SetInteractionMessage(string message)
    {
        interactionMessage = message;
    }
    
    /// <summary>
    /// Check if a player is within interaction range
    /// </summary>
    protected bool IsPlayerInRange(GameObject player)
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.transform.position) <= interactionRange;
    }
    
    #endregion
    
    #region Debug Visualization
    
    #if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Draw interaction range
        Gizmos.color = isCurrentlyInteractable ? gizmoColor : Color.gray;
        
        // Use the assigned interactionCollider if available, otherwise try component on this GameObject
        Collider2D col = null;
        if (interactionCollider != null)
        {
            col = interactionCollider.GetComponent<Collider2D>();
        }
        else
        {
            col = GetComponent<Collider2D>();
        }
        
        if (col != null)
        {
            if (col is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere(col.transform.position + (Vector3)circle.offset, circle.radius);
            }
            else if (col is BoxCollider2D box)
            {
                Gizmos.matrix = col.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.offset, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else if (col is CapsuleCollider2D capsule)
            {
                // Draw simplified capsule as circle
                Gizmos.DrawWireSphere(col.transform.position + (Vector3)capsule.offset, capsule.size.x * 0.5f);
            }
        }
        
        // Draw interaction icon
        Gizmos.color = gizmoColor;
        Gizmos.DrawIcon(transform.position, "sv_icon_dot3_pix16_gizmo", true);
    }
    
    protected virtual void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Draw additional range indicator when selected
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
        Gizmos.DrawSphere(transform.position, interactionRange);
    }
    #endif
    
    #endregion
}
