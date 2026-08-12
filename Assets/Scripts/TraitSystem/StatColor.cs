using UnityEngine;

/// <summary>
/// Defines a color theme for stats or trait effects.
/// Can be used to visually categorize traits, buffs, or stat displays.
/// </summary>
[CreateAssetMenu(fileName = "New Stat Color", menuName = "Traits/Stat Color")]
public class StatColor : ScriptableObject
{
    [Header("Color Identity")]
    [Tooltip("Unique identifier for this color (e.g., 'Fire', 'Ice', 'Physical')")]
    public string colorID;
    
    [Tooltip("Display name shown to players")]
    public string displayName;
    
    [Header("Visual")]
    [Tooltip("Primary color used for UI elements")]
    public Color primaryColor = Color.white;
    
    [Tooltip("Secondary color for gradients or highlights")]
    public Color secondaryColor = Color.white;
    
    [Tooltip("Optional icon representing this color/element")]
    public Sprite icon;
    
    [Header("Description")]
    [TextArea(2, 4)]
    [Tooltip("Description of what this color represents")]
    public string description;
}
