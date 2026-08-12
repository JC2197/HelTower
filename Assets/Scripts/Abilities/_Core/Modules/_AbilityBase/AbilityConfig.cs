using UnityEngine;

/// <summary>
/// Base ability configuration - contains only UI/display data.
/// All mechanical data (cooldowns, energy, charges) is in extending classes like AbilityDataConfig.
/// </summary>
public abstract class AbilityConfig: ScriptableObject
{
    [Header("UI Display")]
    public string abilityName = "Ability";
    public Sprite abilityIcon;
    [TextArea(3, 5)]
    public string abilityDescription = "No description available.";
    
    [Header("Weapon Requirement")]
    [Tooltip("If set, this ability can only be used when the specified weapon is equipped. Leave empty for no requirement.")]
    public WeaponConfig requiredWeapon;
    
    [Header("Ability Tags")]
    public AbilityTagSelector abilityTags = new AbilityTagSelector();
}