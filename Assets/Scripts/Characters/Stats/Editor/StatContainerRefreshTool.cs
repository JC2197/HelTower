#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class StatContainerRefreshTool
{
    [MenuItem("Tools/Stats/Refresh Class and Enemy Containers")]
    private static void RefreshAll()
    {
        StatTypeDatabase defaultDatabase = StatTypeDatabase.Instance;
        if (defaultDatabase == null)
        {
            EditorUtility.DisplayDialog(
                "Stat Container Refresh",
                "No StatTypeDatabase was found at Resources/StatTypeDatabase.",
                "OK");
            return;
        }

        int classCount = RefreshClasses(defaultDatabase, out int changedClasses);
        int enemyCount = RefreshEnemies(defaultDatabase, out int changedEnemies);
        int summonCount = RefreshSummonConfigs(defaultDatabase, out int changedSummons);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int changedContainers = changedClasses + changedEnemies;
        Debug.Log($"[StatContainerRefresh] Refreshed {classCount} classes and {enemyCount} enemies. Updated {changedContainers} stat containers.");
        EditorUtility.DisplayDialog(
            "Stat Container Refresh",
            $"Refreshed {classCount} class assets and {enemyCount} enemy assets.\nUpdated {changedContainers} stat containers.",
            "OK");
    }

    private static int RefreshClasses(StatTypeDatabase database, out int changedContainers)
    {
        changedContainers = 0;
        string[] guids = AssetDatabase.FindAssets("t:ClassData");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ClassData classData = AssetDatabase.LoadAssetAtPath<ClassData>(path);
            if (classData == null)
                continue;

            Undo.RecordObject(classData, "Refresh Class Stats");
            classData.baseStatContainer ??= new StatContainer();
            int changed = classData.baseStatContainer.Synchronize(database);
            changedContainers += changed > 0 ? 1 : 0;

            if (changed > 0)
                EditorUtility.SetDirty(classData);
        }

        return guids.Length;
    }


    private static int RefreshEnemies(StatTypeDatabase defaultDatabase, out int changedContainers)
    {
        changedContainers = 0;
        string[] guids = AssetDatabase.FindAssets("t:EnemyConfig");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EnemyConfig enemyConfig = AssetDatabase.LoadAssetAtPath<EnemyConfig>(path);
            if (enemyConfig == null)
                continue;

            StatTypeDatabase database = enemyConfig.statTypeDatabase != null
                ? enemyConfig.statTypeDatabase
                : defaultDatabase;

            Undo.RecordObject(enemyConfig, "Refresh Enemy Stats");
            enemyConfig.stats ??= new StatContainer();
            int changed = enemyConfig.stats.Synchronize(database);
            changedContainers += changed > 0 ? 1 : 0;

            if (changed > 0)
                EditorUtility.SetDirty(enemyConfig);
        }

        return guids.Length;
    }

    private static int RefreshSummonConfigs(StatTypeDatabase defaultDatabase, out int changedContainers)
    {
        changedContainers = 0;
        int processedCount = 0;

        // Find all AbilityDataConfig assets in the project folder
        string[] guids = AssetDatabase.FindAssets("t:AbilityDataConfig");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AbilityDataConfig abilityConfig = AssetDatabase.LoadAssetAtPath<AbilityDataConfig>(path);
            if (abilityConfig == null) continue;

            // Only process if it is structurally flagged as a summon ability with an active config
            if (abilityConfig.isSummonAbility && abilityConfig.summonConfig != null)
            {
                processedCount++;
                Undo.RecordObject(abilityConfig, "Refresh Summon Stats");

                // Secure allocation: ensure the nested container isn't null
                abilityConfig.summonConfig.statContainer ??= new StatContainer();

                // Run your standard synchronization pipeline to populate the fields
                int changed = abilityConfig.summonConfig.statContainer.Synchronize(defaultDatabase);
                changedContainers += changed > 0 ? 1 : 0;

                if (changed > 0)
                {
                    EditorUtility.SetDirty(abilityConfig);
                }
            }
        }
        return processedCount;

    }
}
#endif
