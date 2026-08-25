using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Persistent meta progression for a single save slot — survives across runs.
/// Per-run / per-character state (stats, weapons, abilities, level) lives in CharacterData.
/// </summary>
[CreateAssetMenu(fileName = "SaveFile_", menuName = "Save Files/Save File Data")]
public class SaveFileData : ScriptableObject
{
    /// <summary>Node IDs with this prefix are granted by gear and are never persisted.</summary>
    public const string GearNodePrefix = "gear_";

    [Header("Identity")]
    public string saveFileName;
    public string displayName;

    [Tooltip("className of the ClassData this save file last played. Used to rebuild CharacterData on load.")]
    public string lastClassName;

    [Header("Meta Currencies")]
    public int totalGold;
    public int researchPoints;

    [Header("World Progression")]
    public int maxLevelMapUnlocked = 1;

    [Header("Trait Progression (Meta)")]
    [Tooltip("Unlocked node IDs across every trait tree the equipped class exposes.")]
    public List<string> unlockedNodeIDs = new List<string>();

    [Header("Run State")]
    [Tooltip("True while a map/arena run is active. A save loaded with this still true ended abnormally.")]
    public bool inMap;

    public static event Action<int> OnGoldChanged;

    public bool IsNodeUnlocked(string nodeID)
    {
        return !string.IsNullOrEmpty(nodeID) && unlockedNodeIDs != null && unlockedNodeIDs.Contains(nodeID);
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Debug.Log($"[Gold] Adding {amount} gold. Previous total: {totalGold}, New total: {totalGold + amount}");
        totalGold += amount;
        OnGoldChanged?.Invoke(totalGold);
    }

    /// <summary>Deduct gold if the balance covers it. Returns false and changes nothing otherwise.</summary>
    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (totalGold < amount) return false;

        totalGold -= amount;
        OnGoldChanged?.Invoke(totalGold);
        return true;
    }

    public bool UnlockNode(string nodeID)
    {
        if (string.IsNullOrEmpty(nodeID) || IsGearNode(nodeID))
            return false;

        unlockedNodeIDs ??= new List<string>();
        if (unlockedNodeIDs.Contains(nodeID))
            return false;

        unlockedNodeIDs.Add(nodeID);
        return true;
    }

    /// <summary>Replace the persisted node list with the supplied set, dropping gear-granted nodes.</summary>
    public void SetUnlockedNodes(IEnumerable<string> nodeIDs)
    {
        unlockedNodeIDs ??= new List<string>();
        unlockedNodeIDs.Clear();

        if (nodeIDs == null)
            return;

        foreach (string nodeID in nodeIDs)
        {
            if (string.IsNullOrEmpty(nodeID) || IsGearNode(nodeID))
                continue;
            if (!unlockedNodeIDs.Contains(nodeID))
                unlockedNodeIDs.Add(nodeID);
        }
    }

    public void ClearTraitProgress()
    {
        unlockedNodeIDs?.Clear();
    }

    public static bool IsGearNode(string nodeID)
    {
        return !string.IsNullOrEmpty(nodeID) && nodeID.StartsWith(GearNodePrefix);
    }
}
