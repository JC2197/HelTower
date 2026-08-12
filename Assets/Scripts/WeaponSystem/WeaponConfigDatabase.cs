using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Database of all WeaponConfigs in the game.
/// Create one instance in your project and drag all WeaponConfigs into the list.
/// The registry uses this database for lookups.
/// 
/// IMPORTANT: This asset must be in a Resources folder to be loaded at runtime.
/// Recommended path: Assets/Resources/WeaponConfigDatabase.asset
/// </summary>
[CreateAssetMenu(fileName = "WeaponConfigDatabase", menuName = "Items/Weapons/Weapon Config Database")]
public class WeaponConfigDatabase : ScriptableObject
{
    [Tooltip("List of all weapon configs in the game. DO NOT remove entries manually - use the 'Clean Null Entries' button below.")]
    [SerializeField] // Use SerializeField instead of public to protect the list
    private List<WeaponConfig> allWeaponConfigs = new List<WeaponConfig>();
    
    // Public read-only access
    public List<WeaponConfig> AllWeaponConfigs => allWeaponConfigs ?? new List<WeaponConfig>();
    
    private static WeaponConfigDatabase instance;
    
    public static WeaponConfigDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<WeaponConfigDatabase>("WeaponConfigDatabase");
                if (instance == null)
                {
                    WeaponConfigDatabase[] candidates = Resources.LoadAll<WeaponConfigDatabase>(string.Empty);
                    if (candidates != null && candidates.Length > 0)
                    {
                        instance = candidates[0];
                        Debug.LogWarning($"[WeaponConfigDatabase] Exact resource path 'WeaponConfigDatabase' was not found. Using fallback asset '{instance.name}' from Resources. Rename asset to 'WeaponConfigDatabase.asset' to remove this warning.");
                    }
                    else
                    {
                        Debug.LogError("[WeaponConfigDatabase] No WeaponConfigDatabase found in Resources folder! Create one at Assets/Resources/WeaponConfigDatabase.asset");
                    }
                }
            }
            return instance;
        }
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Add a config to the database (Editor only)
    /// </summary>
    public void AddConfig(WeaponConfig config)
    {
        if (config == null) return;
        if (allWeaponConfigs == null) allWeaponConfigs = new List<WeaponConfig>();
        if (!allWeaponConfigs.Contains(config))
        {
            allWeaponConfigs.Add(config);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
    
    /// <summary>
    /// Remove a config from the database (Editor only)
    /// </summary>
    public void RemoveConfig(WeaponConfig config)
    {
        if (allWeaponConfigs == null) return;
        if (allWeaponConfigs.Remove(config))
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
    
    /// <summary>
    /// Clean null entries (Editor only)
    /// </summary>
    public void CleanNullEntries()
    {
        if (allWeaponConfigs == null)
        {
            allWeaponConfigs = new List<WeaponConfig>();
            return;
        }
        
        int beforeCount = allWeaponConfigs.Count;
        allWeaponConfigs.RemoveAll(config => config == null);
        int afterCount = allWeaponConfigs.Count;
        
        if (beforeCount != afterCount)
        {
            Debug.LogWarning($"[WeaponConfigDatabase] Removed {beforeCount - afterCount} null entries.");
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
    
    private void OnEnable()
    {
        // Ensure list is initialized
        if (allWeaponConfigs == null)
        {
            allWeaponConfigs = new List<WeaponConfig>();
        }
    }
#endif
}

/// <summary>
/// Registry for looking up WeaponConfig by name.
/// Loads from WeaponConfigDatabase ScriptableObject.
/// This allows weapon configs to exist independently without needing to be in WeaponItemDropsConfig lists.
/// </summary>
public static class WeaponConfigRegistry
{
    private static Dictionary<string, WeaponConfig> configsByName;
    private static bool isInitialized = false;

    /// <summary>
    /// Initialize the registry by loading all WeaponConfigs from the database
    /// </summary>
    private static void Initialize()
    {
        if (isInitialized) return;

        configsByName = new Dictionary<string, WeaponConfig>();
        
        WeaponConfigDatabase database = WeaponConfigDatabase.Instance;
        if (database == null)
        {
            Debug.LogError("[WeaponConfigRegistry] Cannot initialize - WeaponConfigDatabase not found!");
            isInitialized = true;
            return;
        }

        // Filter out null configs before processing
        int nullCount = 0;
        foreach (var config in database.AllWeaponConfigs)
        {
            if (config == null)
            {
                nullCount++;
                continue;
            }
            
            if (string.IsNullOrEmpty(config.weaponName))
            {
                Debug.LogWarning($"[WeaponConfigRegistry] Skipping config with empty weaponName");
                continue;
            }
            
            if (!configsByName.ContainsKey(config.weaponName))
            {
                configsByName[config.weaponName] = config;
                Debug.Log($"[WeaponConfigRegistry] Registered: {config.weaponName}");
            }
            else
            {
                Debug.LogWarning($"[WeaponConfigRegistry] Duplicate weapon name found: {config.weaponName}. Using first instance.");
            }
        }
        
        if (nullCount > 0)
        {
            Debug.LogWarning($"[WeaponConfigRegistry] Found {nullCount} null configs in database. Consider cleaning up the database.");
        }

        isInitialized = true;
        Debug.Log($"[WeaponConfigRegistry] Initialized with {configsByName.Count} weapon configs");
    }

    /// <summary>
    /// Get a WeaponConfig by its weapon name
    /// </summary>
    public static WeaponConfig GetConfig(string weaponName)
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (string.IsNullOrEmpty(weaponName))
        {
            Debug.LogWarning("[WeaponConfigRegistry] Cannot get config - weaponName is null or empty");
            return null;
        }

        if (configsByName.TryGetValue(weaponName, out WeaponConfig config))
        {
            return config;
        }

        Debug.LogWarning($"[WeaponConfigRegistry] No WeaponConfig found with name: {weaponName}");
        return null;
    }

    /// <summary>
    /// Check if a config exists
    /// </summary>
    public static bool HasConfig(string weaponName)
    {
        if (!isInitialized)
        {
            Initialize();
        }

        return configsByName.ContainsKey(weaponName);
    }

    /// <summary>
    /// Get all registered config names
    /// </summary>
    public static string[] GetAllConfigNames()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        return configsByName.Keys.ToArray();
    }

    /// <summary>
    /// Force reload all configs (useful for editor refresh)
    /// </summary>
    public static void Reload()
    {
        isInitialized = false;
        configsByName?.Clear();
        Initialize();
    }
}
