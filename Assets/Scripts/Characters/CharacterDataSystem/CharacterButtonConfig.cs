using UnityEngine;

/// <summary>
/// Configuration for character selection button visuals.
/// Stores different sprite states for button appearance.
/// </summary>
[System.Serializable]
public class CharacterButtonConfig
{
    [Header("Button Icons")]
    [Tooltip("Default button icon sprite")]
    public Sprite icon;
    
    [Tooltip("Icon sprite when button is highlighted/hovered")]
    public Sprite iconHighlighted;
    
    [Tooltip("Icon sprite when button is selected")]
    public Sprite iconSelected;
}
