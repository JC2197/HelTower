using UnityEngine;
using System.Collections.Generic;

public class CharacterSelectionManager : MonoBehaviour
{
    public static CharacterSelectionManager Instance { get; private set; }
    public static CharacterData SelectedCharacter { get; private set; }
    
    // Track runtime-created characters for cleanup
    private static List<CharacterData> runtimeCharacters = new List<CharacterData>();

    [Header("Configuration")]
    [SerializeField] private CharacterSelectionConfig config;

    private void Awake()
    {
        
        // Singleton pattern - only one instance should exist
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persists across scene changes
            
            // Auto-load config from Resources if not assigned
            if (config == null)
            {
                config = Resources.Load<CharacterSelectionConfig>("CharacterSelectionConfig");
                if (config == null)
                {
                    Debug.LogError("CharacterSelectionConfig not found! Create it at Assets/Resources/CharacterSelectionConfig.asset");
                }
                else
                {
                    Debug.Log("Auto-loaded CharacterSelectionConfig from Resources");
                }
            }
            LoadSelectedCharacter();
        }
        else
        {
            Debug.Log("CharacterSelectionManager already exists, destroying duplicate");
            Destroy(gameObject);
        }
    }

    public void SelectCharacter(CharacterData characterData)
    {
        
        if (config == null)
        {
            Debug.LogError("CharacterSelectionConfig is not assigned!");
            return;
        }
        
        if (characterData == null)
        {
            return;
        }

        SetSelectedCharacter(characterData);
    }

    /// <summary>
    /// Set the character the next run will use. Static so the main menu can select a
    /// character before any CharacterSelectionManager instance exists in the scene.
    /// </summary>
    public static void SetSelectedCharacter(CharacterData characterData)
    {
        if (characterData == null)
            return;

        SelectedCharacter = characterData;
        RegisterRuntimeCharacter(characterData);
    }

    public ClassData GetClassByIndex(int index)
    {
        if (config == null)
        {
            Debug.LogError("CharacterSelectionConfig is not assigned!");
            return null;
        }
        
        return config.GetClassByIndex(index);
    }

    private void LoadSelectedCharacter()
    {
        // Run-based game: no persistent characters. A CharacterData is generated
        // from the chosen ClassData at run start, so nothing is loaded here.
        SelectedCharacter = null;
    }
    
    public ClassData[] GetAvailableClasses()
    {
        return config != null ? config.availableClasses : null;
    }
    
    // Method for your game scene to get the selected character
    public static CharacterData GetSelectedCharacter()
    {
        return SelectedCharacter;
    }
    
    /// <summary>
    /// Register a runtime-created character for cleanup tracking
    /// </summary>
    public static void RegisterRuntimeCharacter(CharacterData character)
    {
        if (character != null && !runtimeCharacters.Contains(character))
        {
            runtimeCharacters.Add(character);
            Debug.Log($"[CharacterSelectionManager] Registered runtime character: {character.displayName}");
        }
    }
    
    /// <summary>
    /// Cleanup all runtime-created characters
    /// Call this when returning to menu or when done with characters
    /// </summary>
    public static void CleanupRuntimeCharacters()
    {
       
        foreach (var character in runtimeCharacters)
        {
            if (character != null)
            {
                Debug.Log($"[CharacterSelectionManager] Destroying runtime character: {character.displayName}");
                Destroy(character);
            }
        }
        
        runtimeCharacters.Clear();
        SelectedCharacter = null;
    }

    private void OnDestroy()
    {
        Debug.Log("[CharacterSelectionManager] is being destroyed!");
        // Don't cleanup here - let the menu handle cleanup explicitly
    }
    
    private void OnApplicationQuit()
    {
        CleanupRuntimeCharacters();
    }
}