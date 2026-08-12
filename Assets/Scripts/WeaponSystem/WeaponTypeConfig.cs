using UnityEngine;

/// <summary>
/// ScriptableObject that defines default positioning for a weapon type (e.g., Pistol, Sword).
/// Individual WeaponConfigs inherit these values unless they enable overridePositioning.
/// </summary>
[CreateAssetMenu(fileName = "WeaponType_", menuName = "Items/Weapons/Weapon Type Config")]
public class WeaponTypeConfig : ScriptableObject
{
    [Tooltip("Must match a weapon type string from WeaponTypeList")]
    [WeaponTypeDropdown]
    public string typeName = "Pistol";

    [Tooltip("Default positioning data for all weapons of this type")]
    public WeaponPositioningData defaultPositioning = new WeaponPositioningData();

    /// <summary>
    /// Cached lookup by type name. Populated at runtime.
    /// </summary>
    private static System.Collections.Generic.Dictionary<string, WeaponTypeConfig> _lookup;

    /// <summary>
    /// Find the WeaponTypeConfig for a given weapon type string.
    /// Loads all WeaponTypeConfig assets from Resources on first call.
    /// </summary>
    public static WeaponTypeConfig GetConfigForType(string weaponType)
    {
        if (_lookup == null)
        {
            _lookup = new System.Collections.Generic.Dictionary<string, WeaponTypeConfig>();
            var all = Resources.LoadAll<WeaponTypeConfig>("");
            foreach (var config in all)
            {
                if (!string.IsNullOrEmpty(config.typeName) && !_lookup.ContainsKey(config.typeName))
                {
                    _lookup[config.typeName] = config;
                }
            }
        }

        if (!string.IsNullOrEmpty(weaponType) && _lookup.TryGetValue(weaponType, out var found))
        {
            return found;
        }
        return null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only: find via AssetDatabase for inspector use (avoids Resources folder requirement).
    /// </summary>
    public static WeaponTypeConfig EditorGetConfigForType(string weaponType)
    {
        if (string.IsNullOrEmpty(weaponType)) return null;

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:WeaponTypeConfig");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponTypeConfig>(path);
            if (config != null && config.typeName == weaponType)
                return config;
        }
        return null;
    }
#endif

    /// <summary>
    /// Clear the cached lookup (e.g., after creating new configs at runtime).
    /// </summary>
    public static void ClearCache()
    {
        _lookup = null;
    }
}
