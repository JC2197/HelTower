using UnityEngine;

/// <summary>
/// Interface for objects that can be interacted with by the player
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Called when the player presses the interact button while in range
    /// </summary>
    void OnInteract(GameObject player);
    
    /// <summary>
    /// Returns the interaction prompt text to display (e.g., "Press F to activate")
    /// </summary>
    string GetInteractionPrompt();
    
    /// <summary>
    /// Returns true if this object can currently be interacted with
    /// </summary>
    bool CanInteract();
    
    /// <summary>
    /// Returns the transform of the interactable object
    /// </summary>
    Transform GetTransform();
}
