using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages the player's ability loadout:
/// - Weapon ability (LMB) - granted by equipped weapon
/// - Secondary weapon ability (RMB) - granted by equipped offhand weapon
/// - Active trait abilities - Shift, Q, E, R
/// - Passive trait abilities - autocast, no keybind
/// - Aura abilities - managed by PlayerAuraManager
/// </summary>
public class CharacterAbilityManager : MonoBehaviour
{
    // === Events ===
    /// <summary>Fired when weapon ability changes (weapon swap).</summary>
    public event Action<AbilityReference, Ability> OnPrimaryAbilityChanged;
    public event Action<AbilityReference, Ability> OnSecondaryAbilityChanged;
    public event Action<AbilityReference, Ability> OnDashAbilityChanged;
    public event Action<AbilityReference, Ability> OnPassiveAbilityChanged;
    
    /// <summary>Fired when trait abilities list changes (add/remove/clear).</summary>
    public event Action OnTraitAbilitiesChanged;
    /// <summary>Fired after all runtime abilities have been replaced from a character loadout.</summary>
    public event Action OnAbilitiesLoaded;
    
    // === Core Abilities ===
    private Ability primaryAbility;    
    private AbilityReference primaryAbilityRef;
    // Secondary weapon ability occupies slot 1 (RMB).
    private Ability secondaryAbility;
    private AbilityReference secondaryAbilityRef;
    private Ability dashAbility;
    private AbilityReference dashAbilityRef;
    private Ability passiveAbility;
    private AbilityReference passiveAbilityRef;

    
    // Active trait abilities get dynamic keybinds (slots 2-5 = Shift, Q, E, R)
    private readonly List<Ability> activeTraitAbilities = new List<Ability>();
    private readonly List<AbilityReference> activeTraitAbilityRefs = new List<AbilityReference>();
    
    // Passive trait abilities (autocast - no keybind, fire automatically)
    private readonly List<Ability> passiveTraitAbilities = new List<Ability>();
    private readonly List<AbilityReference> passiveTraitAbilityRefs = new List<AbilityReference>();
    
    

    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (GetComponent<PlayerAuraManager>() == null)
            gameObject.AddComponent<PlayerAuraManager>();
    }

    // ===========================
    // LOADING
    // ===========================
    
    public void LoadCharacterAbilities(CharacterData data)
    {
        if (data == null || data.abilityLoadout == null)
        {
            Debug.LogWarning("[CharacterAbilityManager] Cannot load abilities - missing character data or loadout");
            return;
        }

        ClearExistingAbilities();
        var loadout = data.abilityLoadout;

        // Load Weapon Ability (Slot 0 = LMB)
        if (loadout.WeaponAbility?.Config != null)
        {
            primaryAbilityRef = loadout.WeaponAbility;
            primaryAbility = LoadAbility(primaryAbilityRef, 0);
        }

        // Load Secondary Weapon Ability (Slot 1 = RMB)
        if (loadout.SecondaryWeaponAbility?.Config != null)
        {
            secondaryAbilityRef = loadout.SecondaryWeaponAbility;
            secondaryAbility = LoadAbility(secondaryAbilityRef, 1);
        }

        if (loadout.DashAbility?.Config != null)
        {
            dashAbilityRef = loadout.DashAbility;
            dashAbility = LoadAbility(dashAbilityRef, 2);
        }

        if (loadout.PassiveAbility?.Config != null)
        {
            passiveAbilityRef = loadout.PassiveAbility;
            passiveAbility = LoadAbility(passiveAbilityRef, -1);
        }

        // Load Trait Abilities
        int activeSlot = 3;
        foreach (var traitRef in loadout.TraitAbilities)
        {
            if (traitRef?.Config == null) continue;
            
            var dataConfig = traitRef.Config as AbilityDataConfig;
            if (dataConfig == null) continue;
            
            bool requiresKeybind = dataConfig.RequiresKeybind;
            if (requiresKeybind && activeSlot > 5)
            {
                Debug.LogWarning($"[CharacterAbilityManager] No input slot available for {traitRef.AbilityName}; Shift, Q, E, and R are occupied.");
                continue;
            }

            var ability = LoadAbility(traitRef, requiresKeybind ? activeSlot : -1);
            if (ability != null)
            {
                if (requiresKeybind)
                {
                    activeTraitAbilities.Add(ability);
                    activeTraitAbilityRefs.Add(traitRef);
                    Debug.Log($"[CharacterAbilityManager] Loaded trait slot {activeSlot}: {traitRef.AbilityName}");
                    activeSlot++;
                }
                else
                {
                    passiveTraitAbilities.Add(ability);
                    passiveTraitAbilityRefs.Add(traitRef);
                }
            }
        }

        OnAbilitiesLoaded?.Invoke();
    }

    private Ability LoadAbility(AbilityReference abilityRef, int slotIndex)
    {
        Debug.Log($"[CharacterAbilityManager] LoadAbility called: slot={slotIndex}, abilityRef={abilityRef?.AbilityName ?? "NULL"}");
        
        if (abilityRef?.Config == null)
        {
            Debug.LogWarning("[CharacterAbilityManager] AbilityReference or Config is null");
            return null;
        }

        // Swap to a trait-granted replacement ability if one is active. Callers keep the base
        // reference, so removing the trait and reloading naturally restores the original ability.
        abilityRef = ResolveReplacement(abilityRef);

        // Add DataDrivenAbility component
        var newAbility = gameObject.AddComponent<DataDrivenAbility>();
        if (newAbility == null)
        {
            Debug.LogError("[CharacterAbilityManager] Failed to add DataDrivenAbility component");
            return null;
        }

        newAbility.SetAbilityReference(abilityRef);
        newAbility.SetAbilitySlot(slotIndex);
        newAbility.InitializeAbility();
        
        string slotName = slotIndex switch
        {
            0 => "Weapon (LMB)",
            1 => "Secondary Weapon (RMB)",
            -1 => "Passive/Autocast",
            2 => "Ability (Shift)",
            3 => "Ability (Q)",
            4 => "Ability (E)",
            5 => "Ability (R)",
            _ => $"Ability slot {slotIndex}"
        };
        
        var dataConfig = abilityRef.Config as AbilityDataConfig;
        if (dataConfig?.areaConfig?.isAura == true)
            GetComponent<PlayerAuraManager>().AddAura(dataConfig);

        Debug.Log($"[CharacterAbilityManager] ✓ Loaded {abilityRef.AbilityName} -> {slotName}");
        Debug.Log($"[CharacterAbilityManager]   Config type: {abilityRef.Config.GetType().Name}");
        Debug.Log($"[CharacterAbilityManager]   isAura={dataConfig?.areaConfig?.isAura}, autocast={dataConfig?.autocast}, disableCast={dataConfig?.disableCast}");

        return newAbility;
    }

    private void ClearExistingAbilities()
    {
        // Clear auras
        var auraManager = GetComponent<PlayerAuraManager>();
        if (auraManager != null)
            auraManager.ClearAllAuras();

        // Remove all ability components
        var existingAbilities = GetComponents<Ability>();
        foreach (var ability in existingAbilities)
        {
            if (ability != null)
                Destroy(ability);
        }

        primaryAbility = null;
        primaryAbilityRef = null;
        secondaryAbility = null;
        secondaryAbilityRef = null;
        activeTraitAbilities.Clear();
        activeTraitAbilityRefs.Clear();
        passiveTraitAbilities.Clear();
        passiveTraitAbilityRefs.Clear();
        
    }

    // ===========================
    // GETTERS
    // ===========================
    
    public Ability GetPrimaryAbility() => primaryAbility;
    public AbilityReference GetPrimaryAbilityRef() => primaryAbilityRef;

    public Ability GetSecondaryAbility() => secondaryAbility;
    public AbilityReference GetSecondaryAbilityRef() => secondaryAbilityRef;

    public Ability GetDashAbility() => dashAbility;
    public AbilityReference GetDashAbilityRef() => dashAbilityRef;

    public Ability GetPassiveAbility() => passiveAbility;
    public AbilityReference GetPassiveAbilityRef() => passiveAbilityRef;
    public List<Ability> GetActiveTraitAbilities() => new List<Ability>(activeTraitAbilities);
    public List<AbilityReference> GetActiveTraitAbilityRefs() => new List<AbilityReference>(activeTraitAbilityRefs);
    
    public List<Ability> GetPassiveTraitAbilities() => new List<Ability>(passiveTraitAbilities);
    public List<AbilityReference> GetPassiveTraitAbilityRefs() => new List<AbilityReference>(passiveTraitAbilityRefs);
    
    /// <summary>Get all trait abilities (active + passive) for UI display.</summary>
    public List<Ability> GetAllTraitAbilities()
    {
        var all = new List<Ability>(activeTraitAbilities);
        all.AddRange(passiveTraitAbilities);
        return all;
    }
    
    public Ability GetActiveTraitAbility(int index)
    {
        if (index >= 0 && index < activeTraitAbilities.Count)
            return activeTraitAbilities[index];
        return null;
    }

    public Ability GetSecondaryWeaponAbility() => secondaryAbility;
    public AbilityReference GetSecondaryWeaponAbilityRef() => secondaryAbilityRef;

    /// <summary>
    /// Get ability by slot index:
    /// 0 = primary weapon (LMB), 1 = secondary weapon (RMB), 2-5 = Shift, Q, E, R.
    /// </summary>
    public DataDrivenAbility GetDataDrivenAbilityAtSlot(int slot)
    {
        return slot switch
        {
            0 => primaryAbility as DataDrivenAbility,
            1 => secondaryAbility as DataDrivenAbility,
            _ when slot >= 2 && slot - 2 < activeTraitAbilities.Count => activeTraitAbilities[slot - 2] as DataDrivenAbility,
            -1 when passiveTraitAbilities.Count == 1 => passiveTraitAbilities[0] as DataDrivenAbility,
            _ => null
        };
    }

    public DataDrivenAbility FindDataDrivenAbility(int slot, string abilityName = null)
    {
        DataDrivenAbility bySlot = GetDataDrivenAbilityAtSlot(slot);
        if (bySlot != null && (string.IsNullOrEmpty(abilityName) || string.Equals(bySlot.AbilityName, abilityName, StringComparison.OrdinalIgnoreCase)))
            return bySlot;

        if (!string.IsNullOrEmpty(abilityName))
        {
            // Covers combo step runners, which share the shell's slot and are not in the loadout lists.
            foreach (DataDrivenAbility candidate in GetComponents<DataDrivenAbility>())
            {
                if (string.Equals(candidate.AbilityName, abilityName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }

        return bySlot;
    }

    /// <summary>
    /// Resolves a triggered-only ability config for this character.
    /// If the character has <paramref name="config"/> in their triggeredAbilities loadout slot,
    /// that per-character reference is returned (so trait modifiers targeting it by SO reference apply).
    /// Falls back to the original <paramref name="config"/> if not found.
    /// </summary>
    public AbilityDataConfig ResolveTriggeredAbility(AbilityDataConfig config)
    {
        if (config == null) return null;
        CharacterData data = playerController?.GetCurrentCharacterData();
        if (data?.abilityLoadout == null) return config;
        AbilityDataConfig perCharacter = data.abilityLoadout.FindTriggeredAbility(config);
        return perCharacter != null ? perCharacter : config;
    }

    // ===========================
    // SETTERS
    // ===========================
    
    /// <summary>Set weapon ability (called when weapons are equipped).</summary>
    public void SetWeaponAbility(AbilityConfig abilityConfig)
    {
        if (primaryAbilityRef?.Config is AbilityDataConfig previousConfig && previousConfig.areaConfig?.isAura == true)
            GetComponent<PlayerAuraManager>().ClearAura(previousConfig);

        // Remove existing
        if (primaryAbility != null)
        {
            Destroy(primaryAbility);
            primaryAbility = null;
        }
        primaryAbilityRef = null;

        if (abilityConfig == null)
        {
            Debug.Log("[CharacterAbilityManager] Cleared weapon ability");
            OnPrimaryAbilityChanged?.Invoke(null, null);
            return;
        }

        primaryAbilityRef = new AbilityReference(abilityConfig);
        primaryAbility = LoadAbility(primaryAbilityRef, 0);
        
        OnPrimaryAbilityChanged?.Invoke(primaryAbilityRef, primaryAbility);
    }
    public void SetSecondaryAbility(AbilityConfig abilityConfig)
    {
        if (secondaryAbilityRef?.Config is AbilityDataConfig previousConfig && previousConfig.areaConfig?.isAura == true)
            GetComponent<PlayerAuraManager>().ClearAura(previousConfig);

        if (secondaryAbility != null)
        {
            Destroy(secondaryAbility);
            secondaryAbility = null;
        }
        secondaryAbilityRef = null;

        if (abilityConfig == null)
        {
            Debug.Log("[CharacterAbilityManager] Cleared secondary ability");
            OnSecondaryAbilityChanged?.Invoke(null, null);
            return;
        }

        secondaryAbilityRef = new AbilityReference(abilityConfig);
        secondaryAbility = LoadAbility(secondaryAbilityRef, 1);
        
        OnSecondaryAbilityChanged?.Invoke(secondaryAbilityRef, secondaryAbility);
    }
    public void SetDashAbility(AbilityConfig abilityConfig)
    {
        if (dashAbilityRef?.Config is AbilityDataConfig previousConfig && previousConfig.areaConfig?.isAura == true)
            GetComponent<PlayerAuraManager>().ClearAura(previousConfig);

        if (dashAbility != null)
        {
            Destroy(dashAbility);
            dashAbility = null;
        }
        dashAbilityRef = null;

        if (abilityConfig == null)
        {
            Debug.Log("[CharacterAbilityManager] Cleared dash ability");
            OnDashAbilityChanged?.Invoke(null, null);
            return;
        }

        dashAbilityRef = new AbilityReference(abilityConfig);
        dashAbility = LoadAbility(dashAbilityRef, 2);
        
        OnDashAbilityChanged?.Invoke(dashAbilityRef, dashAbility);
    }

    public void SetPassiveAbility(AbilityConfig abilityConfig)
    {
        if (passiveAbilityRef?.Config is AbilityDataConfig previousConfig && previousConfig.areaConfig?.isAura == true)
            GetComponent<PlayerAuraManager>().ClearAura(previousConfig);

        if (passiveAbility != null)
        {
            Destroy(passiveAbility);
            passiveAbility = null;
        }
        passiveAbilityRef = null;

        if (abilityConfig == null)
        {
            Debug.Log("[CharacterAbilityManager] Cleared passive ability");
            OnPassiveAbilityChanged?.Invoke(null, null);
            return;
        }

        passiveAbilityRef = new AbilityReference(abilityConfig);
        passiveAbility = LoadAbility(passiveAbilityRef, -1);
        
        OnPassiveAbilityChanged?.Invoke(passiveAbilityRef, passiveAbility);
    }


    /// <summary>Add a trait ability. Returns the slot index assigned.</summary>
    public int AddTraitAbility(AbilityConfig abilityConfig)
    {
        if (abilityConfig == null) return -1;

        var dataConfig = abilityConfig as AbilityDataConfig;
        if (dataConfig == null) return -1;

        // Guard: skip if this exact config is already registered
        bool alreadyRegistered = activeTraitAbilityRefs.Exists(r => r.Config == abilityConfig);
        if (alreadyRegistered)
        {
            Debug.Log($"[CharacterAbilityManager] Skipping duplicate trait ability: {abilityConfig.abilityName}");
            OnTraitAbilitiesChanged?.Invoke();
            return -1;
        }

        if (!dataConfig.RequiresKeybind)
        {
            var passiveRef = new AbilityReference(abilityConfig);
            var passiveAbility = LoadAbility(passiveRef, -1);
            if (passiveAbility == null)
                return -1;

            passiveTraitAbilities.Add(passiveAbility);
            passiveTraitAbilityRefs.Add(passiveRef);
            OnTraitAbilitiesChanged?.Invoke();
            return -1;
        }

        if (activeTraitAbilities.Count >= 4)
        {
            Debug.LogWarning($"[CharacterAbilityManager] Cannot add {abilityConfig.abilityName}; Shift, Q, E, and R are occupied.");
            return -1;
        }

        // Keybound abilities get the next available slot: Shift, Q, E, then R.
        int slotIndex = 2 + activeTraitAbilities.Count;
        var traitRef = new AbilityReference(abilityConfig);
        var ability = LoadAbility(traitRef, slotIndex);
        
        if (ability != null)
        {
            activeTraitAbilities.Add(ability);
            activeTraitAbilityRefs.Add(traitRef);
            Debug.Log($"[CharacterAbilityManager] Added active trait slot {slotIndex}: {abilityConfig.abilityName}");
            OnTraitAbilitiesChanged?.Invoke();
            return slotIndex;
        }

        return -1;
    }

    /// <summary>Remove a trait ability by config.</summary>
    public bool RemoveTraitAbility(AbilityConfig abilityConfig)
    {
        if (abilityConfig == null) return false;

        // Check if it's an aura
        var dataConfig = abilityConfig as AbilityDataConfig;
        if (dataConfig?.areaConfig?.isAura == true)
        {
            GetComponent<PlayerAuraManager>().ClearAura(dataConfig);
            OnTraitAbilitiesChanged?.Invoke();
            return true;
        }

        // Find and remove from active traits
        for (int i = 0; i < activeTraitAbilityRefs.Count; i++)
        {
            if (activeTraitAbilityRefs[i]?.Config == abilityConfig)
            {
                if (activeTraitAbilities[i] != null)
                    Destroy(activeTraitAbilities[i]);
                
                activeTraitAbilities.RemoveAt(i);
                activeTraitAbilityRefs.RemoveAt(i);

                // Reassign slot indices for remaining abilities
                ReassignTraitSlots();
                
                OnTraitAbilitiesChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    private void ReassignTraitSlots()
    {
        for (int i = 0; i < activeTraitAbilities.Count; i++)
        {
            if (activeTraitAbilities[i] is DataDrivenAbility dda)
            {
                dda.SetAbilitySlot(2 + i); // Slots 2, 3, 4...
            }
        }
    }

    /// <summary>
    /// Add an ability to the next available trait slot.
    /// </summary>
    public void AddAbility(AbilityConfig abilityConfig)
    {
        if (abilityConfig == null) return;

        AddTraitAbility(abilityConfig);
    }

    // ===========================
    // ABILITY REPLACEMENT (traits)
    // ===========================

    /// <summary>
    /// Returns a reference to the trait-granted replacement for <paramref name="baseRef"/>,
    /// or the original reference if no active trait replaces it.
    /// </summary>
    private AbilityReference ResolveReplacement(AbilityReference baseRef)
    {
        if (baseRef?.Config == null) return baseRef;

        CharacterTraitManager traitManager = GetComponent<CharacterTraitManager>();
        if (traitManager == null) return baseRef;

        AbilityConfig replacement = traitManager.GetAbilityReplacement(baseRef.Config);
        if (replacement != null && replacement != baseRef.Config)
            return new AbilityReference(replacement);

        return baseRef;
    }

    /// <summary>
    /// Re-resolves every equipped ability against the current set of trait ability replacements,
    /// reloading only the slots whose effective ability changed. Called when traits change so a
    /// newly granted (or removed) ability-replacement trait swaps the live ability immediately.
    /// </summary>
    public void ReapplyAbilityReplacements()
    {
        ReapplyCoreSlot(ref primaryAbility, primaryAbilityRef, 0, (r, a) => OnPrimaryAbilityChanged?.Invoke(r, a));
        ReapplyCoreSlot(ref secondaryAbility, secondaryAbilityRef, 1, (r, a) => OnSecondaryAbilityChanged?.Invoke(r, a));
        ReapplyCoreSlot(ref dashAbility, dashAbilityRef, 2, (r, a) => OnDashAbilityChanged?.Invoke(r, a));
        ReapplyCoreSlot(ref passiveAbility, passiveAbilityRef, -1, (r, a) => OnPassiveAbilityChanged?.Invoke(r, a));

        bool traitAbilitiesChanged = false;

        for (int i = 0; i < activeTraitAbilities.Count; i++)
        {
            if (ReapplyListSlot(activeTraitAbilities, activeTraitAbilityRefs, i, 2 + i))
                traitAbilitiesChanged = true;
        }

        for (int i = 0; i < passiveTraitAbilities.Count; i++)
        {
            if (ReapplyListSlot(passiveTraitAbilities, passiveTraitAbilityRefs, i, -1))
                traitAbilitiesChanged = true;
        }

        if (traitAbilitiesChanged)
            OnTraitAbilitiesChanged?.Invoke();
    }

    private void ReapplyCoreSlot(ref Ability current, AbilityReference baseRef, int slotIndex, Action<AbilityReference, Ability> notify)
    {
        if (baseRef?.Config == null) return;

        AbilityReference resolvedRef = ResolveReplacement(baseRef);
        if (current != null && current.GetAbilityConfig() == resolvedRef.Config)
            return;

        if (current != null)
            Destroy(current);

        current = LoadAbility(baseRef, slotIndex);
        notify?.Invoke(resolvedRef, current);
    }

    private bool ReapplyListSlot(List<Ability> abilities, List<AbilityReference> baseRefs, int index, int slotIndex)
    {
        AbilityReference baseRef = baseRefs[index];
        if (baseRef?.Config == null) return false;

        AbilityReference resolvedRef = ResolveReplacement(baseRef);
        Ability current = abilities[index];
        if (current != null && current.GetAbilityConfig() == resolvedRef.Config)
            return false;

        if (current != null)
            Destroy(current);

        abilities[index] = LoadAbility(baseRef, slotIndex);
        return true;
    }
}