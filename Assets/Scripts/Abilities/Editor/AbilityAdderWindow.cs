#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AbilityAdderWindow : EditorWindow
{
    private readonly List<AbilityDataConfig> abilities = new List<AbilityDataConfig>();
    private Vector2 scrollPosition;
    private string searchText = string.Empty;

    [MenuItem("Tools/Ability Adder")]
    private static void Open()
    {
        GetWindow<AbilityAdderWindow>("Ability Adder");
    }

    private void OnEnable()
    {
        RefreshAbilities();
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnProjectChange()
    {
        RefreshAbilities();
        Repaint();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        Repaint();
    }

    private void OnGUI()
    {
        CharacterData character = ResolveCurrentCharacter(out PlayerController player);

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Current Character", EditorStyles.boldLabel, GUILayout.Width(115f));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(character, typeof(CharacterData), false);
        }

        if (character == null)
        {
            EditorGUILayout.HelpBox("Select a character or enter Play Mode with a local player.", MessageType.Info);
            return;
        }

        if (character.abilityLoadout == null)
        {
            EditorGUILayout.HelpBox("The current character has no ability loadout.", MessageType.Warning);
            if (GUILayout.Button("Create Ability Loadout"))
            {
                Undo.RecordObject(character, "Create Ability Loadout");
                character.abilityLoadout = new CharacterAbilityLoadout();
                EditorUtility.SetDirty(character);
            }
            return;
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            searchText = GUILayout.TextField(searchText, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                RefreshAbilities();
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (AbilityDataConfig ability in abilities)
        {
            if (ability == null || !MatchesSearch(ability))
                continue;

            DrawAbilityRow(character, player, ability);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawAbilityRow(CharacterData character, PlayerController player, AbilityDataConfig ability)
    {
        bool isAdded = character.abilityLoadout.TraitAbilities.Exists(reference => reference?.Config == ability);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            Texture icon = ability.abilityIcon != null ? ability.abilityIcon.texture : null;
            GUILayout.Label(icon, GUILayout.Width(32f), GUILayout.Height(32f));

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(ability.abilityName) ? ability.name : ability.abilityName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Next available ability slot", EditorStyles.miniLabel);
            }

            if (GUILayout.Button("Select", GUILayout.Width(52f), GUILayout.Height(24f)))
                Selection.activeObject = ability;

            using (new EditorGUI.DisabledScope(isAdded))
            {
                if (GUILayout.Button(isAdded ? "Added" : "Add", GUILayout.Width(52f), GUILayout.Height(24f)))
                    AddAbility(character, player, ability);
            }
        }
    }

    private void AddAbility(CharacterData character, PlayerController player, AbilityDataConfig ability)
    {
        Undo.RecordObject(character, $"Add {ability.abilityName}");

        character.abilityLoadout.AddTraitAbility(ability);

        EditorUtility.SetDirty(character);

        if (player != null)
        {
            CharacterAbilityManager manager = player.GetComponent<CharacterAbilityManager>();
            if (manager != null)
                manager.LoadCharacterAbilities(character);
        }

        Repaint();
    }

    private CharacterData ResolveCurrentCharacter(out PlayerController player)
    {
        player = Application.isPlaying ? PlayerController.GetLocalPlayer() : null;
        CharacterData runtimeCharacter = player != null ? player.GetCurrentCharacterData() : null;
        return runtimeCharacter != null ? runtimeCharacter : CharacterSelectionManager.SelectedCharacter;
    }

    private bool MatchesSearch(AbilityDataConfig ability)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        return ability.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
            || (!string.IsNullOrEmpty(ability.abilityName)
                && ability.abilityName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void RefreshAbilities()
    {
        abilities.Clear();
        string[] guids = AssetDatabase.FindAssets("t:AbilityDataConfig");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AbilityDataConfig ability = AssetDatabase.LoadAssetAtPath<AbilityDataConfig>(path);
            if (ability != null)
                abilities.Add(ability);
        }

        abilities.Sort((left, right) => string.Compare(
            string.IsNullOrEmpty(left.abilityName) ? left.name : left.abilityName,
            string.IsNullOrEmpty(right.abilityName) ? right.name : right.abilityName,
            StringComparison.OrdinalIgnoreCase));
    }
}
#endif
