using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages saving and loading CharacterData ScriptableObjects as project assets.
/// This makes runtime-created characters editable in the inspector.
/// </summary>
public static class CharacterDataAssetManager
{
#if UNITY_EDITOR
    private const string CHARACTER_ASSETS_PATH = "Assets/CharacterData/SavedCharacters/";
    
    /// <summary>
    /// Save a CharacterData as a ScriptableObject asset in the project.
    /// This allows editing the character in the inspector.
    /// </summary>
    public static void SaveAsAsset(CharacterData characterData)
    {
        if (characterData == null)
        {
            Debug.LogError("[CharacterDataAssetManager] Cannot save null CharacterData");
            return;
        }

        Debug.Log($"[CharacterDataAssetManager] SaveAsAsset called for '{characterData.characterName}'");
        
        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder("Assets/CharacterData"))
        {
            Debug.Log("[CharacterDataAssetManager] Creating Assets/CharacterData folder");
            AssetDatabase.CreateFolder("Assets", "CharacterData");
        }
        
        if (!AssetDatabase.IsValidFolder(CHARACTER_ASSETS_PATH.TrimEnd('/')))
        {
            Debug.Log("[CharacterDataAssetManager] Creating Assets/CharacterData/SavedCharacters folder");
            AssetDatabase.CreateFolder("Assets/CharacterData", "SavedCharacters");
        }
        
        // Create asset path
        string assetPath = $"{CHARACTER_ASSETS_PATH}Character_{characterData.characterName}.asset";
        Debug.Log($"[CharacterDataAssetManager] Target asset path: {assetPath}");
        
        // CRITICAL: Set the ScriptableObject's name to match the filename
        characterData.name = $"Character_{characterData.characterName}";
        
        // CRITICAL: Manually trigger serialization to convert dictionaries to lists
        if (characterData is ISerializationCallbackReceiver receiver)
        {
            receiver.OnBeforeSerialize();
            Debug.Log($"[CharacterDataAssetManager] Serialization callback invoked for '{characterData.characterName}'");
        }
        
        // Check if asset already exists
        CharacterData existingAsset = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
        
        if (existingAsset != null)
        {
            Debug.Log($"[CharacterDataAssetManager] Updating existing asset at {assetPath}");
            EditorUtility.CopySerialized(characterData, existingAsset);
            EditorUtility.SetDirty(existingAsset);
        }
        else
        {
            Debug.Log($"[CharacterDataAssetManager] Creating new asset at {assetPath}");
            AssetDatabase.CreateAsset(characterData, assetPath);
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CharacterDataAssetManager] Asset save complete for '{characterData.characterName}'");
    }
    
    /// <summary>
    /// Load a CharacterData asset from the project
    /// </summary>
    public static CharacterData LoadFromAsset(string characterName)
    {
        string assetPath = $"{CHARACTER_ASSETS_PATH}Character_{characterName}.asset";
        CharacterData characterData = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
        
        if (characterData != null)
        {
            Debug.Log($"[CharacterDataAssetManager] Loaded character from asset: {characterName}");
        }
        else
        {
            Debug.LogWarning($"[CharacterDataAssetManager] Character asset not found: {assetPath}");
        }
        
        return characterData;
    }
    
    /// <summary>
    /// Delete a CharacterData asset from the project
    /// </summary>
    public static bool DeleteAsset(string characterName)
    {
        string assetPath = $"{CHARACTER_ASSETS_PATH}Character_{characterName}.asset";
        
        if (AssetDatabase.DeleteAsset(assetPath))
        {
            Debug.Log($"[CharacterDataAssetManager] Deleted character asset: {characterName}");
            AssetDatabase.Refresh();
            return true;
        }
        
        Debug.LogWarning($"[CharacterDataAssetManager] Failed to delete character asset: {assetPath}");
        return false;
    }
#endif
    
    /// <summary>
    /// Runtime method to check if character assets should be saved
    /// </summary>
    public static void SaveCharacterAsAsset(CharacterData characterData)
    {
#if UNITY_EDITOR
        Debug.Log($"[CharacterDataAssetManager] SaveCharacterAsAsset called for '{characterData?.characterName}' (isPlaying={Application.isPlaying})");
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CharacterDataAssetManager] SaveCharacterAsAsset should only be called at runtime");
            return;
        }
        
        SaveAsAsset(characterData);
#else
        Debug.LogWarning("[CharacterDataAssetManager] Character asset saving only works in the editor");
#endif
    }
}
