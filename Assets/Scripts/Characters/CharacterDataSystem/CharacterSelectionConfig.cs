using UnityEngine;

[CreateAssetMenu(fileName = "CharacterSelectionConfig", menuName = "Characters/Character Selection Configuration")]
public class CharacterSelectionConfig : ScriptableObject
{
    [Header("Available Classes")]
    [Tooltip("All character classes available for selection (character creation)")]
    public ClassData[] availableClasses;
    
    [Header("Default Class")]
    [Tooltip("Class to use if none is selected or saved data is invalid")]
    public ClassData defaultClass;
    
    [Header("Scene Navigation")]
    [Tooltip("Scene to load when starting the game")]
    public string gameSceneName = "CommandScene";
    
    [Tooltip("Scene to return to when canceling")]
    public string mainMenuSceneName = "MainMenu";
    
    /// <summary>
    /// Creates a runtime character instance from a ClassData template.
    /// Run-based: no experience, no persistence, no gear slots. The chosen weapon
    /// supplies the primary ability; the class ability list is the rollable pool.
    /// </summary>
    /// <param name="classData">The class template</param>
    /// <param name="characterName">Custom character name (optional)</param>
    /// <param name="applyStatConversions">Whether to apply stat conversions</param>
    /// <param name="chosenWeapon">Weapon selected for this run (defaults to the first available)</param>
    public CharacterData CreateCharacterFromClass(ClassData classData, string characterName = null, bool applyStatConversions = true, WeaponConfig chosenWeapon = null)
    {
        if (classData == null)
        {
            Debug.LogError("[CharacterSelectionConfig] Cannot create character from null ClassData!");
            return null;
        }

        CharacterData newCharacter = ScriptableObject.CreateInstance<CharacterData>();

        // Identity
        newCharacter.characterName = characterName ?? classData.className;
        newCharacter.displayName = characterName ?? classData.className;
        newCharacter.classData = classData;

        // STEP 1: Copy baseStatContainer directly from ClassData
        newCharacter.baseStatContainer = new StatContainer();
        newCharacter.baseStatContainer.InitializeFromDatabase();
        foreach (var stat in classData.baseStatContainer.GetAllStats())
        {
            if (stat.CurrentValue != 0f)
                newCharacter.baseStatContainer.SetStat(stat.StatId, stat.CurrentValue);
        }

        // STEP 2: statContainer starts as a copy of baseStatContainer
        newCharacter.statContainer = new StatContainer();
        newCharacter.statContainer.InitializeFromDatabase();
        foreach (var stat in newCharacter.baseStatContainer.GetAllStats())
        {
            newCharacter.statContainer.SetStat(stat.StatId, stat.CurrentValue);
        }

        // Weapon selection: chosen weapon, or the class's first available weapon
        if (chosenWeapon == null && classData.availableWeapons != null && classData.availableWeapons.Length > 0)
            chosenWeapon = classData.availableWeapons[0];

        newCharacter.hasDualWeapons = false;
        newCharacter.mainHandWeaponConfig = chosenWeapon;
        newCharacter.offHandWeaponConfig = null;

        // Ability loadout: primary ability comes from the chosen weapon.
        // Trait abilities start empty and are rolled during the run from classData.abilities.
        newCharacter.abilityLoadout = new CharacterAbilityLoadout();
        if (chosenWeapon != null && chosenWeapon.grantedPrimaryAbility != null)
            newCharacter.abilityLoadout.SetWeaponAbility(chosenWeapon.grantedPrimaryAbility);

        newCharacter.ResetRunRewardProgression();

        // Register this runtime character for cleanup tracking
        CharacterSelectionManager.RegisterRuntimeCharacter(newCharacter);

        // STEP 3: Apply stat conversions ONLY to statContainer (not baseStatContainer)
        if (applyStatConversions)
            CharacterStatConverter.ApplyConversions(newCharacter);

        Debug.Log($"[CharacterSelectionConfig] Created character '{newCharacter.displayName}' from class '{classData.className}'");
        return newCharacter;
    }
    
    public ClassData GetClassByIndex(int index)
    {
        if (availableClasses != null && index >= 0 && index < availableClasses.Length)
        {
            return availableClasses[index];
        }
        return defaultClass;
    }
    
    public int GetClassIndex(ClassData classData)
    {
        if (availableClasses == null || classData == null)
            return -1;
        
        for (int i = 0; i < availableClasses.Length; i++)
        {
            if (availableClasses[i] == classData)
                return i;
        }
        
        return -1;
    }
    
}
