using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages all traits for a character, including activation, deactivation, and stat calculation.
/// Attach this to your player character GameObject.
/// </summary>
public class CharacterTraitManager : MonoBehaviour
{
    [Header("Character Reference")]
    [Tooltip("Reference to the character's data (auto-assigned from PlayerController if not set)")]
    [SerializeField] private CharacterData characterData;

    [Header("Save File Reference")]
    [Tooltip("Meta progression save file that owns the persisted trait tree nodes.")]
    [SerializeField] private SaveFileData saveFileData;

    [Header("Active Traits")]
    [SerializeField] private List<TraitData> startingTraits = new List<TraitData>();

    [Header("Trait Registry")]
    [Tooltip("Global list of all TraitData assets. Auto-loaded from Resources/TraitDataList if not assigned.")]
    [SerializeField] private TraitDataList traitDataList;

    // Map nodeID -> Trait (allows multiple instances of same trait from different nodes)
    private Dictionary<string, Trait> traitLookupByNode = new Dictionary<string, Trait>();

    // Gear-granted nodes are tracked separately so they are never written to
    // characterData.unlockedNodeIDs (they are always re-derived from equippedGear on load).
    private HashSet<string> _gearNodeIDs = new HashSet<string>();

    // Cached stat modifiers for performance (case-insensitive to match StatContainer)
    private Dictionary<string, float> cachedFlatModifiers = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, float> cachedPercentageModifiers = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);

    // Cached PlayerController reference
    private PlayerController playerController;

    /// <summary>
    /// True when this instance belongs to the local player.
    /// Falls back to true in single-player (no network spawned yet).
    /// </summary>
    private bool IsOwner
    {
        get
        {
            if (playerController != null && playerController.IsSpawned)
                return playerController.IsOwner;
            return true; // single-player or pre-spawn
        }
    }

    // Events
    public System.Action<string, TraitData> OnTraitUnlocked; // (nodeID, traitData)
    public System.Action<string, TraitData> OnTraitRemoved;  // (nodeID, traitData)
    public System.Action OnTraitsChanged;

    /// <summary>
    /// Get the character name from CharacterData
    /// </summary>
    public string characterName
    {
        get
        {
            if (characterData != null)
                return characterData.characterName;

            // Fallback: pull from the cached PlayerController (avoids GetComponent in hot path)
            if (playerController != null)
            {
                characterData = playerController.GetCurrentCharacterData();
                if (characterData != null)
                    return characterData.characterName;
            }

            Debug.LogWarning("[CharacterTraitManager] Could not determine character name!");
            return "Unknown";
        }
    }

    /// <summary>
    /// Set the character data reference (called from PlayerController)
    /// </summary>
    public void SetCharacterData(CharacterData data)
    {
        Debug.Log($"[CharacterTraitManager] ========================================");
        Debug.Log($"[CharacterTraitManager] SetCharacterData called");
        Debug.Log($"[CharacterTraitManager] New data: {(data != null ? data.displayName : "null")}");
        Debug.Log($"[CharacterTraitManager] Previous characterData: {(characterData != null ? characterData.displayName : "null")}");

        // Reset traits if switching to a different character
        if (characterData != null && data != null && characterData.characterName != data.characterName)
        {
            Debug.Log($"[CharacterTraitManager] Character changed from {characterData.characterName} to {data.characterName}, resetting traits");
            ResetAllTraits();
        }

        // Check if we need to load traits (before assigning characterData)
        bool shouldLoadTraits = (characterData == null) || (characterData != data);
        Debug.Log($"[CharacterTraitManager] shouldLoadTraits = {shouldLoadTraits}");

        characterData = data;

        // Trait nodes are meta progression, so they are restored from the save file, not CharacterData.
        // if (data != null && shouldLoadTraits)
        // //     LoadTraitsFromSaveFile();

        Debug.Log($"[CharacterTraitManager] ========================================");
    }

    /// <summary>
    /// Assign the meta progression save file and restore its unlocked trait tree nodes.
    /// Called by PlayerController when a save file is loaded/selected.
    /// </summary>
    public void SetSaveFileData(SaveFileData data)
    {
        if (saveFileData == data)
            return;

        // Switching save files must not carry trait unlocks across.
        // if (saveFileData != null)
        //     ResetAllTraits();

        saveFileData = data;
        // LoadTraitsFromSaveFile();
    }

    /// <summary>Expose the save file this manager persists trait nodes into.</summary>
    public SaveFileData GetSaveFileData() => saveFileData;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (traitDataList == null)
            traitDataList = Resources.Load<TraitDataList>("TraitDataList");
        // Traits are loaded when SetCharacterData is called — not here
    }

    /// <summary>
    /// Unlock and activate a new trait from a specific node.
    /// Updates CharacterData in-memory only — the caller is responsible for saving/broadcasting.
    /// Called both from TraitSystemManager (owner, new unlock) and LoadNodesFromCharacterData
    /// (all instances, trait restoration from CharacterData).
    /// </summary>
    /// <param name="nodeID">The node ID being unlocked</param>
    /// <param name="traitData">The trait to unlock</param>
    /// <param name="isRestoring">True when restoring from saved CharacterData — skips IsOwner guard.</param>
    public bool UnlockTrait(string nodeID, TraitData traitData, bool isRestoring = false)
    {
        // STEP A: Check if this node slot already exists in our dictionary tracking
        if (traitLookupByNode.TryGetValue(nodeID, out Trait existingTrait))
        {
            // The node is already in the dictionary! This means the player is leveling it up.
            if (!existingTrait.LevelTrait())
            {
                return false; // Already reached max level, exit out safely.
            }

            // Successfully leveled up! Recalculate stats and exit.
            RecalculateModifiers();
            return true;
        }

        // STEP B: If the nodeID isn't in the dictionary, it's a brand new unlock.
        Trait newTrait = new Trait(traitData);
        newTrait.Activate(gameObject);

        // STEP C: Register the runtime instance directly into the dictionary.
        // The node ID is permanently linked to this specific instance tracking level and states.
        traitLookupByNode[nodeID] = newTrait;

        RecalculateModifiers();
        return true;
    }

    /// <summary>
    /// Check if a specific node is unlocked
    /// </summary>
    public bool IsNodeUnlocked(string nodeID)
    {
        return !string.IsNullOrEmpty(nodeID) && (traitLookupByNode.ContainsKey(nodeID) || _gearNodeIDs.Contains(nodeID));
    }

    /// <summary>
    /// Remove a trait by node ID (for respec)
    /// </summary>
    public bool RemoveTraitByNode(string nodeID)
    {
        if (string.IsNullOrEmpty(nodeID) || !traitLookupByNode.ContainsKey(nodeID))
            return false;

        Trait trait = traitLookupByNode[nodeID];
        trait.Deactivate(gameObject);
        traitLookupByNode.Remove(nodeID);
        _gearNodeIDs.Remove(nodeID);

        RecalculateModifiers();

        OnTraitRemoved?.Invoke(nodeID, trait.data);
        OnTraitsChanged?.Invoke();

        // Belt-and-suspenders: directly tell PlayerController to recalculate
        playerController?.RequestStatsRecalculation();

        // Update the save file in-memory — caller handles save + network broadcast
        // UpdateSaveFileTraitList();

        return true;
    }

    /// <summary>
    /// Get all unlocked node IDs
    /// </summary>
    public HashSet<string> GetUnlockedNodeIDs()
    {
        return new HashSet<string>(traitLookupByNode.Keys);
    }

    /// <summary>
    /// True when the save file holds enough gold to unlock this node. Free nodes always pass.
    /// </summary>
    public bool CanAffordNode(TraitNode node)
    {
        if (node == null)
            return false;

        int cost = GetTraitGoldCost(node);
        if (cost <= 0)
        {
            Debug.Log(
                $"[CharacterTraitManager] Node {node.nodeID} is free to unlock."
            );

            return true;
        }
        int playerGold = saveFileData != null ? saveFileData.totalGold : 0;
        return playerGold >= cost;
    }

    /// <summary>
    /// Expose the authoritative CharacterData reference held by this manager.
    /// </summary>
    public CharacterData GetCharacterData() => characterData;

    /// <summary>
    /// Remove a trait (for respec) - removes ALL instances of this trait
    /// </summary>
    public bool RemoveTrait(TraitData traitData)
    {
        if (traitData == null)
            return false;

        bool removedAny = false;
        var nodesToRemove = new List<string>();

        // Find all nodes with this trait
        foreach (var kvp in traitLookupByNode)
        {
            if (kvp.Value.data == traitData)
            {
                nodesToRemove.Add(kvp.Key);
            }
        }

        // Remove each instance
        foreach (var nodeID in nodesToRemove)
        {
            RemoveTraitByNode(nodeID);
            removedAny = true;
        }

        return removedAny;
    }

    /// <summary>
    /// Check if a trait is currently active (checks if ANY instance exists)
    /// </summary>
    public bool HasTrait(TraitData traitData)
    {
        if (traitData == null) return false;

        // LINQ checks every running instance inside the dictionary values column
        return traitLookupByNode.Values.Any(t => t.data == traitData && t.isActive);
    }

    /// <summary>
    /// Get the number of active instances of a specific trait
    /// </summary>
    public int GetTraitInstanceCount(TraitData traitData)
    {
        if (traitData == null) return 0;
        return traitLookupByNode.Values.Count(t => t.data == traitData && t.isActive);
    }

    public int GetTotalTraitLevels()
    {
        int totalLevels = 0;

        foreach (Trait trait in traitLookupByNode.Values)
        {
            if (trait != null && trait.isActive)
                totalLevels += trait.level;
        }

        return totalLevels;
    }

    public int GetTraitGoldCost(TraitNode node)
    {
        if (node == null || node.traitData == null)
            return 0;

        int totalTraitLevels = GetTotalTraitLevels();

        return TraitUtils.GetGoldCost(
            node,
            totalTraitLevels
        );
    }

    /// <summary>
    /// Get all active traits
    /// </summary>
    public List<TraitData> GetActiveTraitData()
    {
        return traitLookupByNode.Values.Where(t => t.isActive).Select(t => t.data).ToList();
    }

    /// <summary>
    /// Collect all trait tags from active traits with their frequencies.
    /// Returns a dictionary of tag -> count (how many times that tag appears).
    /// Used for weighting future trait rolls based on current build synergies.
    /// </summary>
    public Dictionary<string, int> GetTraitTagCollection()
    {
        Dictionary<string, int> tagCounts = new Dictionary<string, int>();
        return tagCounts;
    }

    // /// <summary>
    // /// Mirror the runtime unlocked node set into the save file (meta progression).
    // /// In-memory only — the caller is responsible for persisting and broadcasting.
    // /// </summary>
    // private void UpdateSaveFileTraitList()
    // {
    //     if (saveFileData == null)
    //         return;

    //     saveFileData.SetUnlockedNodes(unlockedNodeIDs);
    //     Debug.Log($"[CharacterTraitManager] Trait list updated: {activeTraits.Count} traits, {unlockedNodeIDs.Count} nodes persisted to '{saveFileData.saveFileName}'.");
    // }

    /// <summary>
    /// Restore the save file's persisted trait tree nodes. Each node is resolved via the
    /// save file's trait tree, falling back to TraitDataList by traitID (with any "_N" stack
    /// suffix stripped). Gear-granted node IDs are skipped — they are always re-derived from gear.
    /// </summary>
    // private void LoadTraitsFromSaveFile()
    // {
    //     if (saveFileData == null || saveFileData.unlockedNodeIDs == null || saveFileData.unlockedNodeIDs.Count == 0)
    //         return;

    //     // Copy first: UnlockTrait writes back into the save file via UpdateSaveFileTraitList.
    //     List<string> savedNodeIDs = new List<string>(saveFileData.unlockedNodeIDs);
    //     int restored = 0;

    //     foreach (string nodeID in savedNodeIDs)
    //     {
    //         // Gear nodes are re-derived from equipped gear, never restored from the save file.
    //         if (string.IsNullOrEmpty(nodeID) || SaveFileData.IsGearNode(nodeID) || unlockedNodeIDs.Contains(nodeID))
    //             continue;

    //         TraitData traitData = ResolveTraitForNode(nodeID);
    //         if (traitData == null)
    //         {
    //             Debug.LogWarning($"[CharacterTraitManager] Saved node '{nodeID}' has no matching TraitData — skipping.");
    //             continue;
    //         }

    //         if (UnlockTrait(nodeID, traitData, isRestoring: true))
    //             restored++;
    //     }

    //     Debug.Log($"[CharacterTraitManager] Restored {restored}/{savedNodeIDs.Count} trait nodes from save file '{saveFileData.saveFileName}'.");
    // }

    /// <summary>
    /// Resolve the TraitData for a saved node ID: search every trait tree the equipped class
    /// exposes first, then fall back to the global TraitDataList by traitID (stripping any
    /// "_N" stack suffix left by the trait roller).
    /// </summary>
    private TraitData ResolveTraitForNode(string nodeID)
    {
        ClassData classData = characterData != null ? characterData.GetClassData() : null;
        if (classData?.availableTraitTrees != null)
        {
            foreach (TraitTree tree in classData.availableTraitTrees)
            {
                TraitNode node = tree?.nodes?.FirstOrDefault(n => n != null && n.nodeID == nodeID);
                if (node?.traitData != null)
                    return node.traitData;
            }
        }

        if (traitDataList == null)
            traitDataList = Resources.Load<TraitDataList>("TraitDataList");
        if (traitDataList == null)
            return null;

        string traitID = StripStackSuffix(nodeID);
        return traitDataList.AllTraits.FirstOrDefault(t => t != null && t.traitID == traitID);
    }

    /// <summary>Strip the trailing "_N" the trait roller appends when stacking a trait.</summary>
    private static string StripStackSuffix(string nodeID)
    {
        int separator = nodeID.LastIndexOf('_');
        if (separator <= 0 || separator == nodeID.Length - 1)
            return nodeID;

        return int.TryParse(nodeID.Substring(separator + 1), out _) ? nodeID.Substring(0, separator) : nodeID;
    }
    /// <summary>
    /// Get all active runtime traits
    /// </summary>
    public IEnumerable<Trait> GetActiveRuntimeTraits()
    {
        return traitLookupByNode.Values.Where(t => t.isActive);
    }
    /// <summary>
    /// Recalculate all stat modifiers from traits
    /// </summary>
    private void RecalculateModifiers()
    {

        cachedFlatModifiers.Clear();
        cachedPercentageModifiers.Clear();

        // Track how many instances of each trait we have for stacking info
        Dictionary<string, int> traitInstanceCounts = new Dictionary<string, int>();

        foreach (var trait in traitLookupByNode.Values)
        {
            if (!trait.isActive)
            {
                Debug.Log($"[CharacterTraitManager] Skipping inactive trait: {trait.data.displayName}");
                continue;
            }

            // Count trait instances
            string traitID = trait.data.traitID;
            if (!traitInstanceCounts.ContainsKey(traitID))
                traitInstanceCounts[traitID] = 0;
            traitInstanceCounts[traitID]++;

            int traitLevel = Mathf.Max(1, trait.level);
            foreach (var modifier in trait.data.statModifiers)
            {
                // No trait scaling — use the modifier value directly.
                float scaledValue = modifier.value * traitLevel;
                float previousValue = 0f;

                switch (modifier.modifierType)
                {
                    case TraitModifierType.Flat:
                        if (cachedFlatModifiers.ContainsKey(modifier.statID))
                            previousValue = cachedFlatModifiers[modifier.statID];
                        else
                            cachedFlatModifiers[modifier.statID] = 0f;

                        cachedFlatModifiers[modifier.statID] += scaledValue;
                        Debug.Log($"[CharacterTraitManager]   FLAT {modifier.statID}: {previousValue} + {scaledValue} = {cachedFlatModifiers[modifier.statID]}");
                        break;

                    case TraitModifierType.Percentage:
                        if (cachedPercentageModifiers.ContainsKey(modifier.statID))
                            previousValue = cachedPercentageModifiers[modifier.statID];
                        else
                            cachedPercentageModifiers[modifier.statID] = 0f;

                        cachedPercentageModifiers[modifier.statID] += scaledValue;
                        Debug.Log($"[CharacterTraitManager]   PERCENT {modifier.statID}: {previousValue}% + {scaledValue}% = {cachedPercentageModifiers[modifier.statID]}%");
                        break;
                }
            }
        }

        // Log summary of stacked traits
        Debug.Log($"[CharacterTraitManager] ---------- STACKING SUMMARY ----------");
        foreach (var kvp in traitInstanceCounts)
        {
            if (kvp.Value > 1)
            {
                Debug.Log($"[CharacterTraitManager] STACKED: {kvp.Key} x{kvp.Value} instances");
            }
        }

        // Log final totals
        Debug.Log($"[CharacterTraitManager] ---------- FINAL TOTALS ----------");
        if (cachedFlatModifiers.Count > 0)
        {
            foreach (var kvp in cachedFlatModifiers)
            {
                Debug.Log($"[CharacterTraitManager]   TOTAL FLAT {kvp.Key}: +{kvp.Value}");
            }
        }
        if (cachedPercentageModifiers.Count > 0)
        {
            foreach (var kvp in cachedPercentageModifiers)
            {
                Debug.Log($"[CharacterTraitManager]   TOTAL PERCENT {kvp.Key}: +{kvp.Value}%");
            }
        }
        Debug.Log($"[CharacterTraitManager] ========================================");

        // Rebuild weapon ammo modifiers on all active abilities
        foreach (DataDrivenAbility ability in GetComponents<DataDrivenAbility>())
        {
            ability.RebuildAmmoModifiers();
            ability.RebuildConfigModifiers();
        }

        // Rebuild aura configs so persistent auras pick up trait modifier changes
        PlayerAuraManager auraManager = GetComponent<PlayerAuraManager>();
        if (auraManager != null)
        {
            auraManager.RebuildAuraModifiers();
        }
    }

    /// <summary>
    /// Get the total flat modifier for a stat
    /// </summary>
    public float GetFlatModifier(string statID)
    {
        return cachedFlatModifiers.ContainsKey(statID) ? cachedFlatModifiers[statID] : 0f;
    }

    /// <summary>
    /// Get the total percentage modifier for a stat (additive)
    /// </summary>
    public float GetPercentageModifier(string statID)
    {
        return cachedPercentageModifiers.ContainsKey(statID) ? cachedPercentageModifiers[statID] : 0f;
    }



    /// <summary>
    /// Calculate final stat value with all modifiers applied
    /// 
    /// For ABSOLUTE stats (Health, Armor, etc.):
    ///   Formula: (baseValue + flat) * (1 + percentage/100) * more
    ///   - Flat adds absolute amount (Flat 5 = +5 health)
    ///   - Percent is multiplicative (Percent 20 = +20% more health)
    /// 
    /// For PERCENTAGE stats (AttackSpeed, CritChance, etc.):
    ///   Formula: (baseValue + flat/100) * (1 + percentage/100) * more
    ///   - Flat adds percentage points (Flat 15 = +15% = +0.15)
    ///   - Percent is multiplicative (Percent 20 = +20% more)
    /// </summary>
    public float CalculateFinalStat(string statID, float baseValue)
    {
        float flat = GetFlatModifier(statID);
        float percentage = GetPercentageModifier(statID);

        float finalValue;

        // Check if this is a percentage-based stat
        if (IsPercentageStat(statID))
        {
            // Percentage stats: flat is divided by 100 (15 becomes 0.15)
            finalValue = (baseValue + flat / 100f) * (1f + percentage / 100f);

            if (flat != 0f || percentage != 0f)
            {
                Debug.Log($"[CharacterTraitManager] {statID} (percentage): base={baseValue}, flat={flat}% (+{flat / 100f}), percent={percentage}% (x{1f + percentage / 100f}), final={finalValue}");
            }
        }
        else
        {
            // Absolute stats: flat is added as-is
            finalValue = (baseValue + flat) * (1f + percentage / 100f);

            if (flat != 0f || percentage != 0f)
            {
                Debug.Log($"[CharacterTraitManager] {statID} (absolute): base={baseValue}, flat={flat}, percent={percentage}% (x{1f + percentage / 100f}), final={finalValue}");
            }
        }

        return finalValue;
    }

    /// <summary>
    /// Determine if a stat uses percentage-based calculations (flat/100) or absolute values (flat as-is)
    /// </summary>
    private bool IsPercentageStat(string statID)
    {
        // Check StatTypeDatabase for the isPercentage flag
        var statDB = StatTypeDatabase.Instance;
        if (statDB != null)
        {
            if (statDB.TryGetStatType(statID, out var statType) && statType != null)
            {
                return statType.IsPercentage;
            }
        }

        // Fallback: use string pattern matching for unknown stats
        string lowerID = statID.ToLower();
        return lowerID.Contains("speed") ||
               lowerID.Contains("crit") ||
               lowerID.Contains("dodge") ||
               lowerID.Contains("block") ||
               lowerID.Contains("lifesteal") ||
               lowerID.Contains("resistance") ||
               lowerID.Contains("damagebonus") ||
               lowerID.Contains("reduction") ||
               lowerID.Contains("chance") ||
               lowerID.Contains("rate") ||
               lowerID.Contains("regen") ||
               lowerID.Contains("distance");
    }

    /// <summary>
    /// Check if any trait replaces a specific ability
    /// </summary>
    public AbilityConfig GetAbilityReplacement(string abilityName)
    {
        return traitLookupByNode.Values
        .Where(t => t.isActive &&
        t.data.abilityReplacement?.requiredAbility != null &&
        t.data.abilityReplacement.requiredAbility.abilityName == abilityName)
            .Select(t => t.data.abilityReplacement.newAbilityConfig)
                .FirstOrDefault();
    }

    /// <summary>
    /// Check if any trait replaces a specific ability (by AbilityConfig reference)
    /// </summary>
    public AbilityConfig GetAbilityReplacement(AbilityConfig abilityConfig)
    {
        if (abilityConfig == null) return null;

        foreach (var trait in traitLookupByNode.Values)
        {
            if (trait.isActive && trait.data.abilityReplacement?.requiredAbility == abilityConfig)
            {
                return trait.data.abilityReplacement.newAbilityConfig;
            }
        }
        return null;
    }

    /// <summary>
    /// Reset only run-specific (roller-selected) traits, preserving gear-granted traits.
    /// Called by ArenaTeleporter when starting a new run so equipment traits remain active.
    /// </summary>
    public void ResetRunTraits()
    {
        var nodeIDs = traitLookupByNode.Keys.ToList();
        foreach (string nodeID in nodeIDs)
        {
            RemoveTraitByNode(nodeID);
        }
        traitLookupByNode.Clear();
        RecalculateModifiers();
        playerController?.RequestStatsRecalculation();
        OnTraitsChanged?.Invoke();
    }

    /// <summary>
    /// Reset all traits (for complete respec / character switch)
    /// </summary>
   public void ResetAllTraits()
   {
       var nodeIDs = traitLookupByNode.Keys.ToList();
       foreach (var nodeID in nodeIDs)
       {
           RemoveTraitByNode(nodeID);
       }
       _gearNodeIDs.Clear();
       RecalculateModifiers();
       playerController?.RequestStatsRecalculation();
       OnTraitsChanged?.Invoke();
   }

    public int GetTraitLevel(string nodeID)
    {
        if (string.IsNullOrEmpty(nodeID))
            return 0;

        if (traitLookupByNode.TryGetValue(nodeID, out Trait trait))
        {
            return trait.level;
        }

        return 0;
    }

}
