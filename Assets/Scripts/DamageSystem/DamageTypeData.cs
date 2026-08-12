using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "DamageType_", menuName = "Damage/Damage Type Data")]
public class DamageTypeData : ScriptableObject
{
    [Header("Damage Type Identity")]
    public string damageTypeName = "Physical";
    public string displayName = "Physical";
    [TextArea(2, 4)]
    public string description = "";
    public Color damageColor = Color.red;
    
    [Header("Category & Subcategory")]
    [Tooltip("Free-form category id (e.g. Physical, Elemental). Fully data-driven, no fixed set.")]
    public string category = "";
    [Tooltip("Optional subcategory id (e.g. Fire, Slashing). Leave blank to fall back to the category.")]
    public string subcategory = "";

    [Header("Critical Hits")]
    public bool canCriticalHit = true;

    [Header("Damage Interactions")]
    public List<DamageTypeInteraction> interactions = new List<DamageTypeInteraction>();

    [Header("Special Properties")]
    public bool ignoresShields = false;

    /// <summary>
    /// Returns the subcategory id, or the category id when no subcategory is set.
    /// </summary>
    public string GetSubcategory()
    {
        return !string.IsNullOrWhiteSpace(subcategory) ? subcategory : category;
    }

    public bool IsCategory(string categoryId)
    {
        return !string.IsNullOrWhiteSpace(category)
            && category.Equals(categoryId, System.StringComparison.OrdinalIgnoreCase);
    }

    public string GetFormattedDescription()
    {
        string desc = $"<color=#{ColorUtility.ToHtmlStringRGB(damageColor)}>{displayName}</color>\n";
        desc += description;
        
        if (ignoresShields)
        {
            desc += "\n• Ignores shields";
        }
        
        return desc;
    }
}

[System.Serializable]
public class DamageTypeInteraction
{
    public DamageTypeData targetDamageType;
    public InteractionType interactionType;
    public float effectMultiplier = 1f;
    public DamageTypeData resultingDamageType;
    
    public enum InteractionType
    {
    }
}

public enum StatusEffectType
{
    
}