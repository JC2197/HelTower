using UnityEngine;
using System;

/// <summary>
/// Monitors CharacterData for changes and notifies PlayerController to update in real-time.
/// Attach this to the player GameObject alongside PlayerController.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CharacterDataObserver : MonoBehaviour
{
    private PlayerController playerController;
    private CharacterData currentCharacterData;
    
    // Cached values to detect changes
    private int lastCharacterLevel;
    private float lastMaxHealth;
    private float lastMaxEnergy;
    private float lastMoveSpeed;
    private int lastStatHash;
    
    public event Action<CharacterData> OnCharacterDataChanged;
    
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("[CharacterDataObserver] PlayerController component not found!");
        }
    }
    
    private void Start()
    {
        // Get initial character data
        if (playerController != null)
        {
            currentCharacterData = playerController.GetCurrentCharacterData();
            CacheCurrentValues();
        }
    }
    
    
    /// <summary>
    /// Manually trigger a refresh (useful for level-ups, trait changes, etc.)
    /// </summary>
    public void ForceRefresh()
    {
        if (currentCharacterData != null && playerController != null)
        {
            Debug.Log("[CharacterDataObserver] Forced refresh triggered");
            OnCharacterDataChanged?.Invoke(currentCharacterData);
            CacheCurrentValues();
            playerController.SendMessage("RecalculateStatsWithTraits", SendMessageOptions.DontRequireReceiver);
        }
    }
    
    private bool HasDataChanged()
    {
        if (currentCharacterData == null) return false;
        
        // Check level
        if (currentCharacterData.characterLevel != lastCharacterLevel)
            return true;
            
        // Check base stats
        if (currentCharacterData.statContainer != null)
        {
            float maxHealth = currentCharacterData.statContainer.GetStat("MaxHealth");
            float maxEnergy = currentCharacterData.statContainer.GetStat("MaxEnergy");
            float moveSpeed = currentCharacterData.statContainer.GetStat("MoveSpeed");
            
            if (!Mathf.Approximately(maxHealth, lastMaxHealth) ||
                !Mathf.Approximately(maxEnergy, lastMaxEnergy) ||
                !Mathf.Approximately(moveSpeed, lastMoveSpeed))
            {
                return true;
            }
            
            // Check stat hash (detects any stat changes)
            int currentHash = GetStatHash();
            if (currentHash != lastStatHash)
                return true;
        }
        
        return false;
    }
    
    private void CacheCurrentValues()
    {
        if (currentCharacterData == null) return;
        
        lastCharacterLevel = currentCharacterData.characterLevel;
        
        if (currentCharacterData.statContainer != null)
        {
            lastMaxHealth = currentCharacterData.statContainer.GetStat("MaxHealth");
            lastMaxEnergy = currentCharacterData.statContainer.GetStat("MaxEnergy");
            lastMoveSpeed = currentCharacterData.statContainer.GetStat("MoveSpeed");
            lastStatHash = GetStatHash();
        }
    }
    
    private int GetStatHash()
    {
        if (currentCharacterData?.statContainer == null)
            return 0;
            
        int hash = 17;
        var allStats = currentCharacterData.statContainer.GetAllStats();
        
        foreach (var stat in allStats)
        {
            float value = currentCharacterData.statContainer.GetStat(stat.StatId);
            hash = hash * 31 + value.GetHashCode();
        }
        
        return hash;
    }
    
    /// <summary>
    /// Update the character data reference (call this if character data is swapped)
    /// </summary>
    public void SetCharacterData(CharacterData newData)
    {
        if (newData != currentCharacterData)
        {
            Debug.Log($"[CharacterDataObserver] Character data changed from {currentCharacterData?.displayName ?? "null"} to {newData?.displayName ?? "null"}");
            currentCharacterData = newData;
            CacheCurrentValues();
            OnCharacterDataChanged?.Invoke(currentCharacterData);
        }
    }
}
