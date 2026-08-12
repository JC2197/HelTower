using UnityEngine;
using System.Collections.Generic;

public static class DamageTypeRegistry
{
    private static Dictionary<string, DamageTypeData> damageTypes = new Dictionary<string, DamageTypeData>();
    private static bool isInitialized = false;
    
    public static void Initialize()
    {
        if (isInitialized) return;
        
        // Load all damage types from Resources folder
        DamageTypeData[] allDamageTypes = Resources.LoadAll<DamageTypeData>("DamageTypes");
        
        damageTypes.Clear();
        
        foreach (var damageType in allDamageTypes)
        {
            if (damageType != null && !string.IsNullOrEmpty(damageType.displayName))
            {
                damageTypes[damageType.displayName] = damageType;
            }
        }
        
        isInitialized = true;
        Debug.Log($"Loaded {damageTypes.Count} damage types from ScriptableObjects");
    }
    
    public static DamageTypeData GetDamageType(string damageTypeName)
    {
        if (!isInitialized) Initialize();
        
        damageTypes.TryGetValue(damageTypeName, out DamageTypeData damageType);
        return damageType;
    }
    
    public static DamageTypeData[] GetAllDamageTypes()
    {
        if (!isInitialized) Initialize();
        
        return new List<DamageTypeData>(damageTypes.Values).ToArray();
    }
    
    public static string[] GetDamageTypeNames()
    {
        if (!isInitialized) Initialize();
        
        return new List<string>(damageTypes.Keys).ToArray();
    }
    
    public static bool DamageTypeExists(string name)
    {
        if (!isInitialized) Initialize();
        
        return damageTypes.ContainsKey(name);
    }
}