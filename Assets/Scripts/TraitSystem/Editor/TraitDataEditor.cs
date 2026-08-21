#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TraitData)), CanEditMultipleObjects]
public class TraitDataEditor : Editor
{
    private float findValue = 1f;
    private float replaceValue = 3f;
    private SerializedProperty traitIDProp;
    private SerializedProperty displayNameProp;
    private SerializedProperty descriptionProp;
    private SerializedProperty traitIconProp;
    private SerializedProperty weaponTraitTagProp;
    private SerializedProperty traitTypeProp;
    private SerializedProperty statModifiersProp;
    private SerializedProperty effectScriptProp;
    private SerializedProperty abilityReplacementProp;
    private SerializedProperty unlockedAbilitiesProp;
    private SerializedProperty weaponAmmoModifierProp;
    private SerializedProperty requiredAbilityProp;
    private SerializedProperty requiredAbilityLevelProp;
    private SerializedProperty requiredTraitsProp;
    private SerializedProperty abilityConfigModifiersProp;
    private SerializedProperty mutuallyExclusiveWithProp;

    private void OnEnable()
    {
        traitIDProp = serializedObject.FindProperty("traitID");
        displayNameProp = serializedObject.FindProperty("displayName");
        descriptionProp = serializedObject.FindProperty("description");
        traitIconProp = serializedObject.FindProperty("traitIcon");
        weaponTraitTagProp = serializedObject.FindProperty("weaponTraitTag");
        traitTypeProp = serializedObject.FindProperty("traitType");
        statModifiersProp = serializedObject.FindProperty("statModifiers");
        effectScriptProp = serializedObject.FindProperty("effectScript");
        abilityReplacementProp = serializedObject.FindProperty("abilityReplacement");
        unlockedAbilitiesProp = serializedObject.FindProperty("unlockedAbilities");
        weaponAmmoModifierProp = serializedObject.FindProperty("weaponAmmoModifier");
        requiredAbilityProp = serializedObject.FindProperty("requiredAbility");
        requiredAbilityLevelProp = serializedObject.FindProperty("requiredAbilityLevel");
        requiredTraitsProp = serializedObject.FindProperty("requiredTraits");
        abilityConfigModifiersProp = serializedObject.FindProperty("abilityConfigModifiers");
        mutuallyExclusiveWithProp = serializedObject.FindProperty("mutuallyExclusiveWith");
    }

    public override void OnInspectorGUI()
    {
        TraitData trait = (TraitData)target;
        serializedObject.Update();

        DrawIdentitySection();
        DrawVisualSection();
        DrawTagSection();
        DrawTypeSection();
        DrawTypeSpecificSection();
        DrawMutualExclusionSection();
        serializedObject.ApplyModifiedProperties();

        // Disable value replacement tool for multi-object editing
        if (targets.Length > 1)
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox("Value Replacement Tool is not available when editing multiple objects.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Value Replacement Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Replace all occurrences of a specific value across all modifiers in this trait. " +
            "This affects stat modifiers, ability modifiers, tag modifiers, and status effect values.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Find:", GUILayout.Width(40));
        findValue = EditorGUILayout.FloatField(findValue, GUILayout.Width(60));
        EditorGUILayout.LabelField("→ Replace with:", GUILayout.Width(100));
        replaceValue = EditorGUILayout.FloatField(replaceValue, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button($"Replace All {findValue} → {replaceValue}", GUILayout.Height(30)))
        {
            int replacementCount = ReplaceValues(trait, findValue, replaceValue);

            if (replacementCount > 0)
            {
                EditorUtility.SetDirty(trait);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Replacement Complete",
                    $"Replaced {replacementCount} value(s) in this trait.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No Changes",
                    $"No values matching {findValue} were found in this trait.",
                    "OK");
            }
        }

        EditorGUILayout.Space(10);
    }

    private void DrawIdentitySection()
    {
        EditorGUILayout.LabelField("Trait Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(traitIDProp);
        EditorGUILayout.PropertyField(displayNameProp);
        EditorGUILayout.PropertyField(descriptionProp);
        EditorGUILayout.Space(8f);
    }

    private void DrawVisualSection()
    {
        EditorGUILayout.LabelField("Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(traitIconProp);
        EditorGUILayout.Space(8f);
    }

    private void DrawTagSection()
    {
        EditorGUILayout.LabelField("Weapon Tag", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(weaponTraitTagProp);

        EditorGUILayout.Space(8f);
    }

    private void DrawTypeSection()
    {
        EditorGUILayout.LabelField("Trait Type", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(traitTypeProp);
        EditorGUILayout.Space(8f);
    }

    private void DrawTypeSpecificSection()
    {
        TraitType traitType = GetSelectedTraitType();

        switch (traitType)
        {
            case TraitType.General:
                // Optional ability requirement for ability-specific general traits
                EditorGUILayout.LabelField("Ability Requirement (Optional)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(requiredAbilityProp, new GUIContent("Required Ability", "If set, this trait only appears when the player owns this ability"));
                if (requiredAbilityProp.objectReferenceValue != null)
                {
                    EditorGUILayout.PropertyField(requiredAbilityLevelProp, new GUIContent("Required Level", "Minimum ability level needed. 0 = just need to own it."));
                }
                EditorGUILayout.PropertyField(requiredTraitsProp, true);
                EditorGUILayout.Space(4f);
                EditorGUILayout.PropertyField(statModifiersProp, true);
                EditorGUILayout.PropertyField(effectScriptProp);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Ability Config Modifiers (Optional)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Directly modify cooldown, attack speed, charges, or energy cost on specific abilities using asset references instead of name strings.", MessageType.None);
                EditorGUILayout.PropertyField(abilityConfigModifiersProp, true);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Ammo Modifier (Optional)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Modify magazine size and reload time for abilities that use ammo (like weapon abilities).", MessageType.None);
                EditorGUILayout.PropertyField(weaponAmmoModifierProp, true);
                break;

            case TraitType.Ability:
                EditorGUILayout.PropertyField(unlockedAbilitiesProp, true);
                EditorGUILayout.PropertyField(abilityReplacementProp, true);
                break;

            case TraitType.AbilityUpgrade:
                EditorGUILayout.LabelField("Ability Requirement", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(requiredAbilityProp, new GUIContent("Required Ability", "The ability that must be owned for this upgrade to appear (including weapon abilities like Snipe)"));
                EditorGUILayout.PropertyField(requiredAbilityLevelProp, new GUIContent("Required Level", "Minimum ability level needed. 0 = just need to own it. 5 = max level for replacement upgrades."));

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Trait Requirement", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(requiredTraitsProp, true);
                EditorGUILayout.PropertyField(abilityReplacementProp, true);
                EditorGUILayout.PropertyField(statModifiersProp, true);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Ability Config Modifiers", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Directly modify cooldown, attack speed, charges, or energy cost on the target ability.", MessageType.None);
                EditorGUILayout.PropertyField(abilityConfigModifiersProp, true);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Ammo Modifier", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Modify magazine size and reload time for abilities that use ammo (like weapon abilities).", MessageType.None);
                EditorGUILayout.PropertyField(weaponAmmoModifierProp, true);
                break;

            case TraitType.Keystone:
                //EditorGUILayout.PropertyField(requiredTagProp);
                 EditorGUILayout.PropertyField(statModifiersProp, true);
                EditorGUILayout.PropertyField(effectScriptProp);
                EditorGUILayout.PropertyField(abilityReplacementProp, true);
                EditorGUILayout.PropertyField(unlockedAbilitiesProp, true);
                break;
        }

        EditorGUILayout.Space(8f);
    }

    private void DrawMutualExclusionSection()
    {
        EditorGUILayout.LabelField("Mutual Exclusion", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This trait will not roll if any trait in this list has already been taken by the player.", MessageType.None);
        EditorGUILayout.PropertyField(mutuallyExclusiveWithProp, new GUIContent("Mutually Exclusive With"), true);
        EditorGUILayout.Space(8f);
    }


    private TraitType GetSelectedTraitType()
    {
        // Use intValue instead of enumValueIndex because our enum has explicit values
        // (General=0, Ability=3, AbilityUpgrade=4, Keystone=5)
        return (TraitType)traitTypeProp.intValue;
    }

    private int ReplaceValues(TraitData trait, float find, float replace)
    {
        int count = 0;

        // Replace in stat modifiers
        if (trait.statModifiers != null)
        {
            foreach (var modifier in trait.statModifiers)
            {
                if (Mathf.Approximately(modifier.value, find))
                {
                    modifier.value = replace;
                    count++;
                }
            }
        }

        return count;
    }
}
#endif
