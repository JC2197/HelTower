using UnityEngine;

/// <summary>
/// Utility class for applying stat conversions to CharacterData.
/// Stat conversions translate base stats (Vigor, Intelligence, etc.) into derived stats (MaxHealth, MaxEnergy, etc.)
/// </summary>
public static class CharacterStatConverter
{
    /// <summary>
    /// Apply stat conversions to a CharacterData's StatContainer.
    /// Reads from statContainer (which includes level bonuses), calculates conversions,
    /// and writes derived stats back to statContainer.
    /// 
    /// baseStatContainer is immutable (original class stats) and is NOT used here.
    /// statContainer accumulates level bonuses and serves as the "current base" for calculations.
    /// </summary>
    public static void ApplyConversions(CharacterData characterData)
    {
        if (characterData == null || characterData.statContainer == null)
        {
            Debug.LogError("[CharacterStatConverter] Cannot apply conversions - null CharacterData or StatContainer");
            return;
        }
        
        // Load conversion data from Resources
        StatConversionData conversionData = Resources.Load<StatConversionData>("StatConversionData");
        if (conversionData == null)
        {
            Debug.LogWarning("[CharacterStatConverter] No StatConversionData found in Resources! Skipping conversions.");
            return;
        }
        
        Debug.Log($"[CharacterStatConverter] Applying stat conversions to {characterData.displayName}");
        
        // Apply each conversion rule to statContainer
        foreach (var conversion in conversionData.conversions)
        {
            if (conversion == null) continue;
            
            // Get source stat value from statContainer (includes level bonuses)
            float sourceValue = characterData.statContainer.GetStat(conversion.baseStatName);
            
            // Calculate conversion based on points per tick
            int effectivePoints = Mathf.FloorToInt(sourceValue) / conversion.pointsPerTick;
            float convertedValue = effectivePoints * conversion.conversionRate;
            
            // Apply percentage if needed
            if (conversion.isPercentage)
            {
                convertedValue *= 0.01f;
            }
            
            // Get base value of target stat (from immutable baseStatContainer)
            // This ensures derived stats start from their class default before adding conversions
            float baseTargetValue = characterData.baseStatContainer != null 
                ? characterData.baseStatContainer.GetStat(conversion.derivedStatName) 
                : 0f;
            
            float newTargetValue = baseTargetValue + convertedValue;
            
            characterData.statContainer.SetStat(conversion.derivedStatName, newTargetValue);
            
            Debug.Log($"[CharacterStatConverter] {conversion.baseStatName}={sourceValue} → {conversion.derivedStatName}: base={baseTargetValue} + conversion={convertedValue} = {newTargetValue}");
        }
    }
}
