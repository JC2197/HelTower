using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages the player's ability loadout:
/// - Weapon ability (LMB) - granted by equipped weapon
/// - Dash ability (Shift) - movement ability with isDash=true
/// - Active trait abilities - Q, E, 1-7 (Ability1-Ability9)
/// - Passive trait abilities - autocast, no keybind
/// - Aura abilities - managed by PlayerAuraManager
/// </summary>
public class CharacterAbilityManager : MonoBehaviour
{
    // === Events ===
    /// <summary>Fired when weapon ability changes (weapon swap).</summary>
    public event Action<AbilityReference, Ability> OnWeaponAbilityChanged;
    
    /// <summary>Fired when dash ability changes.</summary>
    public event Action<AbilityReference, Ability> OnDashAbilityChanged;
    
    /// <summary>Fired when trait abilities list changes (add/remove/clear).</summary>
    public event Action OnTraitAbilitiesChanged;
    
    /// <summary>Fired when offhand ability is set or cleared.</summary>
    public event Action<AbilityReference, Ability> OnOffhandAbilityChanged;
    
    /// <summary>Fired when CTRL toggles between weapon and offhand ability.</summary>
    public event Action<bool> OnOffhandToggled;

    // === Core Abilities ===
    private Ability weaponAbility;           // Slot 0 = LMB
    private AbilityReference weaponAbilityRef;
    
    private Ability dashAbility;             // Slot 1 = Space
    private AbilityReference dashAbilityRef;
    
    // Active trait abilities get dynamic keybinds (slots 2, 3, 4... = Q, E, 1, 2, 3...)
    private readonly List<Ability> activeTraitAbilities = new List<Ability>();
    private readonly List<AbilityReference> activeTraitAbilityRefs = new List<AbilityReference>();
    
    // Passive trait abilities (autocast - no keybind, fire automatically)
    private readonly List<Ability> passiveTraitAbilities = new List<Ability>();
    private readonly List<AbilityReference> passiveTraitAbilityRefs = new List<AbilityReference>();
    
    // === Offhand (for dual-wielding) ===
    private Ability offhandAbility;
    private AbilityReference offhandAbilityRef;
    private bool isOffhandActive = false;
    private bool shouldAlternate = false;
    private bool lastAttackWasMainhand = true;

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
            weaponAbilityRef = loadout.WeaponAbility;
            weaponAbility = LoadAbility(weaponAbilityRef, 0);
        }

        // Load Dash Ability (Slot 1 = Space)
        if (loadout.DashAbility?.Config != null)
        {
            dashAbilityRef = loadout.DashAbility;
            dashAbility = LoadAbility(dashAbilityRef, 1);
        }

        // Load Trait Abilities
        int activeSlot = 2; // Start at slot 2 for active traits (keys 1, 2, 3...)
        foreach (var traitRef in loadout.TraitAbilities)
        {
            if (traitRef?.Config == null) continue;
            
            var dataConfig = traitRef.Config as AbilityDataConfig;
            if (dataConfig == null) continue;
            
            // Auras also register with PlayerAuraManager for runtime behavior
            if (dataConfig.isAuraAbility)
            {
                GetComponent<PlayerAuraManager>().AddAura(dataConfig);
            }
            
            // All trait abilities get sequential slots
            var ability = LoadAbility(traitRef, activeSlot);
            if (ability != null)
            {
                activeTraitAbilities.Add(ability);
                activeTraitAbilityRefs.Add(traitRef);
                Debug.Log($"[CharacterAbilityManager] Loaded trait slot {activeSlot}: {traitRef.AbilityName}");
                activeSlot++;
            }
        }

        Debug.Log($"[CharacterAbilityManager] Loaded abilities:");
        Debug.Log($"  Weapon: {weaponAbility?.AbilityName ?? "None"}");
        Debug.Log($"  Dash: {dashAbility?.AbilityName ?? "None"}");
        Debug.Log($"  Active Traits: {activeTraitAbilities.Count}");
    }

    private Ability LoadAbility(AbilityReference abilityRef, int slotIndex)
    {
        Debug.Log($"[CharacterAbilityManager] LoadAbility called: slot={slotIndex}, abilityRef={abilityRef?.AbilityName ?? "NULL"}");
        
        if (abilityRef?.Config == null)
        {
            Debug.LogWarning("[CharacterAbilityManager] AbilityReference or Config is null");
            return null;
        }

        // Add DataDrivenAbility component
        var newAbility = gameObject.AddComponent<DataDrivenAbility>();
        if (newAbility == null)
        {
            Debug.LogError("[CharacterAbilityManager] Failed to add DataDrivenAbility component");
            return null;
        }

        newAbility.SetAbilityReference(abilityRef);
        newAbility.SetAbilitySlot(slotIndex);
        
        string slotName = slotIndex switch
        {
            0 => "Weapon (LMB)",
            1 => "Dash (Shift)",
            -1 => "Passive/Autocast",
            2 => "Ability1 (Q)",
            3 => "Ability2 (E)",
            _ => $"Ability{slotIndex - 1} ({slotIndex - 3})"  // Slot 4 = Ability3 (1), etc.
        };
        
        var dataConfig = abilityRef.Config as AbilityDataConfig;
        Debug.Log($"[CharacterAbilityManager] ✓ Loaded {abilityRef.AbilityName} -> {slotName}");
        Debug.Log($"[CharacterAbilityManager]   Config type: {abilityRef.Config.GetType().Name}");
        Debug.Log($"[CharacterAbilityManager]   isAuraAbility={dataConfig?.isAuraAbility}, autocast={dataConfig?.autocast}, isDash={dataConfig?.isDash}");

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

        weaponAbility = null;
        weaponAbilityRef = null;
        dashAbility = null;
        dashAbilityRef = null;
        activeTraitAbilities.Clear();
        activeTraitAbilityRefs.Clear();
        passiveTraitAbilities.Clear();
        passiveTraitAbilityRefs.Clear();
        
        // Clear offhand
        offhandAbility = null;
        offhandAbilityRef = null;
        isOffhandActive = false;
        shouldAlternate = false;
        lastAttackWasMainhand = true;
    }

    // ===========================
    // GETTERS
    // ===========================
    
    public Ability GetWeaponAbility() => weaponAbility;
    public AbilityReference GetWeaponAbilityRef() => weaponAbilityRef;
    
    public Ability GetDashAbility() => dashAbility;
    public AbilityReference GetDashAbilityRef() => dashAbilityRef;
    
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
    
    public Ability GetOffhandAbility() => offhandAbility;
    public AbilityReference GetOffhandAbilityRef() => offhandAbilityRef;
    public bool IsOffhandActive => isOffhandActive;
    public bool ShouldAlternate => shouldAlternate;

    /// <summary>
    /// Get ability by slot index:
    /// 0 = Weapon (LMB), 1 = Dash (Shift), 2 = Ability1 (Q), 3 = Ability2 (E), 4+ = Ability3-9 (1-7)
    /// </summary>
    public DataDrivenAbility GetDataDrivenAbilityAtSlot(int slot)
    {
        return slot switch
        {
            0 => weaponAbility as DataDrivenAbility,
            1 => dashAbility as DataDrivenAbility,
            _ when slot >= 2 && slot - 2 < activeTraitAbilities.Count => activeTraitAbilities[slot - 2] as DataDrivenAbility,
            -1 when passiveTraitAbilities.Count == 1 => passiveTraitAbilities[0] as DataDrivenAbility,
            _ => null
        };
    }

    public DataDrivenAbility FindDataDrivenAbility(int slot, string abilityName = null)
    {
        DataDrivenAbility bySlot = GetDataDrivenAbilityAtSlot(slot);
        if (bySlot != null)
            return bySlot;

        if (!string.IsNullOrEmpty(abilityName))
        {
            if (weaponAbility is DataDrivenAbility weaponDda && string.Equals(weaponDda.AbilityName, abilityName, StringComparison.OrdinalIgnoreCase))
                return weaponDda;

            if (dashAbility is DataDrivenAbility dashDda && string.Equals(dashDda.AbilityName, abilityName, StringComparison.OrdinalIgnoreCase))
                return dashDda;

            foreach (Ability ability in activeTraitAbilities)
            {
                if (ability is DataDrivenAbility dda && string.Equals(dda.AbilityName, abilityName, StringComparison.OrdinalIgnoreCase))
                    return dda;
            }

            foreach (Ability ability in passiveTraitAbilities)
            {
                if (ability is DataDrivenAbility dda && string.Equals(dda.AbilityName, abilityName, StringComparison.OrdinalIgnoreCase))
                    return dda;
            }
        }

        return null;
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
        // Remove existing
        if (weaponAbility != null)
        {
            Destroy(weaponAbility);
            weaponAbility = null;
        }
        weaponAbilityRef = null;

        if (abilityConfig == null)
        {
            Debug.Log("[CharacterAbilityManager] Cleared weapon ability");
            OnWeaponAbilityChanged?.Invoke(null, null);
            return;
        }

        weaponAbilityRef = new AbilityReference(abilityConfig);
        weaponAbility = LoadAbility(weaponAbilityRef, 0);
        
        Debug.Log($"[CharacterAbilityManager] Set weapon ability: {weaponAbility?.AbilityName ?? "None"}");
        OnWeaponAbilityChanged?.Invoke(weaponAbilityRef, weaponAbility);
    }

    /// <summary>Set dash ability.</summary>
    public void SetDashAbility(AbilityConfig abilityConfig)
    {
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
        dashAbility = LoadAbility(dashAbilityRef, 1);
        
        Debug.Log($"[CharacterAbilityManager] Set dash ability: {dashAbility?.AbilityName ?? "None"}");
        OnDashAbilityChanged?.Invoke(dashAbilityRef, dashAbility);
    }

    /// <summary>Add a trait ability. Returns the slot index assigned.</summary>
    public int AddTraitAbility(AbilityConfig abilityConfig)
    {
        if (abilityConfig == null) return -1;

        var dataConfig = abilityConfig as AbilityDataConfig;
        if (dataConfig == null) return -1;

        // Auras also register with PlayerAuraManager for runtime behavior
        if (dataConfig.isAuraAbility)
        {
            GetComponent<PlayerAuraManager>().AddAura(dataConfig);
        }

        // Guard: skip if this exact config is already registered
        bool alreadyRegistered = activeTraitAbilityRefs.Exists(r => r.Config == abilityConfig);
        if (alreadyRegistered)
        {
            Debug.Log($"[CharacterAbilityManager] Skipping duplicate trait ability: {abilityConfig.abilityName}");
            OnTraitAbilitiesChanged?.Invoke();
            return -1;
        }

        // All trait abilities get next available slot
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
        if (dataConfig != null && dataConfig.isAuraAbility)
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

    // ===========================
    // OFFHAND (DUAL-WIELD)
    // ===========================
    
    /// <summary>
    /// Sets the offhand ability. When <paramref name="mainWeaponConfig"/> and
    /// <paramref name="offhandWeaponConfig"/> are provided and share the same weaponType
    /// (e.g. Pistol + Pistol, Dagger + Dagger), alternating mode is enabled and the shared
    /// weaponAbility instance is used for both hands (DataDrivenAbility handles alternating
    /// the animation/ammo cost between hands internally). Weapon TYPE is the source of truth
    /// here — not ability reference equality — so two separately-tuned weapons of the same
    /// type still alternate correctly even if they grant distinct AbilityConfig assets.
    /// </summary>
    public void SetOffhandAbility(AbilityConfig abilityConfig, WeaponConfig mainWeaponConfig = null, WeaponConfig offhandWeaponConfig = null)
    {
        if (offhandAbility != null)
        {
            Destroy(offhandAbility);
            offhandAbility = null;
        }
        offhandAbilityRef = null;
        shouldAlternate = false;
        isOffhandActive = false;
        
        if (abilityConfig == null)
        {
            Debug.Log("[CharacterAbilityManager] Cleared offhand ability");
            OnOffhandAbilityChanged?.Invoke(null, null);
            return;
        }
        
        // Alternate whenever both hands wield the same weapon type.
        bool sameWeaponType = mainWeaponConfig != null && offhandWeaponConfig != null
            && !string.IsNullOrEmpty(mainWeaponConfig.weaponType)
            && string.Equals(mainWeaponConfig.weaponType, offhandWeaponConfig.weaponType, StringComparison.OrdinalIgnoreCase);

        if (sameWeaponType)
        {
            shouldAlternate = true;
            lastAttackWasMainhand = true;
            OnOffhandAbilityChanged?.Invoke(null, null);
            return;
        }
        
        // Different weapon type — load into offhand
        offhandAbilityRef = new AbilityReference(abilityConfig);
        offhandAbility = LoadAbility(offhandAbilityRef, 10); // Use high slot for offhand
        OnOffhandAbilityChanged?.Invoke(offhandAbilityRef, offhandAbility);
    }
    
    public void ClearOffhandAbility() => SetOffhandAbility(null);
    
    public void SetOffhandToggle(bool active)
    {
        if (offhandAbility == null || shouldAlternate) return;
        
        isOffhandActive = active;
        OnOffhandToggled?.Invoke(isOffhandActive);
    }
    
    public Ability GetActiveWeaponAbility()
    {
        if (shouldAlternate)
            return weaponAbility;
        
        return isOffhandActive && offhandAbility != null ? offhandAbility : weaponAbility;
    }

    // ===========================
    // LEGACY COMPATIBILITY
    // ===========================
    // These methods maintain compatibility with old code during transition
    
    [Obsolete("Use GetWeaponAbility() instead")]
    public Ability GetPrimaryAbility() => weaponAbility;
    
    [Obsolete("Use GetDashAbility() instead")]
    public Ability GetTertiaryAbility() => dashAbility;
    
    [Obsolete("Use SetWeaponAbility() instead")]
    public void SetPrimaryAbility(AbilityConfig config) => SetWeaponAbility(config);
    
    [Obsolete("Use SetDashAbility() instead")]  
    public void SetTertiaryAbility(AbilityConfig config) => SetDashAbility(config);
    
    // Legacy events - redirect to new events
    public event Action<AbilityReference, Ability> OnPrimaryAbilityChanged
    {
        add => OnWeaponAbilityChanged += value;
        remove => OnWeaponAbilityChanged -= value;
    }
    
    public event Action<AbilityReference, Ability> OnTertiaryAbilityChanged
    {
        add => OnDashAbilityChanged += value;
        remove => OnDashAbilityChanged -= value;
    }

    /// <summary>
    /// Add an ability and auto-route to the correct slot based on ability type.
    /// - Dash abilities (isDash=true) → Dash slot (Shift key)
    /// - Aura abilities → PlayerAuraManager (passive background)
    /// - All others → Trait ability list (Ability1-Ability9)
    /// This is the preferred method for trait-unlocked abilities.
    /// </summary>
    public void AddAbility(AbilityConfig abilityConfig)
    {
        if (abilityConfig == null) return;

        var dataConfig = abilityConfig as AbilityDataConfig;
        if (dataConfig == null)
        {
            // Fallback for non-data configs
            AddTraitAbility(abilityConfig);
            return;
        }

        // Route based on ability type flags
        if (dataConfig.isDash)
        {
            SetDashAbility(abilityConfig);
            Debug.Log($"[CharacterAbilityManager] Auto-routed {abilityConfig.abilityName} to Dash slot (isDash=true)");
        }
        else
        {
            // Auras are handled inside AddTraitAbility, everything else goes to trait slots
            AddTraitAbility(abilityConfig);
        }
    }
}