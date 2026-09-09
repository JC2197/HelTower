using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic ability HUD. Instantiates one AbilitySlotUI prefab per keybind-able ability:
/// - Weapon ability (LMB)
/// - Dash ability (Space)
/// - Active trait abilities (Keys 1, 2, 3...)
/// Passive and autocast abilities are NOT shown (they don't need keybinds).
/// </summary>
public class PlayerAbilityDisplay : MonoBehaviour
{
    [Header("Dynamic Layout")]
    [Tooltip("Parent RectTransform with a HorizontalLayoutGroup component.")]
    [SerializeField] private RectTransform abilityContainer;
    [Tooltip("Prefab with an AbilitySlotUI component. Instantiated once per active ability.")]
    [SerializeField] private AbilitySlotUI abilityIconPrefab;

    private PlayerController player;
    private CharacterAbilityManager subscribedManager;

    // Track spawned icons so we can rebuild when abilities change
    private readonly List<AbilitySlotUI> spawnedIcons = new List<AbilitySlotUI>();

    void OnEnable()
    {
        PlayerController.OnPlayerSpawned += HandlePlayerSpawned;
        FindAndLoadPlayer();
    }

    void OnDisable()
    {
        PlayerController.OnPlayerSpawned -= HandlePlayerSpawned;
        UnsubscribeFromAbilityEvents();
    }

    // -- Player wiring --

    private void HandlePlayerSpawned(PlayerController newPlayer)
    {
        bool isNetworkActive = newPlayer.IsServerStarted || newPlayer.IsClientStarted;
        bool isLocalPlayer = !isNetworkActive || newPlayer.IsOwner;
        if (!isLocalPlayer) return;

        player = newPlayer;
        SubscribeToAbilityEvents();
        RebuildIcons();
        Debug.Log("[PlayerAbilityDisplay] Player spawned, ability icons built");
    }

    void FindAndLoadPlayer()
    {
        if (player == null)
            player = PlayerController.GetLocalPlayer();

        if (player != null)
        {
            SubscribeToAbilityEvents();
            RebuildIcons();
        }
        else
        {
            Invoke(nameof(FindAndLoadPlayer), 0.1f);
        }
    }

    /// <summary>Reload the ability display - call after weapon swaps or ability changes.</summary>
    public void RefreshAbilityDisplay()
    {
        RebuildIcons();
    }

    // -- Icon building --

    /// <summary>
    /// Clears all existing icons and instantiates one per keybind-able ability.
    /// Order: Weapon (LMB), Dash (Space), Active Traits (1, 2, 3...)
    /// </summary>
    void RebuildIcons()
    {
        ClearIcons();

        if (abilityIconPrefab == null)
        {
            Debug.LogWarning("[PlayerAbilityDisplay] abilityIconPrefab is not assigned!");
            return;
        }

        CharacterAbilityManager abilityManager = player?.GetComponent<CharacterAbilityManager>();
        if (abilityManager == null)
        {
            BuildSelectedCharacterIcons();
            return;
        }

        // Weapon ability (Slot 0 = LMB)
        var weaponRef = abilityManager.GetPrimaryAbilityRef();
        if (weaponRef?.Config != null)
        {
            SpawnIcon(weaponRef, abilityManager.GetPrimaryAbility(), 0);
        }

        // Secondary weapon ability (Slot 1 = RMB)
        var secondaryRef = abilityManager.GetSecondaryAbilityRef();
        if (secondaryRef?.Config != null)
        {
            SpawnIcon(secondaryRef, abilityManager.GetSecondaryAbility(), 1);
        }

        // Dash ability (Slot 2 = Shift)
        var dashRef = abilityManager.GetDashAbilityRef();
        if (dashRef?.Config != null)
        {
            SpawnIcon(dashRef, abilityManager.GetDashAbility(), 2);
        }

        // Passive ability (Slot -1 = Auto)
        var passiveRef = abilityManager.GetPassiveAbilityRef();
        if (passiveRef?.Config != null)
        {
            SpawnIcon(passiveRef, abilityManager.GetPassiveAbility(), -1);
        }

        // Active trait abilities (Slots 2+ = Keys 1, 2, 3...)
        var traitRefs = abilityManager.GetActiveTraitAbilityRefs();
        var traitAbilities = abilityManager.GetActiveTraitAbilities();
        for (int i = 0; i < traitRefs.Count; i++)
        {
            if (traitRefs[i]?.Config != null)
            {
                SpawnIcon(traitRefs[i], traitAbilities[i], 3 + i);
            }
        }

        // Autocast/Passive trait abilities (Slot -1 = Auto)
        var passiveRefs = abilityManager.GetPassiveTraitAbilityRefs();
        var passiveAbilities = abilityManager.GetPassiveTraitAbilities();
        for (int i = 0; i < passiveRefs.Count; i++)
        {
            if (passiveRefs[i]?.Config != null)
            {
                SpawnIcon(passiveRefs[i], passiveAbilities[i], -1); // -1 = autocast slot
            }
        }

        Debug.Log($"[PlayerAbilityDisplay] Built {spawnedIcons.Count} ability icons (including {passiveRefs.Count} autocast)");
    }

    private void BuildSelectedCharacterIcons()
    {
        CharacterAbilityLoadout loadout = CharacterSelectionManager.SelectedCharacter?.abilityLoadout;
        if (loadout == null)
            return;

        SpawnSelectedCharacterIcon(loadout.WeaponAbility, 0);
        SpawnSelectedCharacterIcon(loadout.SecondaryWeaponAbility, 1);
        SpawnSelectedCharacterIcon(loadout.DashAbility, 2);
        SpawnSelectedCharacterIcon(loadout.PassiveAbility, -1);

        List<AbilityReference> activeTraits = loadout.GetActiveTraitAbilities();
        for (int i = 0; i < activeTraits.Count; i++)
            SpawnSelectedCharacterIcon(activeTraits[i], 3 + i);

        foreach (AbilityReference passiveTrait in loadout.GetPassiveTraitAbilities())
            SpawnSelectedCharacterIcon(passiveTrait, -1);

        Debug.Log($"[PlayerAbilityDisplay] Built {spawnedIcons.Count} selected-character ability icons");
    }

    private void SpawnSelectedCharacterIcon(AbilityReference reference, int slotIndex)
    {
        if (reference?.Config != null)
            SpawnIcon(reference, null, slotIndex);
    }

    private void SpawnIcon(AbilityReference reference, Ability ability, int slotIndex)
    {
        AbilitySlotUI icon = Instantiate(abilityIconPrefab, abilityContainer);
        icon.SetAbility(reference, ability);
        
        // Set the keybind text (e.g., "LMB", "Space", "1", "2", etc.)
        string keybind = InputHelper.GetKeybindForSlot(slotIndex);
        icon.SetKeybindText(keybind);
        
        spawnedIcons.Add(icon);
    }

    private void ClearIcons()
    {
        foreach (var icon in spawnedIcons)
        {
            if (icon != null)
                Destroy(icon.gameObject);
        }
        spawnedIcons.Clear();
    }

    // -- Ability-change event wiring --

    private void SubscribeToAbilityEvents()
    {
        UnsubscribeFromAbilityEvents();
        if (player == null) return;

        subscribedManager = player.GetComponent<CharacterAbilityManager>();
        if (subscribedManager != null)
        {
            subscribedManager.OnPrimaryAbilityChanged += OnAbilityChanged;
            subscribedManager.OnSecondaryAbilityChanged += OnAbilityChanged;
            subscribedManager.OnDashAbilityChanged += OnAbilityChanged;
            subscribedManager.OnPassiveAbilityChanged += OnAbilityChanged;
            subscribedManager.OnTraitAbilitiesChanged += OnTraitAbilitiesChanged;
            subscribedManager.OnAbilitiesLoaded += OnAbilitiesLoaded;
        }
    }

    private void UnsubscribeFromAbilityEvents()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnPrimaryAbilityChanged -= OnAbilityChanged;
            subscribedManager.OnSecondaryAbilityChanged -= OnAbilityChanged;
            subscribedManager.OnDashAbilityChanged -= OnAbilityChanged;
            subscribedManager.OnPassiveAbilityChanged -= OnAbilityChanged;
            subscribedManager.OnTraitAbilitiesChanged -= OnTraitAbilitiesChanged;
            subscribedManager.OnAbilitiesLoaded -= OnAbilitiesLoaded;
            subscribedManager = null;
        }
    }

    private void OnAbilityChanged(AbilityReference abilityRef, Ability ability)
    {
        RebuildIcons();
        string abilityName = abilityRef?.AbilityName ?? "None";
        Debug.Log($"[PlayerAbilityDisplay] Rebuilt icons after ability change: {abilityName}");
    }

    private void OnTraitAbilitiesChanged()
    {
        RebuildIcons();
        Debug.Log("[PlayerAbilityDisplay] Rebuilt icons after trait abilities changed");
    }

    private void OnAbilitiesLoaded()
    {
        RebuildIcons();
        Debug.Log("[PlayerAbilityDisplay] Rebuilt icons after character ability loadout changed");
    }
}