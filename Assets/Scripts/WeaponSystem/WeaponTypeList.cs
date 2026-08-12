using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ScriptableObject that defines available weapon types for categorization and filtering.
/// Used by WeaponConfig to specify weapon type and by AbilityDataConfig to require specific weapon types.
/// </summary>
[CreateAssetMenu(fileName = "WeaponTypeList", menuName = "Config/Weapon Type List")]
public class WeaponTypeList : ScriptableObject
{
    [Tooltip("List of all available weapon types in the game")]
    public List<string> weaponTypes = new List<string>
    {
        "Sword",
        "Axe",
        "Dagger",
        "Spear",
        "Bow",
        "Crossbow",
        "Pistol",
        "Rifle",
        "Sniper",
        "Automatic",
        "Shotgun",
        "Staff",
        "Wand",
        "Shield",
        "Fist Weapon",
        "Hammer",
        "Mace",
        "Any" // Special type that matches everything
    };

    /// <summary>
    /// Get the singleton instance from Resources folder
    /// </summary>
    public static WeaponTypeList GetInstance()
    {
#if UNITY_EDITOR
        const string assetPath = "Assets/Resources/WeaponTypeList.asset";
        WeaponTypeList editorInstance = AssetDatabase.LoadAssetAtPath<WeaponTypeList>(assetPath);
        if (editorInstance != null)
        {
            return editorInstance;
        }

        string[] guids = AssetDatabase.FindAssets("t:WeaponTypeList");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WeaponTypeList found = AssetDatabase.LoadAssetAtPath<WeaponTypeList>(path);
            if (found != null)
            {
                return found;
            }
        }
#endif

        WeaponTypeList instance = Resources.Load<WeaponTypeList>("WeaponTypeList");
        if (instance == null)
        {
            Debug.LogWarning("[WeaponTypeList] No WeaponTypeList found in Resources folder. Create one at Resources/WeaponTypeList.asset");
        }
        return instance;
    }

    /// <summary>
    /// Check if a weapon type exists in the list
    /// </summary>
    public bool IsValidWeaponType(string weaponType)
    {
        return weaponTypes.Contains(weaponType);
    }
}
