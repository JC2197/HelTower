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

    [Header("Abilities")]
    [Tooltip("Ability-traits this class can roll during a run.")]
    public List<AbilityConfig> abilities = new List<AbilityConfig>();

    [Header("Base Stats")]
    [Tooltip("Starting stat values for this class. Use the context menu 'Initialize Base Stats from Database' if empty.")]
    public StatContainer baseStatContainer = new StatContainer();

    [ContextMenu("Initialize Base Stats from Database")]
    private void InitializeBaseStatsFromDatabase()
    {
        baseStatContainer.InitializeFromDatabase();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[ClassData] Initialized baseStatContainer for '{className}' from StatTypeDatabase.");
#endif
    }
}
