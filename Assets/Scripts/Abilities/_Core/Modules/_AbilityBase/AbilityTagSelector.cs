using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Serializable tag/damage-type selector for abilities. Tags flow into the damage pipeline
/// for tag-based modifiers. Ability tags are free-form strings plus damage-type-derived tags.
/// </summary>
[System.Serializable] // NOT a MonoBehaviour
public class AbilityTagSelector
{
    [SerializeField] private List<string> selectedTags = new List<string>();
    [SerializeField] private List<DamageTypeData> selectedDamageTypes = new List<DamageTypeData>();

    public List<string> SelectedTags => selectedTags;
    public List<DamageTypeData> SelectedDamageTypes => selectedDamageTypes;

    public bool HasTag(string tagName)
    {
        if (selectedTags.Contains(tagName)) return true;

        foreach (var damageType in selectedDamageTypes)
        {
            if (damageType == null) continue;

            if (tagName.Equals(damageType.displayName, System.StringComparison.OrdinalIgnoreCase) ||
                tagName.Equals(damageType.GetSubcategory(), System.StringComparison.OrdinalIgnoreCase) ||
                tagName.Equals(damageType.category, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void AddTag(string tagName)
    {
        if (!string.IsNullOrEmpty(tagName) && !HasTag(tagName))
            selectedTags.Add(tagName);
    }

    public void RemoveTag(string tagName) => selectedTags.Remove(tagName);

    public void AddDamageType(DamageTypeData damageType)
    {
        if (damageType != null && !selectedDamageTypes.Contains(damageType))
            selectedDamageTypes.Add(damageType);
    }

    public void RemoveDamageType(DamageTypeData damageType) => selectedDamageTypes.Remove(damageType);

    public void SetTags(List<string> tags) => selectedTags = new List<string>(tags);

    public void SetDamageTypes(List<DamageTypeData> damageTypes) => selectedDamageTypes = new List<DamageTypeData>(damageTypes);

    public bool HasAnyTag(params string[] tags) => tags.Any(HasTag);

    public bool HasAllTags(params string[] tags) => tags.All(HasTag);

    public bool HasDamageType(string category) => selectedDamageTypes.Any(dt => dt != null && dt.IsCategory(category));

    public List<DamageTypeData> GetDamageTypesByCategory(string category) =>
        selectedDamageTypes.Where(dt => dt != null && dt.IsCategory(category)).ToList();

    /// <summary>Get all combined tags (free-form tags + damage-type-derived tags).</summary>
    public List<string> GetAllTags()
    {
        var allTags = new List<string>(selectedTags);

        foreach (var damageType in selectedDamageTypes)
        {
            if (damageType == null) continue;

            allTags.Add(damageType.displayName);
            allTags.Add(damageType.category);

            string subcategory = damageType.GetSubcategory();
            if (subcategory != damageType.category)
                allTags.Add(subcategory);
        }

        return allTags.Distinct().ToList();
    }
}
