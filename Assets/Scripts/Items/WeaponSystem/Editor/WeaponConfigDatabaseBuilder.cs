using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor utility that creates and populates WeaponConfigDatabase.asset.
///
/// Run via:  Tools → Weapon Database → Build / Rebuild Weapon Config Database
///
/// The asset is placed at Assets/Resources/WeaponConfigDatabase.asset, which is
/// the path WeaponConfigDatabase.Instance expects at runtime.
/// </summary>
public static class WeaponConfigDatabaseBuilder
{
    private const string AssetPath = "Assets/Resources/WeaponConfigDatabase.asset";

    [MenuItem("Tools/Weapon Database/Build (Add Missing Configs)")]
    public static void BuildDatabase()
    {
        WeaponConfigDatabase db = LoadOrCreateDatabase();

        List<WeaponConfig> found = FindAllWeaponConfigs();
        int added = 0;

        foreach (WeaponConfig cfg in found)
        {
            if (!db.AllWeaponConfigs.Contains(cfg))
            {
                db.AddConfig(cfg);
                added++;
            }
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WeaponConfigDatabaseBuilder] Done. {added} new config(s) added. " +
                  $"Total: {db.AllWeaponConfigs.Count}. Asset: {AssetPath}");
    }

    [MenuItem("Tools/Weapon Database/Rebuild (Replace All Configs)")]
    public static void RebuildDatabase()
    {
        WeaponConfigDatabase db = LoadOrCreateDatabase();

        // Remove everything, then re-add all discovered configs
        foreach (var existing in db.AllWeaponConfigs.ToList())
            db.RemoveConfig(existing);
        db.CleanNullEntries();

        List<WeaponConfig> found = FindAllWeaponConfigs();
        foreach (var cfg in found)
            db.AddConfig(cfg);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WeaponConfigDatabaseBuilder] Rebuilt. {found.Count} config(s). Asset: {AssetPath}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WeaponConfigDatabase LoadOrCreateDatabase()
    {
        WeaponConfigDatabase db = AssetDatabase.LoadAssetAtPath<WeaponConfigDatabase>(AssetPath);
        if (db != null) return db;

        // Ensure the Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        db = ScriptableObject.CreateInstance<WeaponConfigDatabase>();
        AssetDatabase.CreateAsset(db, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[WeaponConfigDatabaseBuilder] Created new database at {AssetPath}");
        return db;
    }

    private static List<WeaponConfig> FindAllWeaponConfigs()
    {
        return AssetDatabase.FindAssets("t:WeaponConfig")
            .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(cfg => cfg != null)
            .OrderBy(cfg => cfg.weaponName)
            .ToList();
    }
}
