using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using FishNet.Object;

/// <summary>
/// Handles player interaction with IInteractable objects in the world
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private LayerMask interactionLayer = -1; // All layers by default
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    
    private IInteractable currentInteractable;
    private PlayerInput playerInput;
    private InputAction interactAction;
    private NetworkObject _nob;
    
    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            interactAction = playerInput.actions["Interact"];
        }
        _nob = GetComponent<NetworkObject>();
    }
    
    void Update()
    {
        // Only process interactions for the local player (owner in multiplayer)
        if (_nob != null && _nob.IsSpawned && !_nob.IsOwner)
            return;
        
        // Find nearby interactables
        FindNearestInteractable();
        
        // Check for interact input
        if (interactAction != null && interactAction.WasPressedThisFrame())
        {
            TryInteract();
        }
    }
    
    void FindNearestInteractable()
    {
        IInteractable previousInteractable = currentInteractable;
        currentInteractable = null;
        
        // Find all colliders in range
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRange, interactionLayer);
        
        // Check if mouse is over any interactable
        IInteractable mouseHoverInteractable = GetInteractableUnderMouse();
        
        // Use float.MaxValue so any collider found by OverlapCircleAll is valid
        // We only use distance to pick the closest one if there are multiple
        float closestDistance = float.MaxValue;
        IInteractable closestInteractable = null;
        
        foreach (Collider2D col in colliders)
        {
            // Skip self
            if (col.gameObject == gameObject) continue;
            
            // Only detect trigger colliders for interaction (ignore solid collision colliders)
            if (!col.isTrigger) continue;
            
            // Check if object implements IInteractable (check parent hierarchy for child colliders)
            IInteractable interactable = col.GetComponentInParent<IInteractable>();
            
            if (interactable != null && interactable.CanInteract())
            {
                // Calculate distance to collider center (including offset) for prioritization when multiple interactables overlap
                Vector2 colliderCenter = (Vector2)col.transform.position + col.offset;
                float distance = Vector2.Distance(transform.position, colliderCenter);
                
                // Track closest interactable
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestInteractable = interactable;
                }
                
                // If this is the one under the mouse, check if it's in range
                if (mouseHoverInteractable == interactable)
                {
                    // Prioritize mouse hover if it's the same one we found in range
                    currentInteractable = interactable;
                }
            }
        }
        
        // If no mouse hover interactable was selected (or mouse is outside range), use closest
        if (currentInteractable == null)
        {
            currentInteractable = closestInteractable;
        }
        
        // Notify UI if interactable changed
        if (currentInteractable != previousInteractable)
        {
            OnInteractableChanged();
        }
    }
    
    /// <summary>
    /// Check if mouse is hovering over an interactable (regardless of range)
    /// </summary>
    IInteractable GetInteractableUnderMouse()
    {
        if (Mouse.current == null || Camera.main == null) return null;
        
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Camera.main.nearClipPlane));
        
        // Check what collider is under the mouse
        Collider2D hit = Physics2D.OverlapPoint(worldPosition, interactionLayer);
        
        if (hit != null && hit.isTrigger)
        {
            return hit.GetComponentInParent<IInteractable>();
        }
        
        return null;
    }
    
    void TryInteract()
    {
        if (currentInteractable != null && currentInteractable.CanInteract())
        {
            currentInteractable.OnInteract(gameObject);
        }
    }
    
    void OnInteractableChanged()
    {
        if (currentInteractable != null)
        {
            // Show interaction prompt
            string prompt = currentInteractable.GetInteractionPrompt();
            InteractionPromptUI.Show(prompt);
        }
        else
        {
            // Hide interaction prompt
            InteractionPromptUI.Hide();
        }
    }
    
    void OnDisable()
    {
        // Hide prompt when player is disabled
        InteractionPromptUI.Hide();
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Draw interaction range
        Gizmos.color = currentInteractable != null ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
        
        // Draw line to current interactable
        if (currentInteractable != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentInteractable.GetTransform().position);
        }
    }
    
    // Public API for external access
    public IInteractable GetCurrentInteractable() => currentInteractable;
    public bool HasInteractable() => currentInteractable != null;
}
