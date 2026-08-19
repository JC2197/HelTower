using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Represents an individual character instance with their own progression, stats, and loadout.
/// References a ClassData for shared class properties.
/// </summary>
[CreateAssetMenu(fileName = "Character_", menuName = "Characters/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Identity")]
    [Tooltip("Unique identifier for this character instance")]
    public string characterName;
    
    [Tooltip("Display name (can be customized by player)")]
    public string displayName;

    [Header("Class Reference")]
    [Tooltip("The class this character belongs to (contains shared appearance, animations, base stats)")]
    public ClassData classData;
    
    [Header("Run State")]
    [Tooltip("Current run level. Starts at 1 each run; no persistent experience.")]
    public int characterLevel = 1;

    [Tooltip("Set true when the character enters a map/arena run.")]
    public bool inMap = false;

    [Header("Stat Containers")]
    [Tooltip("Base stat values (ClassData + level-ups only, never modified by traits)")]
    public StatContainer baseStatContainer;
    
    [Tooltip("Current runtime stat values (base + traits + conversions)")]
    public StatContainer statContainer;

    [Header("Pet Settings")]
    [Tooltip("Pet prefab for this character")]
    public GameObject petPrefab;

    [Tooltip("Which pet to spawn by default")]
    public int defaultPetIndex = 0;

    [Header("Weapon Configuration")]
    [Tooltip("Enable dual-wielding (two weapons, one per hand)")]
    public bool hasDualWeapons = false;
    
    [Tooltip("Main hand weapon config (or only weapon if not dual-wielding)")]
    public WeaponConfig mainHandWeaponConfig;
    
    [Tooltip("Off-hand weapon config (only used if hasDualWeapons is true)")]
    public WeaponConfig offHandWeaponConfig;

    [Header("Accessory Configuration")]
    [Tooltip("Accessories equipped by this character. All are spawned under the single AccessoryHolder.")]
    public List<AccessoryConfig> accessoryConfigs = new List<AccessoryConfig>();

    [Header("Character Stats")]
    [Tooltip("Flag indicating if stats have been initialized with conversions applied (prevents re-initialization on load)")]
    public bool isStatsInitialized = false;

    [Header("Ability Loadouts")]
    [Tooltip("This character's equipped abilities")]
    public CharacterAbilityLoadout abilityLoadout;

    [Header("Run Reward Progression")]
    [Tooltip("Per-run ability levels tracked by the LevelUpRewardDirector. Not persisted between runs.")]
    [SerializeField] private List<AbilityRewardProgression> abilityRewardProgression = new List<AbilityRewardProgression>();

    [Tooltip("Per-run starting weapon progression tracked by the LevelUpRewardDirector. Not persisted between runs.")]
    [SerializeField] private WeaponRewardProgression weaponRewardProgression = new WeaponRewardProgression();
      
    // [Header("Inventory")]
    // [Tooltip("Items stored by slot index (0-31). Null entries represent empty slots.")]
    // public Dictionary<int, ItemInstance> inventorySlots = new Dictionary<int, ItemInstance>();

    
    // [Header("Equipped Gear")]
    // [Tooltip("Currently equipped gear items by slot (Head, Chest, Hands, Feet, Weapon, etc.)")]
    // public Dictionary<GearSlot, ItemInstance> equippedGear = new Dictionary<GearSlot, ItemInstance>();
    
    // Serialization support for dictionaries (Unity can't serialize dictionaries directly)
    // [SerializeField, HideInInspector] private List<int> _inventorySlotKeys = new List<int>();
    // [SerializeField, HideInInspector] private List<ItemInstance> _inventorySlotValues = new List<ItemInstance>();
    // [SerializeField, HideInInspector] private List<GearSlot> _equippedGearKeys = new List<GearSlot>();
    // [SerializeField, HideInInspector] private List<ItemInstance> _equippedGearValues = new List<ItemInstance>();
    
    /// <summary>
    /// Maximum number of inventory slots
    /// </summary>
    public const int MAX_INVENTORY_SLOTS = 32;
    
    /// <summary>
    /// Get item at specific slot index
    /// </summary>
    // public ItemInstance GetItemAtSlot(int slotIndex)
    // {
    //     if (slotIndex < 0 || slotIndex >= MAX_INVENTORY_SLOTS) return null;
    //     return inventorySlots.ContainsKey(slotIndex) ? inventorySlots[slotIndex] : null;
    // }
    
    // /// <summary>
    // /// Set item at specific slot index
    // /// </summary>
    // public void SetItemAtSlot(int slotIndex, ItemInstance item)
    // {
    //     if (slotIndex < 0 || slotIndex >= MAX_INVENTORY_SLOTS) return;
        
    //     if (item == null)
    //     {
    //         inventorySlots.Remove(slotIndex);
    //     }
    //     else
    //     {
    //         inventorySlots[slotIndex] = item;
    //     }
    // }
    
    /// <summary>
    /// Find first empty slot index
    /// </summary>
    // public int FindEmptySlot()
    // {
    //     return ItemSlotStackingUtility.FindFirstEmptySlot(inventorySlots, MAX_INVENTORY_SLOTS);
    // }
    
    // /// <summary>
    // /// Add item to inventory. Stackable items merge into an existing matching stack
    // /// (up to ItemInstance.MAX_STACK_SIZE) before consuming a new slot.
    // /// </summary>
    // public bool AddItemToInventory(ItemInstance item)
    // {
    //     return ItemSlotStackingUtility.AddItemToSlots(inventorySlots, MAX_INVENTORY_SLOTS, item);
    // }
    
    // /// <summary>
    // /// Remove item from specific slot
    // /// </summary>
    // public bool RemoveItemFromSlot(int slotIndex)
    // {
    //     if (inventorySlots.ContainsKey(slotIndex))
    //     {
    //         inventorySlots.Remove(slotIndex);
    //         return true;
    //     }
    //     return false;
    // }

    public int CountMaterial(string materialId)
    {
        return 0;
    }

    public int ConsumeMaterial(string materialId, int amount)
    {
        return 0;
    }

    /// <summary>
    /// Get class data or log warning if missing
    /// </summary>
    public ClassData GetClassData()
    {
        if (classData == null)
        {
            Debug.LogWarning($"[CharacterData] Character '{characterName}' has no ClassData assigned!");
        }
        return classData;
    }
    
    // Convenience accessors for class properties
    public RuntimeAnimatorController GetAnimatorController() => classData != null ? classData.animatorController : null;

    public List<AbilityRewardProgression> AbilityRewardProgressionList
    {
        get
        {
            abilityRewardProgression ??= new List<AbilityRewardProgression>();
            return abilityRewardProgression;
        }
    }
    
    /// <summary>
    /// Find an ability's reward progression record without creating one if it doesn't exist.
    /// Returns null if the ability is not tracked.
    /// </summary>
    public AbilityRewardProgression FindAbilityRewardProgression(AbilityConfig abilityConfig)
    {
        if (abilityConfig == null || abilityRewardProgression == null)
            return null;
        
        string abilityID = abilityConfig.name;
        string abilityName = abilityConfig.abilityName;
        
        return abilityRewardProgression.Find(record =>
            (!string.IsNullOrEmpty(abilityID) && string.Equals(record.abilityID, abilityID, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(abilityName) && string.Equals(record.abilityName, abilityName, StringComparison.OrdinalIgnoreCase)));
    }

    public WeaponRewardProgression WeaponRewardProgression
    {
        get
        {
            weaponRewardProgression ??= new WeaponRewardProgression();
            return weaponRewardProgression;
        }
    }

    public void ResetRunRewardProgression()
    {
        abilityRewardProgression = new List<AbilityRewardProgression>();
        weaponRewardProgression = new WeaponRewardProgression();
    }

    public AbilityRewardProgression EnsureAbilityRewardProgression(AbilityConfig abilityConfig, int minimumLevel = 1)
    {
        if (abilityConfig == null)
            return null;

        string abilityID = abilityConfig.name;
        string abilityName = string.IsNullOrEmpty(abilityConfig.abilityName) ? abilityConfig.name : abilityConfig.abilityName;
        return EnsureAbilityRewardProgression(abilityID, abilityName, minimumLevel);
    }

    public AbilityRewardProgression EnsureAbilityRewardProgression(string abilityID, string abilityName, int minimumLevel = 1)
    {
        if (string.IsNullOrEmpty(abilityID) && string.IsNullOrEmpty(abilityName))
            return null;

        abilityRewardProgression ??= new List<AbilityRewardProgression>();

        AbilityRewardProgression existing = abilityRewardProgression.Find(record =>
            (!string.IsNullOrEmpty(abilityID) && string.Equals(record.abilityID, abilityID, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(abilityName) && string.Equals(record.abilityName, abilityName, StringComparison.OrdinalIgnoreCase)));

        if (existing != null)
        {
            existing.abilityID = string.IsNullOrEmpty(existing.abilityID) ? abilityID : existing.abilityID;
            existing.abilityName = string.IsNullOrEmpty(existing.abilityName) ? abilityName : existing.abilityName;
            existing.level = Mathf.Max(existing.level, minimumLevel);
            existing.contributingNodeIDs ??= new List<string>();
            return existing;
        }

        AbilityRewardProgression created = new AbilityRewardProgression
        {
            abilityID = abilityID,
            abilityName = abilityName,
            level = Mathf.Max(0, minimumLevel),
            contributingNodeIDs = new List<string>()
        };

        abilityRewardProgression.Add(created);
        return created;
    }
    
    // ISerializationCallbackReceiver implementation - Unity dictionaries need manual serialization
    // public void OnBeforeSerialize()
    // {
    //     // Convert dictionaries to lists for Unity serialization
    //     _inventorySlotKeys.Clear();
    //     _inventorySlotValues.Clear();
    //     foreach (var kvp in inventorySlots)
    //     {
    //         _inventorySlotKeys.Add(kvp.Key);
    //         _inventorySlotValues.Add(kvp.Value);
    //     }
        
    //     _equippedGearKeys.Clear();
    //     _equippedGearValues.Clear();
    //     foreach (var kvp in equippedGear)
    //     {
    //         _equippedGearKeys.Add(kvp.Key);
    //         _equippedGearValues.Add(kvp.Value);
    //     }
    // }
    
    // public void OnAfterDeserialize()
    // {
    //     // Convert lists back to dictionaries after Unity deserialization
    //     inventorySlots = new Dictionary<int, ItemInstance>();
    //     for (int i = 0; i < Mathf.Min(_inventorySlotKeys.Count, _inventorySlotValues.Count); i++)
    //     {
    //         inventorySlots[_inventorySlotKeys[i]] = _inventorySlotValues[i];
    //     }
        
    //     equippedGear = new Dictionary<GearSlot, ItemInstance>();
    //     for (int i = 0; i < Mathf.Min(_equippedGearKeys.Count, _equippedGearValues.Count); i++)
    //     {
    //         equippedGear[_equippedGearKeys[i]] = _equippedGearValues[i];
    //     }
    // }
}

[System.Serializable]
public class AbilityRewardProgression
{
    public string abilityID;
    public string abilityName;
    public int level;
    public int upgradeCount;
    public List<string> contributingNodeIDs = new List<string>();
    public List<string> takenUpgradeNodeIDs = new List<string>();

    public bool HasContribution(string nodeID)
    {
        return !string.IsNullOrEmpty(nodeID)
            && contributingNodeIDs != null
            && contributingNodeIDs.Exists(existing => string.Equals(existing, nodeID, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasUpgrade(string nodeID)
    {
        return !string.IsNullOrEmpty(nodeID)
            && takenUpgradeNodeIDs != null
            && takenUpgradeNodeIDs.Exists(existing => string.Equals(existing, nodeID, StringComparison.OrdinalIgnoreCase));
    }

    public bool CanTakeUpgrade(int levelsPerUpgrade, int maxUpgrades)
    {
        if (levelsPerUpgrade <= 0)
            return maxUpgrades <= 0 || upgradeCount < maxUpgrades;

        if (maxUpgrades > 0 && upgradeCount >= maxUpgrades)
            return false;

        int unlockedUpgradeSlots = level / levelsPerUpgrade;
        return unlockedUpgradeSlots > upgradeCount;
    }
}

[System.Serializable]
public class WeaponRewardProgression
{
    public string weaponID;
    public string weaponName;
    public string weaponType;
    public int level;
    public int upgradeCount;
    public List<string> contributingNodeIDs = new List<string>();
    public List<string> takenUpgradeNodeIDs = new List<string>();

    public bool HasContribution(string nodeID)
    {
        return !string.IsNullOrEmpty(nodeID)
            && contributingNodeIDs != null
            && contributingNodeIDs.Exists(existing => string.Equals(existing, nodeID, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasUpgrade(string nodeID)
    {
        return !string.IsNullOrEmpty(nodeID)
            && takenUpgradeNodeIDs != null
            && takenUpgradeNodeIDs.Exists(existing => string.Equals(existing, nodeID, StringComparison.OrdinalIgnoreCase));
    }

    public bool CanTakeUpgrade(int levelsPerUpgrade, int maxUpgrades)
    {
        if (levelsPerUpgrade <= 0)
            return maxUpgrades <= 0 || upgradeCount < maxUpgrades;

        if (maxUpgrades > 0 && upgradeCount >= maxUpgrades)
            return false;

        int unlockedUpgradeSlots = level / levelsPerUpgrade;
        return unlockedUpgradeSlots > upgradeCount;
    }
}

[System.Serializable]
public class WeaponSettings
{
    public GameObject weaponPrefab;
    
    [Header("Weapon Position - Per Direction")]
    [Tooltip("Distance from player center to weapon pivot point")]
    public float aimingRadius = 0.3f;
    
    [Tooltip("Weapon offset when facing North East")]
    public Vector2 northEastOffset = Vector2.zero;
    [Tooltip("Weapon offset when facing North West")]
    public Vector2 northWestOffset = Vector2.zero;
    [Tooltip("Weapon offset when facing South East")]
    public Vector2 southEastOffset = Vector2.zero;
    [Tooltip("Weapon offset when facing South West")]
    public Vector2 southWestOffset = Vector2.zero;
    
    [Header("Aiming Mode")]
    [Tooltip("Lock aiming to 2 cardinal directions (E, W) instead of 360 degrees. When enabled, weapon uses sprite flips instead of rotation.")]
    public bool lockTo2Directions = false;
    
    [Header("Weapon Sprite Flipping")]
    [Tooltip("Enable weapon sprite flipping when aiming left to prevent upside-down appearance")]
    public bool flipWeaponOnTurn = false;
    [Tooltip("Flip on Y axis when shouldFlip is true")]
    public bool flipWeaponOnYAxis = false;
    [Tooltip("Flip on X axis when shouldFlip is true")]
    public bool flipWeaponOnXAxis = false;

    [Header("Weapon Sorting Order - Diagonal Directions Only")]
    [Tooltip("Weapon renders behind player when moving NorthEast")]
    public bool weaponBehindOnNE = true;
    [Tooltip("Weapon renders behind player when moving NorthWest")]
    public bool weaponBehindOnNW = true;
    [Tooltip("Weapon renders behind player when moving SouthEast")]
    public bool weaponBehindOnSE = false;
    [Tooltip("Weapon renders behind player when moving SouthWest")]
    public bool weaponBehindOnSW = false;
    
    [Header("Offhand Sorting Order")]
    [Tooltip("Weapon renders behind player when moving NorthEast")]
    public bool offhandWeaponBehindOnNE = true;
    [Tooltip("Weapon renders behind player when moving NorthWest")]
    public bool offhandWeaponBehindOnNW = true;
    [Tooltip("Weapon renders behind player when moving SouthEast")]
    public bool offhandWeaponBehindOnSE = false;
    [Tooltip("Weapon renders behind player when moving SouthWest")]
    public bool offhandWeaponBehindOnSW = false;
    
    [Tooltip("HandHolder (hand sprite) renders behind weapon when moving NorthEast")]
    public bool handBehindOnNE = false;
    [Tooltip("HandHolder (hand sprite) renders behind weapon when moving NorthWest")]
    public bool handBehindOnNW = false;
    [Tooltip("HandHolder (hand sprite) renders behind weapon when moving SouthEast")]
    public bool handBehindOnSE = false;
    [Tooltip("HandHolder (hand sprite) renders behind weapon when moving SouthWest")]
    public bool handBehindOnSW = false;
    [Tooltip("HandHolder Rotation offset relative to the weapon")]
    public  float handRotationOffset = 0f;

    [Header("Weapon Animation (Bow, Staff, etc.)")]
    [Tooltip("Does weapon have attack animations (e.g., bow draw/shoot)?")]
    public bool hasWeaponAnimation = false;
    [Tooltip("Weapon animation trigger name for attacks")]
    public string weaponAttackTrigger = "Shoot";
    [Tooltip("Animator Controller or Override Controller for this weapon variant. Use base controller for first variant, Override Controllers for sprite swaps.")]
    public RuntimeAnimatorController animatorController;
    
    public int weaponDamageMin;
    public int weaponDamageMax;
    public string weaponDamageType = "Physical";

   
}

[System.Serializable]
public class AbilityReference
{
    [Header("Ability Configuration")]
    [SerializeField] private AbilityConfig abilityConfig;

    public AbilityConfig Config => abilityConfig;
    public string AbilityName => abilityConfig != null ? abilityConfig.abilityName : "None";

    // All abilities use DataDrivenAbility - no need to store script reference
    public System.Type AbilityType => typeof(DataDrivenAbility);

    // Constructor for creating AbilityReferences at runtime
    public AbilityReference(AbilityConfig config)
    {
        abilityConfig = config;
    }
}

[System.Serializable]
public class CharacterAbilityLoadout
{
    [Header("Core Abilities")]
    [Tooltip("Weapon-granted ability (LMB). Set automatically when weapon is equipped.")]
    [SerializeField] private AbilityReference weaponAbility;
    
    [FormerlySerializedAs("dashAbility")]
    [Tooltip("Secondary weapon ability (RMB). Set automatically when an offhand weapon is equipped.")]
    [SerializeField] private AbilityReference secondaryWeaponAbility;
    
    [Header("Trait Abilities")]
    [Tooltip("Abilities granted from traits. Passives/autocasts run automatically; actives get dynamic keybinds (1,2,3...).")]
    [SerializeField] private List<AbilityReference> traitAbilities = new List<AbilityReference>();

    [Header("Triggered Abilities (Hidden — no UI slot)")]
    [Tooltip("Abilities that are only ever fired by on-hit EffectData triggers (isTriggeredOnly = true). " +
             "Listed here so trait modifiers can target them per-character. No icon or keybind is created.")]
    [SerializeField] private List<AbilityReference> triggeredAbilities = new List<AbilityReference>();

    // === Properties ===
    public AbilityReference WeaponAbility => weaponAbility;
    public AbilityReference SecondaryWeaponAbility => secondaryWeaponAbility;
    public List<AbilityReference> TraitAbilities => traitAbilities ?? new List<AbilityReference>();
    public List<AbilityReference> TriggeredAbilities => triggeredAbilities ?? new List<AbilityReference>();

    /// <summary>
    /// Returns only the trait abilities that require manual keybind activation (not autocast, not aura).
    /// </summary>
    public List<AbilityReference> GetActiveTraitAbilities()
    {
        var actives = new List<AbilityReference>();
        foreach (var abilityRef in TraitAbilities)
        {
            if (abilityRef?.Config is AbilityDataConfig dataConfig && dataConfig.RequiresKeybind)
                actives.Add(abilityRef);
        }
        return actives;
    }

    /// <summary>
    /// Returns trait abilities that are passive (auras) or autocast.
    /// </summary>
    public List<AbilityReference> GetPassiveTraitAbilities()
    {
        var passives = new List<AbilityReference>();
        foreach (var abilityRef in TraitAbilities)
        {
            if (abilityRef?.Config is AbilityDataConfig dataConfig && !dataConfig.RequiresKeybind)
                passives.Add(abilityRef);
        }
        return passives;
    }

    /// <summary>
    /// Set the weapon ability (called when weapons are equipped).
    /// </summary>
    public void SetWeaponAbility(AbilityConfig config)
    {
        weaponAbility = config != null ? new AbilityReference(config) : null;
    }

    /// <summary>
    /// Set the secondary weapon ability.
    /// </summary>
    public void SetSecondaryWeaponAbility(AbilityConfig config)
    {
        secondaryWeaponAbility = config != null ? new AbilityReference(config) : null;
    }

    /// <summary>
    /// Add a trait ability.
    /// </summary>
    public void AddTraitAbility(AbilityConfig config)
    {
        if (config == null) return;
        traitAbilities ??= new List<AbilityReference>();
        traitAbilities.Add(new AbilityReference(config));
    }

    /// <summary>
    /// Remove a trait ability by config.
    /// </summary>
    public bool RemoveTraitAbility(AbilityConfig config)
    {
        if (config == null || traitAbilities == null) return false;
        return traitAbilities.RemoveAll(r => r?.Config == config) > 0;
    }

    /// <summary>
    /// Clear all trait abilities.
    /// </summary>
    public void ClearTraitAbilities()
    {
        traitAbilities?.Clear();
    }

    /// <summary>
    /// Add a triggered ability (isTriggeredOnly = true). These never get UI slots
    /// but their modifiers still resolve per-character via the modifier system.
    /// </summary>
    public void AddTriggeredAbility(AbilityConfig config)
    {
        if (config == null) return;
        triggeredAbilities ??= new List<AbilityReference>();
        triggeredAbilities.Add(new AbilityReference(config));
    }

    /// <summary>
    /// Remove a triggered ability by config reference.
    /// </summary>
    public bool RemoveTriggeredAbility(AbilityConfig config)
    {
        if (config == null || triggeredAbilities == null) return false;
        return triggeredAbilities.RemoveAll(r => r?.Config == config) > 0;
    }

    /// <summary>
    /// Find a triggered ability by its AbilityDataConfig ScriptableObject reference.
    /// Returns null if this character does not have that ability in their triggered slot.
    /// </summary>
    public AbilityDataConfig FindTriggeredAbility(AbilityDataConfig config)
    {
        if (config == null || triggeredAbilities == null) return null;
        foreach (var abilityRef in triggeredAbilities)
        {
            if (abilityRef?.Config is AbilityDataConfig dataConfig && dataConfig == config)
                return dataConfig;
        }
        return null;
    }
}

[System.Serializable]
public class FootstepParticleSettings
{
    [Tooltip("ParticleSystem prefab for footstep effects")]
    public ParticleSystem particlesPrefab;
    
    [Tooltip("Position offset from character center")]
    public Vector2 offset = new Vector2(0f, -0.5f);
    
    [Tooltip("Animation samples per second (FPS)")]
    public int animationSamplesPerSecond = 12;
    
    [Tooltip("Total frames in walk animation cycle")]
    public int animationTotalFrames = 16;
    
    [Tooltip("Frames between footstep particle spawns")]
    public int framesPerStep = 8;
    
    [Tooltip("Number of particles per footstep")]
    public int particlesPerFootstep = 3;
    public AudioClip footstepSound;
}

[System.Serializable]
public class CustomProperty
{
    public string propertyName;
    public float value;
    public string description;
}
