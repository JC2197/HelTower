using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines how base stats (Vigor, Dexterity, etc.) convert to derived stats.
/// Create via: Assets/Create/Stats/Stat Conversion Data
/// </summary>
[CreateAssetMenu(fileName = "StatConversionData", menuName = "Stats/Stat Conversion Data")]
public class StatConversionData : ScriptableObject
{
    [System.Serializable]
    public class StatConversion
    {
        [Tooltip("Name of the base stat (e.g., 'vigor', 'strength')")]
        public string baseStatName;
        
        [Tooltip("Name of the derived stat (e.g., 'maxHealth', 'attackSpeed')")]
        public string derivedStatName;
        
        [Tooltip("How much derived stat per 1 point of base stat")]
        public float conversionRate;
        
        [Tooltip("Optional: Only apply conversion for every X points (e.g., 5 for Talent)")]
        public int pointsPerTick = 1;
        
        [Tooltip("Is this a percentage bonus? (multiplied by 0.01)")]
        public bool isPercentage = false;
    }
    
    [Header("Stat Conversions")]
    [Tooltip("Define all base stat → derived stat conversions here")]
    public List<StatConversion> conversions = new List<StatConversion>
    {
        // Strength → Max Health (1 str = 2 max hp)
        new StatConversion 
        { 
            baseStatName = "strength", 
            derivedStatName = "maxHealth", 
            conversionRate = 2f, 
            pointsPerTick = 1,
            isPercentage = false
        },
        
        // Vigor → Health Regen (5 vigor = 1 hp/sec)
        new StatConversion 
        { 
            baseStatName = "vigor", 
            derivedStatName = "healthRegen", 
            conversionRate = 1f, 
            pointsPerTick = 5,
            isPercentage = false
        },
        
        // Intelligence → Max Energy
        new StatConversion 
        { 
            baseStatName = "intelligence", 
            derivedStatName = "maxEnergy", 
            conversionRate = 2f, 
            pointsPerTick = 1,
            isPercentage = false
        },
        
        // Faith → Energy Regen
        new StatConversion 
        { 
            baseStatName = "faith", 
            derivedStatName = "energyRegen", 
            conversionRate = 1f, 
            pointsPerTick = 5,
            isPercentage = false
        },
        
        // Talent → Cooldown Reduction (5 talent = 1% CDR)
        new StatConversion 
        { 
            baseStatName = "talent", 
            derivedStatName = "cooldownReduction", 
            conversionRate = 1f, 
            pointsPerTick = 5,
            isPercentage = true
        },
        
        // Dexterity → Attack Speed (5 dex = 3% attack speed)
        new StatConversion 
        { 
            baseStatName = "dexterity", 
            derivedStatName = "attackSpeed", 
            conversionRate = 3f, 
            pointsPerTick = 5,
            isPercentage = true
        }
    };
    
    /// <summary>
    /// Get all conversions for a specific base stat
    /// </summary>
    public List<StatConversion> GetConversionsForBaseStat(string baseStatName)
    {
        List<StatConversion> result = new List<StatConversion>();
        string searchName = baseStatName.ToLower();
        
        foreach (var conversion in conversions)
        {
            if (conversion.baseStatName.ToLower() == searchName)
            {
                result.Add(conversion);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Calculate derived stat value from base stat value
    /// </summary>
    public float CalculateDerivedValue(string baseStatName, int baseStatValue, string derivedStatName)
    {
        float totalValue = 0f;
        
        foreach (var conversion in conversions)
        {
            if (conversion.baseStatName.ToLower() == baseStatName.ToLower() &&
                conversion.derivedStatName.ToLower() == derivedStatName.ToLower())
            {
                // Calculate based on points per tick
                int effectivePoints = baseStatValue / conversion.pointsPerTick;
                float rawValue = effectivePoints * conversion.conversionRate;
                
                // Convert to percentage if needed
                if (conversion.isPercentage)
                {
                    totalValue += rawValue * 0.01f; // Convert to decimal (1% = 0.01)
                }
                else
                {
                    totalValue += rawValue;
                }
            }
        }
        
        return totalValue;
    }
}
