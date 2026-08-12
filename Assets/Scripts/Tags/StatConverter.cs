using UnityEngine;

/// <summary>
/// Handles conversion of base stats to derived stats using StatConversionData.
/// Automatically applies conversions to character stats via StatContainer.
/// </summary>
public class StatConverter : MonoBehaviour
{
    [Header("Conversion Configuration")]
    [SerializeField] private StatConversionData conversionData;
    
    [Header("References")]
    [SerializeField] private Organism organism;
    
    private void Awake()
    {
        if (organism == null)
        {
            organism = GetComponent<Organism>();
        }
        
        // Load default conversion data if not assigned
        if (conversionData == null)
        {
            conversionData = Resources.Load<StatConversionData>("StatConversionData");
            if (conversionData == null)
            {
                Debug.LogWarning("[StatConverter] No StatConversionData assigned or found in Resources!");
            }
        }
    }
    
    private void Start()
    {
        // Stat conversions are now handled by CharacterStatConverter and saved to CharacterData
        // This component is kept for legacy compatibility but does not auto-calculate
        Debug.Log("[StatConverter] Start - conversions handled by CharacterStatConverter, skipping auto-calculation");
    }
    
    /// <summary>
    /// Recalculates all derived stats from base stats
    /// </summary>
    public void RecalculateAllStats()
    {
        if (conversionData == null || organism == null || organism.AllStats == null)
        {
            Debug.LogWarning("[StatConverter] Cannot recalculate - missing references");
            return;
        }
        
        // Reset derived stats in StatContainer
        organism.AllStats.SetStat("AttackSpeed", 0f);
        organism.AllStats.SetStat("CooldownReduction", 0f);
        organism.AllStats.SetStat("HealthRegen", 0f);
        organism.AllStats.SetStat("EnergyRegen", 0f);
        
        // Get base stat values from StatContainer
        int power = Mathf.RoundToInt(organism.AllStats.GetStat("POWER"));
        int body = Mathf.RoundToInt(organism.AllStats.GetStat("BODY"));
        int mind = Mathf.RoundToInt(organism.AllStats.GetStat("MIND"));
        int faith = Mathf.RoundToInt(organism.AllStats.GetStat("FAITH"));
        int skill = Mathf.RoundToInt(organism.AllStats.GetStat("SKILL"));
        int survival = Mathf.RoundToInt(organism.AllStats.GetStat("SURVIVAL"));
        
        // Process each base stat
        ApplyStatConversions("POWER", power);
        ApplyStatConversions("BODY", body);
        ApplyStatConversions("MIND", mind);
        ApplyStatConversions("FAITH", faith);
        ApplyStatConversions("SKILL", skill);
        ApplyStatConversions("SURVIVAL", survival);
        
        // Handle health and energy separately because they affect max/current values
        ApplyHealthBonus(power);
        ApplyEnergyBonus(mind);
        
        Debug.Log($"[StatConverter] Recalculated stats - AttackSpeed: {organism.AllStats.GetStat("AttackSpeed")}, HealthRegen: {organism.AllStats.GetStat("HealthRegen")}, EnergyRegen: {organism.AllStats.GetStat("EnergyRegen")}");
    }
    
    private void ApplyStatConversions(string baseStatName, int baseStatValue)
    {
        var conversions = conversionData.GetConversionsForBaseStat(baseStatName);
        
        foreach (var conversion in conversions)
        {
            // Skip health/energy - handled separately
            if (conversion.derivedStatName.ToLower() == "maxhealth" || 
                conversion.derivedStatName.ToLower() == "maxenergy")
            {
                continue;
            }
            
            float value = conversionData.CalculateDerivedValue(baseStatName, baseStatValue, conversion.derivedStatName);
            
            // Normalize stat name to match StatContainer keys
            string derivedStatKey = NormalizeStatName(conversion.derivedStatName);
            
            // Add the conversion value to the existing stat value
            float currentValue = organism.AllStats.GetStat(derivedStatKey);
            organism.AllStats.SetStat(derivedStatKey, currentValue + value);
            
            Debug.Log($"[StatConverter] {baseStatName} ({baseStatValue}) → {derivedStatKey}: +{value} (total: {currentValue + value})");
        }
    }
    
    private string NormalizeStatName(string statName)
    {
        // Convert to proper casing for StatContainer
        switch (statName.ToLower())
        {
            case "attackspeed": return "AttackSpeed";
            case "cooldownreduction": return "CooldownReduction";
            case "healthregen": return "HealthRegen";
            case "energyregen": return "EnergyRegen";
            case "castspeed": return "CastSpeed";
            default: return statName;
        }
    }
    
    private void ApplyHealthBonus(int power)
    {
        float healthBonus = conversionData.CalculateDerivedValue("POWER", power, "MaxHealth");
        
        // Get base max health from current MaxHealth stat (which includes class base + level-ups)
        // Subtract the old bonus to get back to pure base value
        float currentMaxHealth = organism.MaxHealth;
        float oldBonus = conversionData.CalculateDerivedValue("POWER", Mathf.RoundToInt(organism.AllStats.GetStat("POWER")), "MaxHealth");
        float baseMaxHealth = currentMaxHealth - oldBonus;
        
        // Ensure we have a valid base (fallback to 100 if something went wrong)
        if (baseMaxHealth <= 0)
        {
            baseMaxHealth = 100f;
        }
        
        float oldMaxHealth = organism.MaxHealth;
        float newMaxHealth = baseMaxHealth + healthBonus;
        
        // Calculate health percentage before change
        float healthPercent = oldMaxHealth > 0 ? organism.CurrentHealth / oldMaxHealth : 1f;
        
        // Update max health in StatContainer
        organism.AllStats.SetStat("MaxHealth", newMaxHealth);
        
        // Scale current health to new max (or set to full if this is first time)
        if (organism.CurrentHealth == 0 || healthPercent >= 0.99f)
        {
            organism.ModifyHealth(newMaxHealth - organism.CurrentHealth); // Set to full health
        }
        else
        {
            float targetHealth = newMaxHealth * healthPercent;
            organism.ModifyHealth(targetHealth - organism.CurrentHealth);
        }
        
        Debug.Log($"[StatConverter] Health: {baseMaxHealth} (base) + {healthBonus} (from STR) = {newMaxHealth}");
    }
    
    private void ApplyEnergyBonus(int mind)
    {
        float energyBonus = conversionData.CalculateDerivedValue("MIND", mind, "MaxEnergy");
        
        // Get base max energy from current MaxEnergy stat (which includes class base + level-ups)
        // Subtract the old bonus to get back to pure base value
        float currentMaxEnergy = organism.MaxEnergy;
        float oldBonus = conversionData.CalculateDerivedValue("MIND", Mathf.RoundToInt(organism.AllStats.GetStat("MIND")), "MaxEnergy");
        float baseMaxEnergy = currentMaxEnergy - oldBonus;
        
        // Ensure we have a valid base (fallback to 50 if something went wrong)
        if (baseMaxEnergy <= 0)
        {
            baseMaxEnergy = 50f;
        }
        
        float oldMaxEnergy = organism.MaxEnergy;
        float newMaxEnergy = baseMaxEnergy + energyBonus;
        
        // Calculate energy percentage before change
        float energyPercent = oldMaxEnergy > 0 ? organism.CurrentEnergy / oldMaxEnergy : 1f;
        
        // Update max energy in StatContainer
        organism.AllStats.SetStat("MaxEnergy", newMaxEnergy);
        
        // Scale current energy to new max (or set to full if this is first time)
        if (organism.CurrentEnergy == 0 || energyPercent >= 0.99f)
        {
            organism.ModifyEnergy(newMaxEnergy - organism.CurrentEnergy); // Set to full energy
        }
        else
        {
            float targetEnergy = newMaxEnergy * energyPercent;
            organism.ModifyEnergy(targetEnergy - organism.CurrentEnergy);
        }
        
        Debug.Log($"[StatConverter] Energy: {baseMaxEnergy} (base) + {energyBonus} (from INT) = {newMaxEnergy}");
    }
    
    /// <summary>
    /// Call this when base stats change (e.g., from traits, items, level up)
    /// </summary>
    public void OnBaseStatsChanged()
    {
        RecalculateAllStats();
    }
}
