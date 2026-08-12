using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single piece of gear. Designer-authored, fixed values (no slots, tiers, or rarity).
/// While owned, gear grants stat modifiers and/or traits to the player.
/// Acquisition (drops, pickups, rewards) is handled elsewhere and intentionally not part of this asset.
/// </summary>
[CreateAssetMenu(fileName = "New Gear", menuName = "Gear/Gear Item")]
public class GearItem : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique identifier for this gear.")]
    public string gearID;

    [Tooltip("Display name shown to players.")]
    public string displayName = "New Gear";

    [TextArea(2, 4)]
    [Tooltip("Description of what this gear does.")]
    public string description;

    [Tooltip("Icon displayed for this gear.")]
    public Sprite icon;

    [Header("Stat Modifiers")]
    [Tooltip("Flat/percentage stat modifiers granted while this gear is owned.")]
    public List<StatModifier> statModifiers = new List<StatModifier>();

    [Header("Granted Traits")]
    [Tooltip("Traits granted while this gear is owned (optional).")]
    public List<TraitData> grantedTraits = new List<TraitData>();

    public bool HasStatModifiers => statModifiers != null && statModifiers.Count > 0;

    public bool HasTraits => grantedTraits != null && grantedTraits.Count > 0;
}
