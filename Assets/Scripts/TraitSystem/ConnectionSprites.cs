using UnityEngine;

/// <summary>
/// OBSOLETE: Container for connection sprite assets used in the trait tree.
/// NO LONGER USED - Trait tree now uses LineRenderer instead of sprites.
/// This class is kept for backward compatibility with existing assets.
/// </summary>
[System.Serializable]
[System.Obsolete("ConnectionSprites is obsolete. Trait tree now uses LineRenderer with color theming from TraitData.colorTheme")]
public class ConnectionSprites
{
    [Header("Connection Sprites")]
    [Tooltip("Vertical line connection")]
    public Sprite vertical;
    
    [Tooltip("Horizontal line connection")]
    public Sprite horizontal;
    
    [Tooltip("Angled connection (bottom-left to top-right)")]
    public Sprite angleBottomLeftTopRight;
    
    [Tooltip("Angled connection (bottom-right to top-left)")]
    public Sprite angleBottomRightTopLeft;
    
    [Tooltip("3-way T-junction (horizontal with vertical up)")]
    public Sprite threeWayUp;
    
    [Tooltip("3-way T-junction (horizontal with vertical down)")]
    public Sprite threeWayDown;
    
    [Tooltip("3-way T-junction (vertical with horizontal right)")]
    public Sprite threeWayRight;
    
    [Tooltip("3-way T-junction (vertical with horizontal left)")]
    public Sprite threeWayLeft;
    
    [Tooltip("4-way cross junction")]
    public Sprite fourWayCross;
    
    [Header("Connection Colors")]
    [Tooltip("Color for unlocked connections")]
    public Color unlockedColor = Color.yellow;
    
    [Tooltip("Color for locked connections")]
    public Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
}

/// <summary>
/// OBSOLETE: Enum for connection types
/// NO LONGER USED - Trait tree now uses LineRenderer which doesn't need connection type sprites.
/// This enum is kept for backward compatibility with existing assets.
/// </summary>
[System.Obsolete("ConnectionType is obsolete. Trait tree now uses LineRenderer instead of sprite-based connections")]
public enum ConnectionType
{
    Vertical,
    Horizontal,
    AngleBottomLeftTopRight,
    AngleBottomRightTopLeft,
    ThreeWayUp,
    ThreeWayDown,
    ThreeWayRight,
    ThreeWayLeft,
    FourWayCross
}
