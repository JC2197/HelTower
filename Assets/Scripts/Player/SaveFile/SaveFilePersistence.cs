using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Saves and loads <see cref="SaveFileData"/> (character meta progression) to/from PlayerPrefs as JSON.
/// Only meta progression is persisted here — per-run CharacterData is rebuilt from ClassData each run.
/// </summary>
public static class SaveFilePersistence
{
    private const string SAVE_FILE_LIST_KEY = "SavedSaveFileList";
    private const string SAVE_FILE_KEY_PREFIX = "saveFile_";
    private const int MAX_SAVE_FILES = 9;
    private const string GOLD_EARNED_KEY = "GoldEarned";

    private static readonly HashSet<string> dirtySaveFiles = new HashSet<string>();

    [System.Serializable]
    private class SaveFileSaveData
    {
        public string saveFileName;
        public string displayName;
        public string lastClassName;
        public int totalGold;
        public int researchPoints;
        public int maxLevelMapUnlocked;
        public List<string> unlockedNodeIDs = new List<string>();
        public bool inMap;
    }
    
    [System.Serializable]
    private class SaveFileListData
    {
        public List<string> saveFileNames = new List<string>();
    }

    // ===================== SAVE =====================

    public static void SaveGoldEarned(int gold)
    {
        PlayerPrefs.SetInt(GOLD_EARNED_KEY, gold);
        PlayerPrefs.Save();
    }

    public static int LoadGoldEarned()
    {
        return PlayerPrefs.GetInt(GOLD_EARNED_KEY, 0);
    }

    /// <summary>
    /// Persist a save file to PlayerPrefs. Returns false when the save is rejected
    /// (null/unnamed, not owned by the local player, or slot limit reached).
    /// </summary>
    public static bool SaveFile(SaveFileData saveFile)
    {
        if (saveFile == null || string.IsNullOrEmpty(saveFile.saveFileName))
        {
            Debug.LogError("[SaveFilePersistence] SaveFile: save file is null or unnamed.");
            return false;
        }

        // Pre-game UI runs before any player spawns, so a null local player allows all saves.
        PlayerController localPlayer = PlayerController.GetLocalPlayer();
        if (localPlayer != null && localPlayer.IsSpawned)
        {
            SaveFileData localSaveFile = localPlayer.GetCurrentSaveFileData();
            if (localSaveFile != null && localSaveFile.saveFileName != saveFile.saveFileName)
            {
                Debug.LogWarning($"[SaveFilePersistence] Blocked save for '{saveFile.saveFileName}' — local player owns '{localSaveFile.saveFileName}'.");
                return false;
            }
        }

        bool isNewSaveFile = !SaveFileExists(saveFile.saveFileName);
        if (isNewSaveFile && GetSavedSaveFileCount() >= MAX_SAVE_FILES)
        {
            Debug.LogWarning($"[SaveFilePersistence] Cannot create '{saveFile.saveFileName}' — {MAX_SAVE_FILES} save file limit reached.");
            return false;
        }

        string json = JsonUtility.ToJson(BuildSaveData(saveFile));
        PlayerPrefs.SetString(GetSaveFileKey(saveFile.saveFileName), json);

        if (isNewSaveFile)
            AddToSaveFileList(saveFile.saveFileName);

        PlayerPrefs.Save();

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            // New save files need their asset on disk immediately; updates can be batched.
            if (isNewSaveFile)
                SaveFileDataAssetManager.SaveFileAsAsset(saveFile);
            else
                dirtySaveFiles.Add(saveFile.saveFileName);
        }
#endif

        return true;
    }

    // ===================== LOAD =====================

    /// <summary>
    /// Load a save file from PlayerPrefs into a runtime <see cref="SaveFileData"/> instance.
    /// The config supplies the authored template (trait tree assignment) for the slot.
    /// </summary>
    public static SaveFileData LoadSaveFile(string saveFileName, SaveFileCollectionConfig config)
    {
        if (string.IsNullOrEmpty(saveFileName))
        {
            Debug.LogError("[SaveFilePersistence] LoadSaveFile: save file name is empty.");
            return null;
        }

        string key = GetSaveFileKey(saveFileName);
        if (!PlayerPrefs.HasKey(key))
        {
            Debug.LogWarning($"[SaveFilePersistence] No save data found for: {saveFileName}");
            return null;
        }

        SaveFileSaveData saveData = JsonUtility.FromJson<SaveFileSaveData>(PlayerPrefs.GetString(key));
        if (saveData == null)
        {
            Debug.LogError($"[SaveFilePersistence] Failed to deserialize save file: {saveFileName}");
            return null;
        }

        SaveFileData saveFile = BuildSaveFileFromSaveData(saveData);

        // Safety net for abnormal termination (crash/force-quit): the run never completed,
        // so clear the flag and re-persist. Meta progression is intentionally retained.
        if (saveFile.inMap)
        {
            Debug.LogWarning($"[SaveFilePersistence] '{saveFile.saveFileName}' loaded with inMap=true (run never completed) — clearing run state.");
            saveFile.inMap = false;
            SaveFile(saveFile);
        }

        SaveFileSelectionManager.RegisterRuntimeSaveFile(saveFile);
        return saveFile;
    }

    /// <summary>
    /// Create a brand new save file for a class and persist it immediately.
    /// Returns null if the name is taken or the slot limit is reached.
    /// </summary>
    public static SaveFileData CreateSaveFile(string saveFileName, string className)
    {
        if (string.IsNullOrEmpty(saveFileName))
        {
            Debug.LogError("[SaveFilePersistence] CreateSaveFile: save file name is empty.");
            return null;
        }

        if (SaveFileExists(saveFileName))
        {
            Debug.LogWarning($"[SaveFilePersistence] CreateSaveFile: '{saveFileName}' already exists.");
            return null;
        }

        SaveFileData saveFile = ScriptableObject.CreateInstance<SaveFileData>();
        saveFile.name = $"SaveFile_{saveFileName}";
        saveFile.saveFileName = saveFileName;
        saveFile.displayName = saveFileName;
        saveFile.lastClassName = className;
        saveFile.maxLevelMapUnlocked = 1;

        if (!SaveFile(saveFile))
        {
            Object.Destroy(saveFile);
            return null;
        }

        SaveFileSelectionManager.RegisterRuntimeSaveFile(saveFile);
        return saveFile;
    }

    // ===================== DELETE / QUERY =====================

    public static bool DeleteSaveFile(string saveFileName)
    {
        if (string.IsNullOrEmpty(saveFileName))
        {
            Debug.LogError("[SaveFilePersistence] DeleteSaveFile: save file name is empty.");
            return false;
        }

        string key = GetSaveFileKey(saveFileName);
        if (!PlayerPrefs.HasKey(key))
        {
            Debug.LogWarning($"[SaveFilePersistence] No save data found for: {saveFileName}");
            return false;
        }

        PlayerPrefs.DeleteKey(key);
        RemoveFromSaveFileList(saveFileName);
        PlayerPrefs.Save();

        dirtySaveFiles.Remove(saveFileName);

#if UNITY_EDITOR
        SaveFileDataAssetManager.DeleteAsset(saveFileName);
#endif

        Debug.Log($"[SaveFilePersistence] Deleted save file: {saveFileName}");
        return true;
    }

    public static bool SaveFileExists(string saveFileName)
    {
        return !string.IsNullOrEmpty(saveFileName) && PlayerPrefs.HasKey(GetSaveFileKey(saveFileName));
    }

    public static List<string> GetSavedSaveFileNames()
    {
        if (!PlayerPrefs.HasKey(SAVE_FILE_LIST_KEY))
            return new List<string>();

        SaveFileListData listData = JsonUtility.FromJson<SaveFileListData>(PlayerPrefs.GetString(SAVE_FILE_LIST_KEY));
        return listData?.saveFileNames ?? new List<string>();
    }

    public static int GetSavedSaveFileCount() => GetSavedSaveFileNames().Count;

    public static bool IsAtMaxCapacity() => GetSavedSaveFileCount() >= MAX_SAVE_FILES;

    public static int GetMaxSaveFiles() => MAX_SAVE_FILES;

    // ===================== LIGHTWEIGHT READS =====================

    /// <summary>Read a single save file's persisted blob, or null when it does not exist.</summary>
    private static SaveFileSaveData ReadSaveData(string saveFileName)
    {
        if (string.IsNullOrEmpty(saveFileName))
            return null;

        string key = GetSaveFileKey(saveFileName);
        if (!PlayerPrefs.HasKey(key))
            return null;

        return JsonUtility.FromJson<SaveFileSaveData>(PlayerPrefs.GetString(key));
    }

    /// <summary>Saved gold without constructing a SaveFileData. Returns -1 when no save exists.</summary>
    public static int LoadTotalGold(string saveFileName)
    {
        SaveFileSaveData saveData = ReadSaveData(saveFileName);
        return saveData != null ? saveData.totalGold : -1;
    }

    /// <summary>Saved trait tree node IDs without constructing a SaveFileData. Returns null when no save exists.</summary>
    public static List<string> LoadUnlockedNodeIDs(string saveFileName)
    {
        SaveFileSaveData saveData = ReadSaveData(saveFileName);
        return saveData?.unlockedNodeIDs;
    }

    // ===================== NETWORK SERIALIZATION =====================

    /// <summary>Serialize a save file to JSON without touching PlayerPrefs.</summary>
    public static string SerializeToJson(SaveFileData saveFile)
    {
        if (saveFile == null)
        {
            Debug.LogError("[SaveFilePersistence] SerializeToJson: save file is null.");
            return null;
        }

        return JsonUtility.ToJson(BuildSaveData(saveFile));
    }

    /// <summary>Rebuild a save file received over the network. Does not touch PlayerPrefs.</summary>
    public static SaveFileData DeserializeFromJson(string json, SaveFileCollectionConfig config = null)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[SaveFilePersistence] DeserializeFromJson: json is null or empty.");
            return null;
        }

        SaveFileSaveData saveData = JsonUtility.FromJson<SaveFileSaveData>(json);
        if (saveData == null)
        {
            Debug.LogError("[SaveFilePersistence] DeserializeFromJson: failed to parse JSON.");
            return null;
        }

        config ??= Resources.Load<SaveFileCollectionConfig>("SaveFileCollectionConfig");
        return BuildSaveFileFromSaveData(saveData);
    }

    // ===================== HELPERS =====================

    private static string GetSaveFileKey(string saveFileName) => $"{SAVE_FILE_KEY_PREFIX}{saveFileName}";

    private static SaveFileSaveData BuildSaveData(SaveFileData saveFile)
    {
        return new SaveFileSaveData
        {
            saveFileName = saveFile.saveFileName,
            displayName = saveFile.displayName,
            lastClassName = saveFile.lastClassName,
            totalGold = saveFile.totalGold,
            researchPoints = saveFile.researchPoints,
            maxLevelMapUnlocked = Mathf.Max(1, saveFile.maxLevelMapUnlocked),
            unlockedNodeIDs = saveFile.unlockedNodeIDs != null
                ? new List<string>(saveFile.unlockedNodeIDs)
                : new List<string>(),
            inMap = saveFile.inMap
        };
    }

    private static SaveFileData BuildSaveFileFromSaveData(SaveFileSaveData saveData)
    {
        SaveFileData saveFile = ScriptableObject.CreateInstance<SaveFileData>();
        saveFile.name = $"SaveFile_{saveData.saveFileName}";
        saveFile.saveFileName = saveData.saveFileName;
        saveFile.displayName = string.IsNullOrEmpty(saveData.displayName) ? saveData.saveFileName : saveData.displayName;
        saveFile.lastClassName = saveData.lastClassName;
        saveFile.totalGold = saveData.totalGold;
        saveFile.researchPoints = saveData.researchPoints;
        saveFile.maxLevelMapUnlocked = Mathf.Max(1, saveData.maxLevelMapUnlocked);
        saveFile.SetUnlockedNodes(saveData.unlockedNodeIDs);
        saveFile.inMap = saveData.inMap;

        return saveFile;
    }

    private static void AddToSaveFileList(string saveFileName)
    {
        List<string> names = GetSavedSaveFileNames();
        if (names.Contains(saveFileName))
            return;

        names.Add(saveFileName);
        WriteSaveFileList(names);
    }

    private static void RemoveFromSaveFileList(string saveFileName)
    {
        List<string> names = GetSavedSaveFileNames();
        if (!names.Remove(saveFileName))
            return;

        WriteSaveFileList(names);
    }

    private static void WriteSaveFileList(List<string> saveFileNames)
    {
        string json = JsonUtility.ToJson(new SaveFileListData { saveFileNames = saveFileNames });
        PlayerPrefs.SetString(SAVE_FILE_LIST_KEY, json);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    /// <summary>Write all pending save file assets to disk. Call on application quit or scene unload.</summary>
    public static void FlushDirtySaveFileAssets()
    {
        if (dirtySaveFiles.Count == 0)
            return;

        foreach (string saveFileName in dirtySaveFiles)
        {
            SaveFileData saveFile = SaveFileSelectionManager.GetRuntimeSaveFile(saveFileName);
            if (saveFile != null)
                SaveFileDataAssetManager.SaveFileAsAsset(saveFile);
        }

        dirtySaveFiles.Clear();
    }
#endif
}
