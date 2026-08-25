using UnityEngine;

[CreateAssetMenu(fileName = "SaveFileCollectionConfig", menuName = "Save Files/SaveFileCollectionConfig")]
public class SaveFileCollectionConfig : ScriptableObject
{
    [Header("Slots")]
    [Tooltip("How many save file slots the main menu offers.")]
    [Min(1)] public int slotCount = 3;

    [Tooltip("Optional authored templates. Matched to a save by saveFileName.")]
    public SaveFileData[] saveFiles;

    /// <summary>Stable persistence key for a slot index (0-based).</summary>
    public string GetSlotSaveFileName(int slotIndex) => $"Slot{slotIndex + 1}";

    /// <summary>Display label for a slot index (0-based).</summary>
    public string GetSlotDisplayName(int slotIndex) => $"Slot {slotIndex + 1}";

    public SaveFileData GetTemplate(string saveFileName)
    {
        if (saveFiles == null || string.IsNullOrEmpty(saveFileName))
            return null;

        foreach (SaveFileData template in saveFiles)
        {
            if (template != null && template.saveFileName == saveFileName)
                return template;
        }

        return null;
    }
}