using UnityEngine;

/// <summary>
/// Helper class with extension methods for easy trait integration.
/// Makes it simpler to query traits from abilities and other systems.
/// </summary>
public static class TraitSystemHelpers
{
    /// <summary>
    /// Get the final stat value with trait modifiers applied
    /// </summary>
    public static float GetModifiedStat(this GameObject character, string statID, float baseValue)
    {
        var traitManager = character.GetComponent<CharacterTraitManager>();
        if (traitManager != null)
        {
            return traitManager.CalculateFinalStat(statID, baseValue);
        }
        return baseValue;
    }
    
    /// <summary>
    /// Check if an ability has been replaced by a trait
    /// </summary>
    public static AbilityConfig GetAbilityReplacement(this GameObject character, string abilityName)
    {
        var traitManager = character.GetComponent<CharacterTraitManager>();
        if (traitManager != null)
        {
            return traitManager.GetAbilityReplacement(abilityName);
        }
        return null;
    }
    
    /// <summary>
    /// Check if character has a specific trait
    /// </summary>
    public static bool HasTrait(this GameObject character, TraitData traitData)
    {
        var traitManager = character.GetComponent<CharacterTraitManager>();
        if (traitManager != null)
        {
            return traitManager.HasTrait(traitData);
        }
        return false;
    }
    
    /// <summary>
    /// Check if character has a trait by ID
    /// </summary>
    public static bool HasTraitByID(this GameObject character, string traitID)
    {
        var traitManager = character.GetComponent<CharacterTraitManager>();
        if (traitManager != null)
        {
            var activeTraits = traitManager.GetActiveTraits();
            foreach (var trait in activeTraits)
            {
                if (trait.traitID == traitID)
                    return true;
            }
        }
        return false;
    }

    private static Organism ResolveOrganism(GameObject character)
    {
        if (character == null)
            return null;
        Organism organism = character.GetComponent<Organism>();
        if (organism == null)
            organism = character.GetComponentInParent<Organism>();
        return organism;
    }

    /// <summary>
    /// Get the stat container for a character GameObject (via its Organism).
    /// </summary>
    public static StatContainer GetAllStats(this GameObject character)
    {
        Organism organism = ResolveOrganism(character);
        return organism != null ? organism.AllStats : null;
    }

    /// <summary>
    /// Get the character's current health as a 0-1 percentage.
    /// </summary>
    public static float GetHealthPercent(this GameObject character)
    {
        Organism organism = ResolveOrganism(character);
        return organism != null ? organism.GetHealthPercentage() : 0f;
    }

    /// <summary>
    /// True if the character is at full health.
    /// </summary>
    public static bool IsAtFullHealth(this GameObject character)
    {
        Organism organism = ResolveOrganism(character);
        return organism != null && organism.GetHealthPercentage() >= 1f;
    }
}
