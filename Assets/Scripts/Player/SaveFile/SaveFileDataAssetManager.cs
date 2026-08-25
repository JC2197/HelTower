using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Mirrors runtime <see cref="SaveFileData"/> instances into project assets so save files
/// can be inspected and edited in the editor. Editor-only; a no-op in builds.
/// </summary>
public static class SaveFileDataAssetManager
{
#if UNITY_EDITOR
    private const string SAVEFILE_ROOT_FOLDER = "Assets/SaveFileData";
    private const string SAVEFILE_ASSETS_PATH = SAVEFILE_ROOT_FOLDER + "/SavedSaveFiles";

    public static void SaveAsAsset(SaveFileData saveFile)
    {
        if (saveFile == null || string.IsNullOrEmpty(saveFile.saveFileName))
        {
            Debug.LogError("[SaveFileDataAssetManager] Cannot save a null or unnamed SaveFileData.");
            return;
        }

        EnsureFolders();

        string assetPath = GetAssetPath(saveFile.saveFileName);
        saveFile.name = $"SaveFile_{saveFile.saveFileName}";

        SaveFileData existingAsset = AssetDatabase.LoadAssetAtPath<SaveFileData>(assetPath);
        if (existingAsset != null)
        {
            // Copying keeps the existing asset's GUID so config/scene references stay intact.
            EditorUtility.CopySerialized(saveFile, existingAsset);
            EditorUtility.SetDirty(existingAsset);
        }
        else
        {
            AssetDatabase.CreateAsset(Object.Instantiate(saveFile), assetPath);
        }

        AssetDatabase.SaveAssets();
    }

    public static SaveFileData LoadFromAsset(string saveFileName)
    {
        if (string.IsNullOrEmpty(saveFileName))
            return null;

        return AssetDatabase.LoadAssetAtPath<SaveFileData>(GetAssetPath(saveFileName));
    }

    public static bool DeleteAsset(string saveFileName)
    {
        if (string.IsNullOrEmpty(saveFileName))
            return false;

        string assetPath = GetAssetPath(saveFileName);
        if (AssetDatabase.LoadAssetAtPath<SaveFileData>(assetPath) == null)
            return false;

        return AssetDatabase.DeleteAsset(assetPath);
    }

    private static string GetAssetPath(string saveFileName) => $"{SAVEFILE_ASSETS_PATH}/SaveFile_{saveFileName}.asset";

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(SAVEFILE_ROOT_FOLDER))
            AssetDatabase.CreateFolder("Assets", "SaveFileData");

        if (!AssetDatabase.IsValidFolder(SAVEFILE_ASSETS_PATH))
            AssetDatabase.CreateFolder(SAVEFILE_ROOT_FOLDER, "SavedSaveFiles");
    }
#endif

    /// <summary>Runtime entry point — writes the asset only while playing in the editor.</summary>
    public static void SaveFileAsAsset(SaveFileData saveFile)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;

        SaveAsAsset(saveFile);
#endif
    }
}
