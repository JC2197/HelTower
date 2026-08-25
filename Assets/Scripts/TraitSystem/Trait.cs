using UnityEngine;

/// <summary>
/// Runtime instance of a trait applied to a character.
/// This is the active version of TraitData that tracks state.
/// </summary>
public class Trait
{
    public TraitData data { get; private set; }
    public bool isActive { get; private set; }
    private TraitEffect effectInstance;
    public int level;
    public Trait(TraitData traitData)
    {
        data = traitData;
        isActive = false;
        level = 1;
    }
    
    /// <summary>
    /// Activate this trait on a character
    /// </summary>
    public void Activate(GameObject character)
    {
        if (isActive)
        {
            Debug.LogWarning($"Trait {data.displayName} is already active!");
            return;
        }
        
        isActive = true;
        
        // If this trait has a special effect, instantiate and initialize it
        if (data.effectScript != null)
        {
            effectInstance = Object.Instantiate(data.effectScript);
            effectInstance.Initialize(character, data);
            effectInstance.OnActivate();
        }
        
        Debug.Log($"Activated trait: {data.displayName}");
    }
    
    /// <summary>
    /// Deactivate this trait (for respec or removal)
    /// </summary>
    public void Deactivate(GameObject character)
    {
        if (!isActive)
            return;
        
        isActive = false;
        
        // Clean up effect instance
        if (effectInstance != null)
        {
            effectInstance.OnDeactivate();
            Object.Destroy(effectInstance);
            effectInstance = null;
        }
        
        Debug.Log($"Deactivated trait: {data.displayName}");
    }
    
    /// <summary>
    /// Get the total modifier value for a specific stat type
    /// </summary>
    public float GetStatModifier(string statID, TraitModifierType modifierType)
    {
        if (!isActive)
            return 0f;
        
        float total = 0f;
        foreach (var modifier in data.statModifiers)
        {
            if (modifier.statID == statID && modifier.modifierType == modifierType)
            {
                total += modifier.value;
            }
        }
        
        return total;
    }
}
