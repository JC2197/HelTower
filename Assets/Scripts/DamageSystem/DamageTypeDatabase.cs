using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central database for all damage types in the game.
/// Automatically generates dropdowns everywhere damage types are used.
/// Create via: Assets/Create/Damage System/Damage Type Database
/// </summary>
[CreateAssetMenu(fileName = "DamageTypeDatabase", menuName = "Damage System/Damage Type Database")]
public class DamageTypeDatabase : ScriptableObject
{
    private static DamageTypeDatabase instance;
    
    public static DamageTypeDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<DamageTypeDatabase>("DamageTypeDatabase");
                if (instance == null)
                {
                    Debug.LogError("DamageTypeDatabase not found! Create one at Resources/DamageTypeDatabase");
                }
            }
            return instance;
        }
    }
    
    [Header("Damage Types")]
    [Tooltip("All available damage types in the game")]
    public List<DamageTypeData> damageTypes = new List<DamageTypeData>();
    
    public DamageTypeData GetDamageType(string typeName)
    {
        return damageTypes.Find(t => t.damageTypeName == typeName);
    }
    
    public DamageTypeData GetDamageType(int index)
    {
        if (index >= 0 && index < damageTypes.Count)
            return damageTypes[index];
        return null;
    }
    
    public int GetDamageTypeIndex(string typeName)
    {
        return damageTypes.FindIndex(t => t.damageTypeName == typeName);
    }
    
    public string[] GetDamageTypeNames()
    {
        return damageTypes.ConvertAll(t => t.damageTypeName).ToArray();
    }
}
