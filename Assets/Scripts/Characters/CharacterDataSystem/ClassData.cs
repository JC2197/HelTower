using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// Defines a character class that can be shared by multiple character instances.
/// Contains the shared appearance, animations, base stats, equippable weapons,
/// and the pool of ability-traits this class can roll during a run.
/// </summary>
[CreateAssetMenu(fileName = "Class_", menuName = "Characters/Class Data")]
public class ClassData : ScriptableObject
{
    [Header("Class Identity")]
    [Tooltip("Internal identifier for this class")]
    public string className;

    [Header("Portrait")]
    [Tooltip("Character portrait sprite")]
    public Sprite characterPortrait;

    [Header("Animation")]
    [Tooltip("Animator controller for this class")]
    public RuntimeAnimatorController animatorController;

    [Header("Weapons")]
    [Tooltip("Weapons this class can choose from. The chosen weapon supplies the primary ability.")]
    public WeaponConfig[] availableWeapons;

    [Header("Accessories")]
    [Tooltip("Accessories available to this class.")]
    public AccessoryConfig[] availableAccessories;

    [Header("Abilities")]
    [Tooltip("Ability-traits this class can roll during a run.")]
    public List<AbilityConfig> abilities = new List<AbilityConfig>();

    [Header("Base Stats")]
    [Tooltip("Starting stat values for this class. Use the context menu 'Initialize Base Stats from Database' if empty.")]
    public StatContainer baseStatContainer = new StatContainer();

    [Header("Trait Trees")]
    [Tooltip("Trait trees this class can open. Tab selection isn't implemented yet, so the first entry is used.")]
    public List<TraitTree> availableTraitTrees = new List<TraitTree>();

    private void OnValidate()
    {
        if (baseStatContainer == null)
            baseStatContainer = new StatContainer();

        StatTypeDatabase database = StatTypeDatabase.Instance;
        if (database == null)
            return;

        int addedStats = baseStatContainer.Synchronize(database);
#if UNITY_EDITOR
        if (addedStats > 0)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Synchronize Base Stats from Database")]
    private void SynchronizeBaseStatsFromDatabase()
    {
        OnValidate();
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[ClassData] Synchronized baseStatContainer for '{className}' with StatTypeDatabase.");
#endif
    }
}
