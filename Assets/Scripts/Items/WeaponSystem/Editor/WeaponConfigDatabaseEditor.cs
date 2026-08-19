#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(WeaponConfigDatabase))]
public class WeaponConfigDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WeaponConfigDatabase database = (WeaponConfigDatabase)target;
        
        EditorGUILayout.HelpBox(
            "This database must be located at: Assets/Resources/WeaponConfigDatabase.asset\n" +
            "Add WeaponConfig assets to the list below. DO NOT manually delete entries.",
            MessageType.Info);
        
        EditorGUILayout.Space();
        
        // Draw default list inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        
        // Utility buttons
        EditorGUILayout.LabelField("Maintenance Tools", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Clean Null Entries", GUILayout.Height(30)))
        {
            database.CleanNullEntries();
            EditorUtility.DisplayDialog("Clean Complete", 
                "Removed all null entries from the database.", "OK");
        }
        
        if (GUILayout.Button("Find All WeaponConfigs in Project", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Auto-Populate Database?",
                "This will search the entire project for WeaponConfig assets and add any missing ones to the database. Continue?",
                "Yes", "Cancel"))
            {
                AutoPopulateDatabase(database);
            }
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Total Configs: {database.AllWeaponConfigs.Count}", EditorStyles.miniLabel);
    }
    
    private void AutoPopulateDatabase(WeaponConfigDatabase database)
    {
        // Find all WeaponConfig assets in the project
        string[] guids = AssetDatabase.FindAssets("t:WeaponConfig");
        int addedCount = 0;
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WeaponConfig config = AssetDatabase.LoadAssetAtPath<WeaponConfig>(path);
            
            if (config != null && !database.AllWeaponConfigs.Contains(config))
            {
                database.AddConfig(config);
                addedCount++;
            }
        }
        
        if (addedCount > 0)
        {
            EditorUtility.DisplayDialog("Auto-Populate Complete",
                $"Added {addedCount} WeaponConfig(s) to the database.\n" +
                $"Total configs: {database.AllWeaponConfigs.Count}",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Auto-Populate Complete",
                "All WeaponConfigs are already in the database.",
                "OK");
        }
    }
}
#endif
