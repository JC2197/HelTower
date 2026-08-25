using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks the active save file and every runtime-created <see cref="SaveFileData"/> instance
/// so they can be destroyed when returning to the menu.
/// </summary>
public static class SaveFileSelectionManager
{
    private static readonly Dictionary<string, SaveFileData> runtimeSaveFiles = new Dictionary<string, SaveFileData>();

    /// <summary>The save file currently being played. Null in the pre-game menu.</summary>
    public static SaveFileData ActiveSaveFile { get; private set; }

    public static void SetActiveSaveFile(SaveFileData saveFile)
    {
        ActiveSaveFile = saveFile;
        RegisterRuntimeSaveFile(saveFile);
    }

    public static void RegisterRuntimeSaveFile(SaveFileData saveFile)
    {
        if (saveFile == null || string.IsNullOrEmpty(saveFile.saveFileName))
            return;

        runtimeSaveFiles[saveFile.saveFileName] = saveFile;
    }

    public static SaveFileData GetRuntimeSaveFile(string saveFileName)
    {
        if (string.IsNullOrEmpty(saveFileName))
            return null;

        return runtimeSaveFiles.TryGetValue(saveFileName, out SaveFileData saveFile) ? saveFile : null;
    }

    public static void CleanupRuntimeSaveFiles()
    {
        foreach (SaveFileData saveFile in runtimeSaveFiles.Values)
        {
            if (saveFile != null)
                Object.Destroy(saveFile);
        }

        runtimeSaveFiles.Clear();
        ActiveSaveFile = null;
    }
}
