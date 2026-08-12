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

    [Header("Active Traits")]
    [SerializeField] private List<TraitData> startingTraits = new List<TraitData>();

    [Header("Trait Registry")]
    [Tooltip("Global list of all TraitData assets. Auto-loaded from Resources/TraitDataList if not assigned.")]
    [SerializeField] private TraitDataList traitDataList;

    private List<Trait> activeTraits = new List<Trait>();
    // Map nodeID -> Trait (allows multiple instances of same trait from different nodes)
    private Dictionary<string, Trait> traitLookupByNode = new Dictionary<string, Trait>();
  
    // Track which specific nodes are unlocked (allows same trait on multiple nodes)
    private HashSet<string> unlockedNodeIDs = new HashSet<string>();
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
        
        // Load traits for this character
        if (data != null && shouldLoadTraits)
        {
            Debug.Log($"[CharacterTraitManager] Loading traits from CharacterData for {data.characterName}");
            LoadTraitsFromCharacterData();
        }
        else
        {
            Debug.Log($"[CharacterTraitManager] Skipping trait load (shouldLoadTraits={shouldLoadTraits}, data={data != null})");
        }
        
        Debug.Log($"[CharacterTraitManager] ========================================");
    }

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
        if (traitData == null)
        {
            Debug.LogError("Cannot unlock null trait!");
            return false;
        }

        if (string.IsNullOrEmpty(nodeID))
        {
            Debug.LogError("Cannot unlock trait without nodeID!");
            return false;
        }

        // New unlocks (non-restore) are only valid for the owning player
        if (!isRestoring && !IsOwner)
        {
            Debug.LogWarning($"[CharacterTraitManager] UnlockTrait called on non-owner instance for node {nodeID} — ignoring.");
            return false;
        }

        // Gear-granted nodes (prefix "gear_") are tracked separately from tree nodes
        // so they are never serialised into characterData.unlockedNodeIDs.
        bool isGearNode = nodeID.StartsWith("gear_");
        HashSet<string> nodeSet = isGearNode ? _gearNodeIDs : unlockedNodeIDs;

        // Check if this specific node is already unlocked
        if (nodeSet.Contains(nodeID))
        {
            Debug.LogWarning($"Node {nodeID} is already unlocked!");
            return false;
        }
        
        // Mark this node as unlocked
        nodeSet.Add(nodeID);

        // Always activate the trait - allows multiple instances of same trait from different nodes
        // Create and activate trait instance
        Trait trait = new Trait(traitData);
        trait.Activate(gameObject);

        // Apply trait-unlocked abilities (auto-routed based on ability type)
        // Skip for AbilityUpgrade traits - the ability already exists (it's the prerequisite)
        // AbilityUpgrade traits only apply modifiers to the existing ability, not add new instances
        if (traitData.traitType != TraitType.AbilityUpgrade 
            && traitData.unlockedAbilities != null && traitData.unlockedAbilities.Count > 0)
        {
            CharacterAbilityManager abilityManager = GetComponent<CharacterAbilityManager>();
            if (abilityManager != null)
            {
                foreach (var unlock in traitData.unlockedAbilities)
                {
                    if (unlock.abilityConfig != null)
                    {
                        // Auto-route based on ability type (isMovementAbility → Dash, etc.)
                        abilityManager.AddAbility(unlock.abilityConfig);
                        Debug.Log($"[CharacterTraitManager] Trait '{traitData.displayName}' unlocked ability '{unlock.abilityConfig.abilityName}'");
                    }
                }
            }
        }

        activeTraits.Add(trait);
        traitLookupByNode[nodeID] = trait;
        
        // Count how many instances of this trait exist now
        int instanceCount = GetTraitInstanceCount(traitData);

        Debug.Log($"[CharacterTraitManager] ========================================");
        Debug.Log($"[CharacterTraitManager] ACTIVATING TRAIT: {traitData.displayName} (Node: {nodeID})");
        Debug.Log($"[CharacterTraitManager] This is instance #{instanceCount} of this trait");
        Debug.Log($"[CharacterTraitManager] Trait has {traitData.statModifiers.Count} stat modifiers");
        foreach (var mod in traitData.statModifiers)
        {
            Debug.Log($"[CharacterTraitManager]   - {mod.statID}: +{mod.value} ({mod.modifierType})");
        }
        Debug.Log($"[CharacterTraitManager] Total active traits: {activeTraits.Count}");
        Debug.Log($"[CharacterTraitManager] Total unlocked nodes: {unlockedNodeIDs.Count}");

        // Recalculate cached stats
        RecalculateModifiers();

        Debug.Log($"[CharacterTraitManager] Unlocked node: {nodeID}. Total unlocked nodes: {unlockedNodeIDs.Count}");
        Debug.Log($"[CharacterTraitManager] Invoking OnTraitUnlocked event. Subscribers: {OnTraitUnlocked?.GetInvocationList()?.Length ?? 0}");
        OnTraitUnlocked?.Invoke(nodeID, traitData);
        
        Debug.Log($"[CharacterTraitManager] Invoking OnTraitsChanged event. Subscribers: {OnTraitsChanged?.GetInvocationList()?.Length ?? 0}");
        OnTraitsChanged?.Invoke();

        // Belt-and-suspenders: directly tell PlayerController to recalculate stats.
        // The event path above SHOULD do this, but if the subscription was lost (e.g. object
        // lifecycle, timing) this ensures traits always affect gameplay stats.
        if (playerController != null)
        {
            playerController.RequestStatsRecalculation();
            Debug.Log($"[CharacterTraitManager] Called playerController.RequestStatsRecalculation() directly");
        }
        else
        {
            Debug.LogWarning($"[CharacterTraitManager] playerController is NULL — cannot directly request stat recalculation!");
        }
        
        Debug.Log($"[CharacterTraitManager] Trait unlock complete!");
        Debug.Log($"[CharacterTraitManager] ========================================");
        
        // Update CharacterData in-memory — caller handles save + network broadcast
        UpdateCharacterDataTraitList();

        return true;
    }

    /// <summary>
    /// Check if a specific node is unlocked
    /// </summary>
    public bool IsNodeUnlocked(string nodeID)
    {
        return !string.IsNullOrEmpty(nodeID) && (unlockedNodeIDs.Contains(nodeID) || _gearNodeIDs.Contains(nodeID));
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

        activeTraits.Remove(trait);
        traitLookupByNode.Remove(nodeID);
        unlockedNodeIDs.Remove(nodeID);
        _gearNodeIDs.Remove(nodeID);

        RecalculateModifiers();

        OnTraitRemoved?.Invoke(nodeID, trait.data);
        OnTraitsChanged?.Invoke();

        // Belt-and-suspenders: directly tell PlayerController to recalculate
        playerController?.RequestStatsRecalculation();

        // Update CharacterData in-memory — caller handles save + network broadcast
        UpdateCharacterDataTraitList();

        return true;
    }
    
    /// <summary>
    /// Get all unlocked node IDs
    /// </summary>
    public HashSet<string> GetUnlockedNodeIDs()
    {
        return new HashSet<string>(unlockedNodeIDs);
    }

    /// <summary>
    /// Expose the authoritative CharacterData reference held by this manager.
    /// TraitSystemManager uses this in OpenTraitTree so that TSM.currentCharacterData
    /// and CTM.characterData are always the SAME object — preventing the divergence
    /// where SpendTraitPoint saves empty unlockedNodeIDs.
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
        return activeTraits.Any(t => t.data == traitData);
    }
    
    /// <summary>
    /// Get the number of active instances of a specific trait
    /// </summary>
    public int GetTraitInstanceCount(TraitData traitData)
    {
        if (traitData == null) return 0;
        return activeTraits.Count(t => t.data == traitData);
    }

    /// <summary>
    /// Get all active traits
    /// </summary>
    public List<TraitData> GetActiveTraits()
    {
        return activeTraits.Select(t => t.data).ToList();
    }
    
    /// <summary>
    /// Collect all trait tags from active traits with their frequencies.
    /// Returns a dictionary of tag -> count (how many times that tag appears).
    /// Used for weighting future trait rolls based on current build synergies.
    /// </summary>
    public Dictionary<string, int> GetTraitTagCollection()
    {
        Dictionary<string, int> tagCounts = new Dictionary<string, int>();
        
        foreach (var trait in activeTraits)
        {
            if (trait.data == null) continue;
            
            // Get all tags from this trait
            List<string> tags = trait.data.GetAllTags();
            
            foreach (string tag in tags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                
                if (tagCounts.ContainsKey(tag))
                {
                    tagCounts[tag]++;
                }
                else
                {
                    tagCounts[tag] = 1;
                }
            }
        }
        
        return tagCounts;
    }
    
    /// <summary>
    /// Update CharacterData's node ID list (nodeID-only system).
    /// Also propagates to PC.currentCharacterData when it is a different object
    /// (can happen if SetupCharacter re-ran after the trait tree was opened),
    /// preventing any later SaveCharacter call on PC's copy from wiping the nodes.
    /// </summary>
    private void UpdateCharacterDataTraitList()
    {
        if (characterData == null)
        {
            return;
        }

        // Trait tree/persistence removed — traits live only at runtime, nothing to write to CharacterData.
        Debug.Log($"[CharacterTraitManager] Trait list updated: {activeTraits.Count} traits, {unlockedNodeIDs.Count} nodes");
    }
    
    /// <summary>
    /// Load traits from CharacterData using saved nodeIDs.
    /// Traits are looked up directly in TraitDataList — no trait tree required.
    /// Node IDs from the trait roller are the traitID, optionally with a "_N" stack suffix.
    /// Gear-granted node IDs (prefix "gear_") are skipped here; they are always
    /// re-derived by CharacterGearManager.LoadEquippedGear().
    /// </summary>
    private void LoadTraitsFromCharacterData()
    {
        // Trait tree and persistence were removed — there are no saved node IDs to restore.
        // Traits are granted at runtime (trait rolls / starting traits) instead.
    }
    
    /// <summary>
    /// Recalculate all stat modifiers from traits
    /// </summary>
    private void RecalculateModifiers()
    {
        Debug.Log($"[CharacterTraitManager] ========== RECALCULATING TRAIT MODIFIERS ==========");
        Debug.Log($"[CharacterTraitManager] Active traits count: {activeTraits.Count}");
        
        cachedFlatModifiers.Clear();
        cachedPercentageModifiers.Clear();
        
        // Track how many instances of each trait we have for stacking info
        Dictionary<string, int> traitInstanceCounts = new Dictionary<string, int>();
        
        foreach (var trait in activeTraits)
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
        
            Debug.Log($"[CharacterTraitManager] Processing trait: {trait.data.displayName} (instance #{traitInstanceCounts[traitID]})");
            
            foreach (var modifier in trait.data.statModifiers)
            {
                // No trait scaling — use the modifier value directly.
                float scaledValue = modifier.value;
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
                Debug.Log($"[CharacterTraitManager] {statID} (percentage): base={baseValue}, flat={flat}% (+{flat/100f}), percent={percentage}% (x{1f + percentage/100f}), final={finalValue}");
            }
        }
        else
        {
            // Absolute stats: flat is added as-is
            finalValue = (baseValue + flat) * (1f + percentage / 100f);
            
            if (flat != 0f || percentage != 0f)
            {
                Debug.Log($"[CharacterTraitManager] {statID} (absolute): base={baseValue}, flat={flat}, percent={percentage}% (x{1f + percentage/100f}), final={finalValue}");
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
        foreach (var trait in activeTraits)
        {
            if (trait.isActive && trait.data.abilityReplacement?.requiredAbility != null 
                && trait.data.abilityReplacement.requiredAbility.abilityName == abilityName)
            {
                return trait.data.abilityReplacement.newAbilityConfig;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Check if any trait replaces a specific ability (by AbilityConfig reference)
    /// </summary>
    public AbilityConfig GetAbilityReplacement(AbilityConfig abilityConfig)
    {
        if (abilityConfig == null) return null;
        
        foreach (var trait in activeTraits)
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
        var rollerNodeIDs = new List<string>(unlockedNodeIDs);
        foreach (string nodeID in rollerNodeIDs)
        {
            RemoveTraitByNode(nodeID);
        }

        // Belt-and-suspenders: ensure the runtime set is cleared
        unlockedNodeIDs.Clear();
    }

    /// <summary>
    /// Reset all traits (for complete respec / character switch)
    /// </summary>
    public void ResetAllTraits()
    {
        var traitsToRemove = GetActiveTraits();
        foreach (var traitData in traitsToRemove)
        {
            RemoveTrait(traitData);
        }
        
        // Clear unlocked nodes
        unlockedNodeIDs.Clear();
        _gearNodeIDs.Clear();
    }
    

}
