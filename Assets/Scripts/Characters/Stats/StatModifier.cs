using UnityEngine;


public enum ModifierType
{
    Flat,           // Adds flat value (e.g., value=15 adds +15 to stat)
    Percentage,     // Multiplies by percentage (e.g., value=15 means +15% = 1.15x multiplier)
    Override        // Sets to specific value (NOT USED for gear/traits)
}

/// <summary>
/// Represents a stat modifier that can be applied to a character's stats.
/// </summary>
[System.Serializable]
public class StatModifier
{
    [Tooltip("Stat to modify (from StatTypeDatabase)")]
    public string statID = "MoveSpeed";

    [Tooltip("How to apply the modifier")]
    public ModifierType modifierType = ModifierType.Flat;

    [Tooltip("Value to add/multiply")]
    public float value = 10f;

    public string GetDisplayText()
    {
        string sign = value >= 0 ? "+" : "";
        switch (modifierType)
        {
            case ModifierType.Flat:
                return $"{sign}{value}";
            case ModifierType.Percentage:
                return $"{sign}{value}%";
            case ModifierType.Override:
                return $"={value}";
            default:
                return value.ToString();
        }
    }
}


