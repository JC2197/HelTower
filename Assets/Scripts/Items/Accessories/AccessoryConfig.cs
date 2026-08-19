using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
/// <summary>
/// ScriptableObject that defines an accessory's configuration.
/// Contains the accessory prefab and all positioning/sorting/behavior settings.
/// </summary>
[CreateAssetMenu(fileName = "Accessory_", menuName = "Items/Accessories/Accessory Config")]
public class AccessoryConfig : ScriptableObject
{
    [FormerlySerializedAs("baseTierAvailable")]
    [Tooltip("Advancement level for this accessory (1-6). Used as the minimum rolled gear tier.")]
    [Range(1, 6)]
    public int advancementLevel = 1;

    [Tooltip("Display name of the accessory")]
    public string accessoryname = "Accessory";


    [Header("Trait Grant")]
    [Tooltip("Trait granted when this weapon is equipped (optional)")]
    public TraitData grantedTrait;

    // craftingCost and researchPointCost are inherited from CraftableConfig

    [Header("Accessory Prefab")]
    [Tooltip("Accessory prefab to spawn")]
    public GameObject prefab;


    [Tooltip("Sprite shown in inventory when picked up")]
    public Sprite inventorySprite;

    // treeSprite, treeSpriteColorTag, craftingCost, and researchPointCost are inherited from CraftableConfig

    [Header("Positioning")]
    [Tooltip("Override the accessory type's default positioning with custom values for this accessory")]
    public bool overridePositioning = false;

    [Tooltip("Custom positioning data (only used when overridePositioning is true)")]
    public AccessoryPositioningData Positioning = new AccessoryPositioningData();

    // Convenience accessors so existing code (CanEquipToSlot, inventory, etc.) still compiles
    public Vector2 northEastOffset => Positioning.northEastOffset;
    public Vector2 northWestOffset => Positioning.northWestOffset;
    public Vector2 southEastOffset => Positioning.southEastOffset;
    public Vector2 southWestOffset => Positioning.southWestOffset;


    public bool lockTo2Directions => Positioning.lockTo2Directions;
    public bool flipAccessoryOnTurn => Positioning.flipAccessoryOnTurn;
    public bool flipAccessoryOnYAxis => Positioning.flipAccessoryOnYAxis;
    public bool flipAccessoryOnXAxis => Positioning.flipAccessoryOnXAxis;


    public bool AccessoryBehindOnNE => Positioning.AccessoryBehindOnNE;
    public bool AccessoryBehindOnNW => Positioning.AccessoryBehindOnNW;
    public bool AccessoryBehindOnSE => Positioning.AccessoryBehindOnSE;
    public bool AccessoryBehindOnSW => Positioning.AccessoryBehindOnSW;
    public AbilityConfig grantedPassiveAbility;

    [Header("Animation")]
    [Tooltip("Optional animator controller override applied to the spawned accessory")]
    public RuntimeAnimatorController animatorController;

    public AccessorySettings ToAccessorySettings()
    {
        return new AccessorySettings
        {
            accessoryPrefab = prefab,
            animatorController = animatorController,
            positioning = Positioning
        };
    }
}
