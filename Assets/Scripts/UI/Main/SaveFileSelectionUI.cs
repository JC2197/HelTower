using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Builds the main menu's save file slot list and starts a run from the chosen slot.
/// Selecting an empty slot creates a new save file; selecting an existing one loads its
/// meta progression (gold, trait points, unlocked trait nodes) before entering the game.
/// </summary>
public class SaveFileSelectionUI : MonoBehaviour
{
    [Header("Configs")]
    [Tooltip("Auto-loaded from Resources/SaveFileCollectionConfig if not assigned.")]
    [SerializeField] private SaveFileCollectionConfig saveFileConfig;

    [Tooltip("Auto-loaded from Resources/CharacterSelectionConfig if not assigned.")]
    [SerializeField] private CharacterSelectionConfig characterSelectionConfig;

    [Header("UI References")]
    [Tooltip("Template button. Cloned once per slot and hidden afterwards.")]
    [SerializeField] private Button slotButtonPrefab;

    [Tooltip("Parent for the generated slot buttons. Defaults to the prefab's parent.")]
    [SerializeField] private Transform slotContainer;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Fallbacks")]
    [Tooltip("Slot count used when no SaveFileCollectionConfig is available.")]
    [Min(1)][SerializeField] private int fallbackSlotCount = 3;

    private readonly List<GameObject> spawnedSlots = new List<GameObject>();

    private void Awake()
    {
        saveFileConfig ??= Resources.Load<SaveFileCollectionConfig>("SaveFileCollectionConfig");
        characterSelectionConfig ??= Resources.Load<CharacterSelectionConfig>("CharacterSelectionConfig");

        if (slotContainer == null && slotButtonPrefab != null)
            slotContainer = slotButtonPrefab.transform.parent;
    }

    // The panel is toggled with SetActive, so rebuild every time it is shown.
    private void OnEnable() => BuildSlotButtons();

    public void BuildSlotButtons()
    {
        if (slotButtonPrefab == null || slotContainer == null)
        {
            Debug.LogError("[SaveFileSelectionUI] slotButtonPrefab / slotContainer are not assigned.");
            return;
        }

        ClearSlotButtons();
        slotButtonPrefab.gameObject.SetActive(false);

        int slotCount = saveFileConfig != null ? saveFileConfig.slotCount : fallbackSlotCount;

        for (int i = 0; i < slotCount; i++)
        {
            int slotIndex = i;
            Button button = Instantiate(slotButtonPrefab, slotContainer);
            button.gameObject.SetActive(true);
            button.onClick.AddListener(() => OnSlotSelected(slotIndex));

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = BuildSlotLabel(slotIndex);

            spawnedSlots.Add(button.gameObject);
        }
    }

    private void ClearSlotButtons()
    {
        foreach (GameObject slot in spawnedSlots)
        {
            if (slot != null)
                Destroy(slot);
        }

        spawnedSlots.Clear();
    }

    private string BuildSlotLabel(int slotIndex)
    {
        string displayName = GetSlotDisplayName(slotIndex);
        string saveFileName = GetSlotSaveFileName(slotIndex);

        if (!SaveFilePersistence.SaveFileExists(saveFileName))
            return $"{displayName}\n<size=70%>Empty — New Game</size>";

        int gold = Mathf.Max(0, SaveFilePersistence.LoadTotalGold(saveFileName));
        // int nodeCount = SaveFilePersistence.LoadUnlockedNodeIDs(saveFileName)?.Count ?? 0;
        int nodeCount = 0;

        return $"{displayName}\n<size=70%>{gold} gold · {nodeCount} traits</size>";
    }

    private void OnSlotSelected(int slotIndex)
    {
        string saveFileName = GetSlotSaveFileName(slotIndex);

        SaveFileData saveFile = SaveFilePersistence.SaveFileExists(saveFileName)
            ? SaveFilePersistence.LoadSaveFile(saveFileName, saveFileConfig)
            : CreateSaveFileForSlot(slotIndex, saveFileName);

        if (saveFile == null)
        {
            Debug.LogError($"[SaveFileSelectionUI] Could not open save file '{saveFileName}'.");
            return;
        }

        SaveFileSelectionManager.SetActiveSaveFile(saveFile);

        CharacterData character = BuildRunCharacter(saveFile);
        if (character != null)
            CharacterSelectionManager.SetSelectedCharacter(character);

        Debug.Log($"[SaveFileSelectionUI] Starting run on '{saveFile.saveFileName}' " +
                  $"(class={saveFile.lastClassName}, nodes={saveFile.unlockedNodeIDs.Count}, gold={saveFile.totalGold}).");

        SceneManager.LoadScene(gameSceneName);
    }

    private SaveFileData CreateSaveFileForSlot(int slotIndex, string saveFileName)
    {
        ClassData classData = ResolveClass(null);

        SaveFileData saveFile = SaveFilePersistence.CreateSaveFile(saveFileName, classData?.className);
        if (saveFile != null)
            saveFile.displayName = GetSlotDisplayName(slotIndex);

        return saveFile;
    }

    /// <summary>
    /// Build the per-run CharacterData for this save file and record the class back onto it,
    /// so the same class is restored the next time the slot is played.
    /// </summary>
    private CharacterData BuildRunCharacter(SaveFileData saveFile)
    {
        if (characterSelectionConfig == null)
        {
            Debug.LogWarning("[SaveFileSelectionUI] No CharacterSelectionConfig — the player will pick a random class on spawn.");
            return null;
        }

        ClassData classData = ResolveClass(saveFile.lastClassName);
        if (classData == null)
        {
            Debug.LogError("[SaveFileSelectionUI] CharacterSelectionConfig has no classes assigned.");
            return null;
        }

        if (saveFile.lastClassName != classData.className)
        {
            saveFile.lastClassName = classData.className;
            SaveFilePersistence.SaveFile(saveFile);
        }

        return characterSelectionConfig.CreateCharacterFromClass(classData);
    }

    private ClassData ResolveClass(string className)
    {
        if (characterSelectionConfig == null)
            return null;

        if (!string.IsNullOrEmpty(className) && characterSelectionConfig.availableClasses != null)
        {
            foreach (ClassData candidate in characterSelectionConfig.availableClasses)
            {
                if (candidate != null && candidate.className == className)
                    return candidate;
            }
        }

        return characterSelectionConfig.GetRandomClass();
    }

    private string GetSlotSaveFileName(int slotIndex) =>
        saveFileConfig != null ? saveFileConfig.GetSlotSaveFileName(slotIndex) : $"Slot{slotIndex + 1}";

    private string GetSlotDisplayName(int slotIndex) =>
        saveFileConfig != null ? saveFileConfig.GetSlotDisplayName(slotIndex) : $"Slot {slotIndex + 1}";
}
