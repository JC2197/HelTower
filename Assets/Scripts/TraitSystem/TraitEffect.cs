using UnityEngine;

/// <summary>
/// Base class for custom trait effects.
/// Inherit from this to create complex trait behaviors that go beyond simple stat modifications.
/// </summary>
public abstract class TraitEffect : ScriptableObject
{
    protected GameObject character;
    protected TraitData traitData;
    
    /// <summary>
    /// Initialize the effect with character and trait data
    /// </summary>
    public virtual void Initialize(GameObject targetCharacter, TraitData data)
    {
        character = targetCharacter;
        traitData = data;
    }
    
    /// <summary>
    /// Called when the trait is activated
    /// </summary>
    public abstract void OnActivate();
    
    /// <summary>
    /// Called when the trait is deactivated
    /// </summary>
    public abstract void OnDeactivate();
    
    /// <summary>
    /// Optional: Called every frame if needed
    /// </summary>
    public virtual void Update()
    {
        // Override if you need per-frame logic
    }
}
