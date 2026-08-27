using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Component.Animating;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : Organism
{
    [Header("Movement")]
    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private Transform _visualTransform;

    [SerializeField, Min(0f)] private float _facingDirectionThreshold = 0.01f;

    [Header("Animation")]
    [SerializeField] private Animator _bodyAnimator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [SerializeField] private string _movingParameter = "IsMoving";

    [Header("Abilities")]
    [SerializeField] private DataDrivenAbility[] _abilities = new DataDrivenAbility[6];
    [SerializeField] private InputActionAsset _inputAsset;
    [SerializeField] private string _playerMapName = "Player";
    [SerializeField] private string _moveActionName = "Movement";
    [SerializeField] private string _aimActionName = "Aim";
    [SerializeField] private string[] _abilityActionNames = { "Ability1", "Ability2", "Ability3", "Ability4", "Ability5", "Ability6" };

    public static bool InputEnabled { get; set; } = true;
    public static PlayerController LocalPlayer { get; private set; }

    private CharacterData _currentCharacterData;
    private SaveFileData _currentSaveFileData;
    private NetworkObject _currentMainWeaponNob;
    private WeaponConfig _equippedMainWeaponConfig;
    private WeaponSettings _currentMainWeaponSettings;
    private readonly List<AccessorySettings> _equippedAccessorySettings = new List<AccessorySettings>();
    private WeaponSortingManager _weaponSortingManager;
    private SpriteRenderer _characterSpriteRenderer;
    private bool _isFacingLeft;
    private string _assignedCharacterName;
    private bool _hasFacing;
    /// <summary>Fired on the local owner when the player spawns / gains ownership.</summary>
    public static event Action<PlayerController> OnPlayerSpawned;

    /// <summary>Fired when an attack ability executes.</summary>
    public event Action<AbilityDataConfig> OnAttack;
    public event Action<AbilityDataConfig, GameObject, float, string> OnAttackDamage;
    public Coroutine WeaponIdleReturnCoroutine { get; set; }
    /// <summary>Fired when a stats recalculation is requested (e.g. after trait changes).</summary>
    public event Action OnStatsRecalculationRequested;

    public CharacterData GetCurrentCharacterData() => _currentCharacterData;
    public SaveFileData GetCurrentSaveFileData() => _currentSaveFileData;
    public void SetCurrentCharacterData(CharacterData characterData) => _currentCharacterData = characterData;

    /// <summary>
    /// Assign the meta progression save file for this player and push it to the trait manager
    /// so persisted trait tree nodes are restored.
    /// </summary>
    public void SetCurrentSaveFileData(SaveFileData saveFileData)
    {
        _currentSaveFileData = saveFileData;
        GetComponent<CharacterTraitManager>()?.SetSaveFileData(saveFileData);
    }
    public WeaponConfig GetEquippedMainWeaponConfig() => _equippedMainWeaponConfig;
    public enum AbilityState
    {
        Idle,
        Precast,
        Holding,
        Executing,
    }
    public AbilityState CurrentAbilityState { get; set; } = AbilityState.Idle;
    public Transform GetEquippedMainWeaponTransform()
    {
        WeaponHolder weaponHolder = GetExistingMainWeaponHolder();
        GameObject weapon = weaponHolder != null ? weaponHolder.GetCurrentWeapon() : null;
        return weapon != null ? weapon.transform : null;
    }

    public WeaponConfig GetEquippedOffhandWeaponConfig()
    {
        CharacterData characterData = _currentCharacterData;
        if (characterData != null && characterData.hasDualWeapons && characterData.offHandWeaponConfig != null)
            return characterData.offHandWeaponConfig;

        return _equippedMainWeaponConfig != null ? _equippedMainWeaponConfig.offhandWeaponConfig : null;
    }

    public bool ApplyClassAnimator(ClassData classData)
    {
        if (classData == null)
        {
            Debug.LogWarning("[PlayerController] ApplyClassAnimator called with null class data.");
            return false;
        }

        if (!ApplyClassAnimatorVisual(classData))
            return false;

        ApplyClassBaseStats(classData);

        WeaponConfig defaultWeapon = GetRandomWeaponForClass(classData);
        if (defaultWeapon != null)
            EquipMainHandWeapon(defaultWeapon);

        SynchronizeClassVisual(classData.className);

        Debug.Log($"[PlayerController] Switched animator to class '{classData.className}'.");
        return true;
    }

    /// <summary>
    /// Reseeds the character's base and current stats from the class template, reapplies stat
    /// conversions, and pushes the result onto the live Organism container.
    /// </summary>
    private void ApplyClassBaseStats(ClassData classData)
    {
        if (classData == null || classData.baseStatContainer == null)
        {
            Debug.LogWarning($"[PlayerController] Class '{classData?.className}' has no base stats to apply.");
            return;
        }

        StatContainer source = classData.baseStatContainer;

        if (_currentCharacterData != null)
        {
            _currentCharacterData.classData = classData;
            _currentCharacterData.baseStatContainer = classData.baseStatContainer.Clone();
            _currentCharacterData.statContainer = _currentCharacterData.baseStatContainer.Clone();
            CharacterStatConverter.ApplyConversions(_currentCharacterData);
            source = _currentCharacterData.statContainer;
        }

        StatContainer target = AllStats;
        if (target != null)
        {
            source.CopyToStatContainer(target);
            RefreshMoveSpeedFromStats();
        }

        if (MaxHealth > 0f)
            ModifyHealth(MaxHealth - CurrentHealth);

        if (MaxEnergy > 0f)
            ModifyEnergy(MaxEnergy - CurrentEnergy);

        // Traits reapply their bonuses on top of the freshly seeded base.
        RequestStatsRecalculation();

        Debug.Log($"[PlayerController] Applied base stats from class '{classData.className}'.");
    }

    /// <summary>
    /// Applies a CharacterData to this live player: stats, health/energy, body animator,
    /// weapon, and ability loadout. Used for runtime class switching from the pause menu.
    /// </summary>
    public void ApplyCharacterData(CharacterData characterData)
    {
        if (characterData == null)
        {
            Debug.LogWarning("[PlayerController] ApplyCharacterData called with null data.");
            return;
        }

        _currentCharacterData = characterData;

        // Stats -> runtime Organism container, then refresh derived values.
        StatContainer target = AllStats;
        if (target != null && characterData.statContainer != null)
        {
            characterData.statContainer.CopyToStatContainer(target);
            RefreshMoveSpeedFromStats();
        }

        // Refill only when the class has a valid configured maximum. A missing/zero
        // MaxHealth must not turn the spawn refill into lethal damage.
        if (MaxHealth > 0f)
            ModifyHealth(MaxHealth - CurrentHealth);

        if (MaxEnergy > 0f)
            ModifyEnergy(MaxEnergy - CurrentEnergy);

        // Body animator (class visual).
        Animator bodyAnimator = ResolveBodyAnimator();
        RuntimeAnimatorController controller = characterData.GetAnimatorController();
        if (bodyAnimator != null && controller != null)
            bodyAnimator.runtimeAnimatorController = controller;

        if (characterData.classData != null)
            SynchronizeClassVisual(characterData.classData.className);

        // Main-hand weapon.
        if (characterData.mainHandWeaponConfig != null)
            EquipMainHandWeapon(characterData.mainHandWeaponConfig);
        else
            UnequipMainHandWeapon();

        // Accessories (any number, all under the single AccessoryHolder).
        EquipAccessories(characterData.accessoryConfigs);

        // Ability loadout.
        GetComponent<CharacterAbilityManager>()?.LoadCharacterAbilities(characterData);

        // Notify observers so dependent systems refresh.
        GetComponent<CharacterDataObserver>()?.SetCharacterData(characterData);

        CharacterTraitManager traitManager = GetComponent<CharacterTraitManager>();
        if (traitManager != null)
        {
            traitManager.SetCharacterData(characterData);
            traitManager.SetSaveFileData(_currentSaveFileData);
        }

        Debug.Log($"[PlayerController] Applied character '{characterData.displayName}'.");

    }

    public void RequestStatsRecalculation()
    {
        OnStatsRecalculationRequested?.Invoke();
    }

    public void NotifyAttack(AbilityDataConfig abilityConfig)
    {
        if (abilityConfig == null || !abilityConfig.isAttack)
            return;

        OnAttack?.Invoke(abilityConfig);
    }

    public void NotifyAttackDamage(AbilityDataConfig abilityConfig, GameObject target, float damage, string damageType)
    {
        if (abilityConfig == null || !abilityConfig.isAttack)
            return;

        if (damage <= 0f)
            return;

        OnAttackDamage?.Invoke(abilityConfig, target, damage, damageType);
    }
    public void SetAssignedCharacterName(string characterName) => _assignedCharacterName = characterName;

    /// <summary>Character name for display (falls back to the assigned name).</summary>
    public string GetSyncedCharacterName()
    {
        if (_currentCharacterData != null && !string.IsNullOrEmpty(_currentCharacterData.characterName))
            return _currentCharacterData.characterName;
        return _assignedCharacterName;
    }

    /// <summary>Character's unique save/persistence name.</summary>
    public string GetCharacterSaveName()
    {
        if (_currentCharacterData != null && !string.IsNullOrEmpty(_currentCharacterData.characterName))
            return _currentCharacterData.characterName;

        if (!string.IsNullOrEmpty(_assignedCharacterName))
            return _assignedCharacterName;

        if (CharacterSelectionManager.SelectedCharacter != null &&
            !string.IsNullOrEmpty(CharacterSelectionManager.SelectedCharacter.characterName))
            return CharacterSelectionManager.SelectedCharacter.characterName;

        return null;
    }

    // Animation system not yet ported to the new controller; kept as a hook for callers.
    public void ForceAnimationUpdate() { }

    /// <summary>
    /// ServerRpc proxy for DataDrivenAbility projectile spawning. DataDrivenAbility is added via
    /// AddComponent at runtime, so its own [ServerRpc] is a no-op; routing through the registered
    /// PlayerController ensures the RPC reaches the server.
    /// </summary>
    [ServerRpc]
    public void ServerRpcSpawnAbilityProjectile(int abilitySlot, string abilityName, Vector3 spawnPos, Vector3 direction, float damageMultiplier, uint tick, bool firedFromOffhand = false)
    {
        CharacterAbilityManager mgr = GetComponent<CharacterAbilityManager>();
        if (mgr == null)
        {
            Debug.LogError($"[NET] ServerRpcSpawnAbilityProjectile FAILED - no CharacterAbilityManager on {gameObject.name}");
            return;
        }

        DataDrivenAbility ability = mgr.FindDataDrivenAbility(abilitySlot, abilityName);
        if (ability == null)
        {
            Debug.LogWarning($"[NET] ServerRpcSpawnAbilityProjectile: no DataDrivenAbility at slot {abilitySlot} / ability '{abilityName}' on {gameObject.name}");
            return;
        }

        ability.ExecuteServerSpawn(spawnPos, direction, damageMultiplier, tick, firedFromOffhand);
    }

    [ServerRpc]
    public void ServerRpcExecuteMeleeAbility(int abilitySlot, string abilityName, Vector2 direction, bool firedFromOffhand)
    {
        CharacterAbilityManager manager = GetComponent<CharacterAbilityManager>();
        DataDrivenAbility ability = manager != null ? manager.FindDataDrivenAbility(abilitySlot, abilityName) : null;
        if (ability == null)
        {
            Debug.LogWarning($"[NET] ServerRpcExecuteMeleeAbility: no ability at slot {abilitySlot} / '{abilityName}' on {gameObject.name}");
            return;
        }

        ability.ExecuteServerMelee(direction, firedFromOffhand);
    }

    /// <summary>
    /// Tells all non-server clients to spawn a muzzle flash for this player.
    /// The owner already spawned theirs immediately, so it is skipped here.
    /// </summary>
    [ObserversRpc]
    public void ObserversRpcSpawnMuzzleFlash(int abilitySlot, string abilityName, Vector3 position, float angle, bool firedFromOffhand)
    {
        // Host already renders authoritative muzzle flash on the server path.
        // Skipping the client-RPC echo prevents duplicate flashes in host mode.
        if (InstanceFinder.IsServerStarted)
            return;

        if (IsOwner) return;

        Debug.Log($"[MuzzleFlashTrace][RPC-Recv] player={gameObject.name}, slot={abilitySlot}, ability={abilityName}, offhand={firedFromOffhand}, pos={position}, angle={angle:F1}");

        CharacterAbilityManager mgr = GetComponent<CharacterAbilityManager>();
        if (mgr == null)
        {
            Debug.LogWarning($"[MuzzleFlashTrace][RPC-Recv] Missing CharacterAbilityManager on {gameObject.name}");
            return;
        }

        DataDrivenAbility ability = mgr.FindDataDrivenAbility(abilitySlot, abilityName);
        if (ability == null)
        {
            Debug.LogWarning($"[MuzzleFlashTrace][RPC-Recv] No DataDrivenAbility found. player={gameObject.name}, slot={abilitySlot}, ability={abilityName}");
            return;
        }

        ability?.SpawnMuzzleFlashLocally(position, angle, firedFromOffhand);
    }

    private InputActionMap _playerMap;
    private InputAction _moveAction;
    private InputAction _aimAction;
    private InputAction[] _abilityActions = new InputAction[6];
    private Vector2 _moveInput;
    private Vector2 _aimInput;

    /// <summary>Current owner movement input (raw, unnormalized).</summary>
    public Vector2 GetMovementInput() => _moveInput;

    /// <summary>
    /// ServerRpc proxy for ChannelAbility spawning. ChannelAbility is a plain MonoBehaviour, so it
    /// cannot carry its own RPCs; the owner client routes through the registered PlayerController.
    /// </summary>
    [ServerRpc]
    public void ServerRpcSpawnChannelObject(int abilitySlot, Vector3 position, Quaternion rotation)
    {
        ChannelAbility channel = FindChannelAbility(abilitySlot);
        if (channel == null)
        {
            Debug.LogWarning($"[NET] ServerRpcSpawnChannelObject: no ChannelAbility at slot {abilitySlot} on {gameObject.name}");
            return;
        }

        NetworkObject netObj = channel.ServerSpawnChannelObject(position, rotation);
        if (netObj != null)
            TargetRpcReceiveChannelObject(Owner, abilitySlot, netObj);
    }

    /// <summary>
    /// Sends the server-spawned channel object back to the owner client so it can wire up references.
    /// </summary>
    [TargetRpc]
    public void TargetRpcReceiveChannelObject(NetworkConnection conn, int abilitySlot, NetworkObject netObj)
    {
        if (netObj == null)
            return;

        ChannelAbility channel = FindChannelAbility(abilitySlot);
        channel?.SetChannelObjectReferences(netObj.gameObject);
    }

    private ChannelAbility FindChannelAbility(int abilitySlot)
    {
        ChannelAbility[] channels = GetComponents<ChannelAbility>();
        for (int i = 0; i < channels.Length; i++)
        {
            if (channels[i].AbilitySlot == abilitySlot)
                return channels[i];
        }
        return channels.Length > 0 ? channels[0] : null;
    }

    public static PlayerController GetLocalPlayer()
    {
        if (LocalPlayer != null)
            return LocalPlayer;

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerController player = players[i];
            if (player != null && player.IsOwner)
            {
                LocalPlayer = player;
                return LocalPlayer;
            }
        }

        if (players.Length == 1)
        {
            LocalPlayer = players[0];
            return LocalPlayer;
        }

        return null;
    }

    protected override void Awake()
    {
        base.Awake();

        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody2D>();

        if (_visualTransform == null)
            _visualTransform = transform;

        ResolveBodyAnimator();

        if (_abilities == null || _abilities.Length == 0)
        {
            DataDrivenAbility[] foundAbilities = GetComponents<DataDrivenAbility>();
            _abilities = new DataDrivenAbility[Mathf.Min(5, foundAbilities != null ? foundAbilities.Length : 0)];
            if (foundAbilities != null)
            {
                for (int i = 0; i < _abilities.Length; i++)
                    _abilities[i] = foundAbilities[i];
            }
        }

        if (_abilities == null)
            _abilities = new DataDrivenAbility[5];

        EnsureWeaponSortingManager();

        InitializeInputActions();
    }

    public override void OnStartClient()
    {
        if (IsOwner)
        {
            LocalPlayer = this;
            EnsureCharacterAssigned();
            OnPlayerSpawned?.Invoke(this);
        }
    }

    public override void OnOwnershipClient(NetworkConnection prevOwner)
    {
        if (IsOwner)
        {
            LocalPlayer = this;
            EnsureCharacterAssigned();
            OnPlayerSpawned?.Invoke(this);
        }
        else if (LocalPlayer == this)
            LocalPlayer = null;
    }

    private void EnsureCharacterAssigned()
    {
        // The save file is chosen in the main menu, before this player exists.
        if (_currentSaveFileData == null && SaveFileSelectionManager.ActiveSaveFile != null)
            SetCurrentSaveFileData(SaveFileSelectionManager.ActiveSaveFile);

        if (_currentCharacterData != null)
            return;

        CharacterData selected = CharacterSelectionManager.GetSelectedCharacter();
        if (selected != null)
        {
            ApplyCharacterData(selected);
            return;
        }

        CharacterSelectionConfig selectionConfig = Resources.Load<CharacterSelectionConfig>("CharacterSelectionConfig");
        if (selectionConfig == null)
        {
            Debug.LogWarning("[PlayerController] CharacterSelectionConfig not found in Resources; cannot assign default class.");
            return;
        }

        ClassData randomClass = selectionConfig.GetRandomClass();

        CharacterData runtimeCharacter = selectionConfig.CreateCharacterFromClass(randomClass);
        if (runtimeCharacter == null)
            return;

        CharacterSelectionManager.Instance?.SelectCharacter(runtimeCharacter);
        ApplyCharacterData(runtimeCharacter);
    }

    private WeaponConfig GetRandomWeaponForClass(ClassData classData)
    {
        if (classData == null || classData.availableWeapons == null || classData.availableWeapons.Length == 0)
            return null;

        return classData.availableWeapons[UnityEngine.Random.Range(0, classData.availableWeapons.Length)];
    }

    private bool ApplyClassAnimatorVisual(ClassData classData)
    {
        Animator bodyAnimator = ResolveBodyAnimator();
        if (bodyAnimator == null)
        {
            Debug.LogWarning("[PlayerController] Cannot switch class animator because no Animator is attached.");
            return false;
        }

        if (classData.animatorController == null)
        {
            Debug.LogWarning($"[PlayerController] Class '{classData.className}' has no animator controller assigned.");
            return false;
        }

        bodyAnimator.runtimeAnimatorController = classData.animatorController;
        return true;
    }

    private void SynchronizeClassVisual(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return;

        if (IsServerStarted)
        {
            ObserversRpcApplyClassVisual(className);
            return;
        }

        if (IsOwner && IsClientStarted)
            ServerRpcSetClassVisual(className);
    }

    [ServerRpc]
    private void ServerRpcSetClassVisual(string className)
    {
        if (FindClassData(className) == null)
        {
            Debug.LogWarning($"[PlayerController] Cannot synchronize unknown class '{className}'.");
            return;
        }

        ObserversRpcApplyClassVisual(className);
    }

    [ObserversRpc(BufferLast = true, RunLocally = true)]
    private void ObserversRpcApplyClassVisual(string className)
    {
        ClassData classData = FindClassData(className);
        if (classData != null)
            ApplyClassAnimatorVisual(classData);
    }

    private static ClassData FindClassData(string className)
    {
        CharacterSelectionConfig selectionConfig = Resources.Load<CharacterSelectionConfig>("CharacterSelectionConfig");
        if (selectionConfig == null || selectionConfig.availableClasses == null)
            return null;

        for (int i = 0; i < selectionConfig.availableClasses.Length; i++)
        {
            ClassData classData = selectionConfig.availableClasses[i];
            if (classData != null && string.Equals(classData.className, className, StringComparison.Ordinal))
                return classData;
        }

        return selectionConfig.defaultClass != null &&
               string.Equals(selectionConfig.defaultClass.className, className, StringComparison.Ordinal)
            ? selectionConfig.defaultClass
            : null;
    }

    private void EquipMainHandWeapon(WeaponConfig weaponConfig)
    {
        if (weaponConfig == null)
            return;

        _equippedMainWeaponConfig = weaponConfig;
        _currentMainWeaponSettings = weaponConfig.ToWeaponSettings();
        SyncWeaponGrantedAbility(weaponConfig);

        string weaponName = weaponConfig.weaponName;
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            Debug.LogWarning("[PlayerController] Cannot equip weapon: weaponName is empty on WeaponConfig.");
            return;
        }

        // In an active network session, always equip through server-spawn so NetworkTransform/
        // NetworkAnimator are synchronized for all clients.
        if (IsServerStarted)
        {
            ServerEquipMainHandWeaponByName(weaponName);
            return;
        }

        if (IsOwner && IsClientStarted)
        {
            ServerRpcEquipMainHandWeaponByName(weaponName);
            return;
        }

        WeaponSettings settings = weaponConfig.ToWeaponSettings();
        if (settings.weaponPrefab == null)
            return;

        WeaponHolder weaponHolder = GetOrCreateMainWeaponHolder();
        weaponHolder.EquipWeapon(settings.weaponPrefab);
    }

    private void UnequipMainHandWeapon()
    {
        _equippedMainWeaponConfig = null;
        _currentMainWeaponSettings = null;

        WeaponHolder weaponHolder = GetExistingMainWeaponHolder();
        if (weaponHolder != null)
            weaponHolder.UnequipWeapon();
    }

    [ServerRpc]
    private void ServerRpcEquipMainHandWeaponByName(string weaponName)
    {
        ServerEquipMainHandWeaponByName(weaponName);
    }

    private void ServerEquipMainHandWeaponByName(string weaponName)
    {
        if (!IsServerStarted)
            return;

        WeaponConfig weaponConfig = WeaponConfigRegistry.GetConfig(weaponName);
        if (weaponConfig == null)
        {
            Debug.LogWarning($"[PlayerController] Cannot equip weapon '{weaponName}': config not found in WeaponConfigRegistry.");
            return;
        }

        // Keep authoritative ability state in sync for dedicated server gameplay logic.
        SyncWeaponGrantedAbility(weaponConfig);

        WeaponSettings settings = weaponConfig.ToWeaponSettings();
        if (settings.weaponPrefab == null)
        {
            Debug.LogWarning($"[PlayerController] Cannot equip weapon '{weaponName}': weaponPrefab is null.");
            return;
        }

        WeaponHolder weaponHolder = GetOrCreateMainWeaponHolder();

        // Robust cleanup: if the cached reference is stale, still clear any existing held weapon.
        // This guarantees class/character weapon swaps replace the previous main-hand object.
        GameObject existingWeapon = weaponHolder.GetCurrentWeapon();
        if (existingWeapon != null)
        {
            NetworkObject existingNob = existingWeapon.GetComponent<NetworkObject>();
            if (existingNob != null && existingNob.IsSpawned)
            {
                InstanceFinder.ServerManager.Despawn(existingNob);
            }
            else
            {
                Destroy(existingWeapon);
            }
        }

        if (_currentMainWeaponNob != null)
        {
            if (_currentMainWeaponNob.IsSpawned)
                InstanceFinder.ServerManager.Despawn(_currentMainWeaponNob);
            _currentMainWeaponNob = null;
        }

        GameObject spawnedWeapon = Instantiate(settings.weaponPrefab);
        NetworkObject weaponNob = spawnedWeapon.GetComponent<NetworkObject>();
        if (weaponNob == null)
        {
            Debug.LogError($"[PlayerController] Weapon prefab '{settings.weaponPrefab.name}' has no NetworkObject component.");
            Destroy(spawnedWeapon);
            return;
        }

        InstanceFinder.ServerManager.Spawn(spawnedWeapon, Owner);
        weaponNob.SetParent(this.NetworkObject);
        _currentMainWeaponNob = weaponNob;

        ObserversRpcSetupPlayerWeaponVisuals(
            weaponNob,
            weaponName,
            settings.aimingRadius,
            settings.northEastOffset,
            settings.northWestOffset,
            settings.southEastOffset,
            settings.southWestOffset,
            settings.lockTo2Directions,
            settings.flipWeaponOnTurn,
            settings.flipWeaponOnYAxis,
            settings.flipWeaponOnXAxis,
            settings.weaponBehindOnNE,
            settings.weaponBehindOnNW,
            settings.weaponBehindOnSE,
            settings.weaponBehindOnSW,
            settings.handBehindOnNE,
            settings.handBehindOnNW,
            settings.handBehindOnSE,
            settings.handBehindOnSW,
            settings.handRotationOffset);
    }

    [ObserversRpc(BufferLast = true, RunLocally = true)]
    private void ObserversRpcSetupPlayerWeaponVisuals(
        NetworkObject mainWeaponNob,
        string weaponName,
        float aimingRadius,
        Vector2 northEastOffset,
        Vector2 northWestOffset,
        Vector2 southEastOffset,
        Vector2 southWestOffset,
        bool lockTo2Directions,
        bool flipWeaponOnTurn,
        bool flipWeaponOnYAxis,
        bool flipWeaponOnXAxis,
        bool weaponBehindOnNE,
        bool weaponBehindOnNW,
        bool weaponBehindOnSE,
        bool weaponBehindOnSW,
        bool handBehindOnNE,
        bool handBehindOnNW,
        bool handBehindOnSE,
        bool handBehindOnSW,
        float handRotationOffset)
    {
        if (mainWeaponNob == null)
            return;

        WeaponHolder weaponHolder = GetOrCreateMainWeaponHolder();

        weaponHolder.SetupNetworkWeapon(mainWeaponNob.gameObject);

        _currentMainWeaponSettings = new WeaponSettings
        {
            aimingRadius = aimingRadius,
            northEastOffset = northEastOffset,
            northWestOffset = northWestOffset,
            southEastOffset = southEastOffset,
            southWestOffset = southWestOffset,
            lockTo2Directions = lockTo2Directions,
            flipWeaponOnTurn = flipWeaponOnTurn,
            flipWeaponOnYAxis = flipWeaponOnYAxis,
            flipWeaponOnXAxis = flipWeaponOnXAxis,
            weaponBehindOnNE = weaponBehindOnNE,
            weaponBehindOnNW = weaponBehindOnNW,
            weaponBehindOnSE = weaponBehindOnSE,
            weaponBehindOnSW = weaponBehindOnSW,
            handBehindOnNE = handBehindOnNE,
            handBehindOnNW = handBehindOnNW,
            handBehindOnSE = handBehindOnSE,
            handBehindOnSW = handBehindOnSW,
            handRotationOffset = handRotationOffset
        };

        if (!string.IsNullOrWhiteSpace(weaponName))
        {
            WeaponConfig weaponConfig = WeaponConfigRegistry.GetConfig(weaponName);
            if (weaponConfig != null)
            {
                _equippedMainWeaponConfig = weaponConfig;
                SyncWeaponGrantedAbility(weaponConfig);
            }
        }
    }

    /// <summary>
    /// Replaces all equipped accessories with the supplied configs. Accessories are purely visual
    /// here, so they are instantiated locally rather than network-spawned like weapons.
    /// </summary>
    public void EquipAccessories(IReadOnlyList<AccessoryConfig> accessoryConfigs)
    {
        AccessoryHolder accessoryHolder = GetOrCreateAccessoryHolder();
        accessoryHolder.UnequipAllAccessories();
        _equippedAccessorySettings.Clear();

        if (accessoryConfigs == null)
            return;

        for (int i = 0; i < accessoryConfigs.Count; i++)
        {
            AccessoryConfig config = accessoryConfigs[i];
            if (config == null)
                continue;

            AccessorySettings settings = config.ToAccessorySettings();
            if (settings.accessoryPrefab == null)
            {
                Debug.LogWarning($"[PlayerController] Cannot equip accessory '{config.accessoryname}': prefab is null.");
                continue;
            }

            accessoryHolder.EquipAccessory(settings.accessoryPrefab, settings.animatorController);
            _equippedAccessorySettings.Add(settings);
        }
    }

    private AccessoryHolder GetOrCreateAccessoryHolder()
    {
        Transform namedHolder = transform.Find("AccessoryHolder");
        if (namedHolder != null)
        {
            AccessoryHolder holderOnNamedChild = namedHolder.GetComponent<AccessoryHolder>();
            if (holderOnNamedChild == null)
                holderOnNamedChild = namedHolder.gameObject.AddComponent<AccessoryHolder>();

            return holderOnNamedChild;
        }

        AccessoryHolder anyExistingHolder = GetComponentInChildren<AccessoryHolder>(true);
        if (anyExistingHolder != null)
            return anyExistingHolder;

        GameObject holderObject = new GameObject("AccessoryHolder");
        holderObject.transform.SetParent(transform);
        holderObject.transform.localPosition = Vector3.zero;
        holderObject.transform.localRotation = Quaternion.identity;
        holderObject.transform.localScale = Vector3.one;

        return holderObject.AddComponent<AccessoryHolder>();
    }

    private void SyncWeaponGrantedAbility(WeaponConfig weaponConfig)
    {
        CharacterAbilityManager abilityManager = GetComponent<CharacterAbilityManager>();
        abilityManager?.SetWeaponAbility(weaponConfig != null ? weaponConfig.grantedPrimaryAbility : null);
        abilityManager?.SetSecondaryWeaponAbility(weaponConfig != null ? weaponConfig.grantedSecondaryAbility : null);

    }

    private WeaponHolder GetOrCreateMainWeaponHolder()
    {
        WeaponHolder existingHolder = GetExistingMainWeaponHolder();
        if (existingHolder != null)
            return existingHolder;

        Transform namedHolder = transform.Find("WeaponHolder");
        if (namedHolder != null)
        {
            Debug.LogWarning("[PlayerController] Added missing WeaponHolder component on existing child 'WeaponHolder'.");
            return namedHolder.gameObject.AddComponent<WeaponHolder>();
        }

        GameObject holderObject = new GameObject("WeaponHolder");
        holderObject.transform.SetParent(transform);
        holderObject.transform.localPosition = Vector3.zero;
        holderObject.transform.localRotation = Quaternion.identity;
        holderObject.transform.localScale = Vector3.one;

        Debug.LogWarning("[PlayerController] Created missing child 'WeaponHolder' at runtime.");
        return holderObject.AddComponent<WeaponHolder>();
    }

    private WeaponHolder GetExistingMainWeaponHolder()
    {
        Transform namedHolder = transform.Find("WeaponHolder");
        if (namedHolder != null)
        {
            WeaponHolder holder = namedHolder.GetComponent<WeaponHolder>();
            if (holder != null)
                return holder;
        }

        return GetComponentInChildren<WeaponHolder>(true);
    }

    private void OnEnable()
    {
        EnableInputActions();
    }

    private void OnDisable()
    {
        DisableInputActions();
    }

    protected override void HandleUpdate()
    {
        if (!InputEnabled || !IsOwner)
            return;

        ReadInputs();
        UpdateMainWeaponPresentation();
        HandleAbilityInput();
    }

    private void LateUpdate()
    {
        if (IsOwner)
            return;

        WeaponHolder weaponHolder = GetOrCreateMainWeaponHolder();
        if (weaponHolder == null || !weaponHolder.HasWeapon())
            return;

        GameObject weapon = weaponHolder.GetCurrentWeapon();
        if (weapon == null)
            return;

        ApplyFacingVisual(IsFacingLeftFromAngle(weapon.transform.localEulerAngles.z));
    }

    private void FixedUpdate()
    {
        if (!isAlive || !InputEnabled || !IsOwner || _rigidbody == null)
            return;

        DataDrivenAbility[] abilities = GetComponents<DataDrivenAbility>();
        for (int i = 0; i < abilities.Length; i++)
        {
            if (abilities[i] != null && !abilities[i].HasPlayerControl)
                return;
        }

        // Use Organism.MoveSpeed so runtime stat modifiers (slow, root, cast penalties)
        // immediately affect player movement without duplicating speed state.
        float effectiveMoveSpeed = MoveSpeed;
        Vector2 velocity = _moveInput * effectiveMoveSpeed;

        MovementAbility[] movementAbilities = GetComponents<MovementAbility>();
        for (int i = 0; i < movementAbilities.Length; i++)
        {
            if (movementAbilities[i] != null)
                velocity += movementAbilities[i].AdditiveVelocity;
        }

        _rigidbody.linearVelocity = velocity;
        UpdateMovementPresentation(velocity);
    }

    private void ReadInputs()
    {
        if (_moveAction != null)
            _moveInput = _moveAction.ReadValue<Vector2>();

        if (_aimAction != null)
            _aimInput = _aimAction.ReadValue<Vector2>();
    }

    private void HandleAbilityInput()
    {
        for (int i = 0; i < _abilityActions.Length; i++)
        {
            if (_abilityActions[i] != null && _abilityActions[i].WasPressedThisFrame())
                TriggerAbility(i);
        }
    }

    private void TriggerAbility(int slotIndex)
    {
        if (!InputEnabled || !IsOwner)
            return;

        DataDrivenAbility ability = GetComponent<CharacterAbilityManager>()?.GetDataDrivenAbilityAtSlot(slotIndex);
        if (ability == null)
            return;

        ability.SetAbilitySlot(slotIndex);
        ability.TryUseAbilityManually();
    }

    private Animator ResolveBodyAnimator()
    {
        if (_bodyAnimator == null)
            _bodyAnimator = GetComponentInChildren<Animator>();

        return _bodyAnimator;
    }

    private void UpdateMovementPresentation(Vector2 velocity)
    {
        ApplyFacingFromAim();

        Animator bodyAnimator = ResolveBodyAnimator();
        if (bodyAnimator != null && !string.IsNullOrWhiteSpace(_movingParameter))
            bodyAnimator.SetBool(_movingParameter, velocity.sqrMagnitude > 0f);
    }

    private void ApplyFacingFromAim()
    {
        Vector2 position = _rigidbody != null ? _rigidbody.position : (Vector2)transform.position;
        Vector2 aimWorld = GetAimWorldPosition();
        float directionX = aimWorld.x - position.x;
        if (Mathf.Abs(directionX) < _facingDirectionThreshold)
            return;

        bool facingLeft = directionX < 0f;
        if (_hasFacing && facingLeft == _isFacingLeft)
            return;

        SpriteRenderer renderTarget = spriteRenderer != null ? spriteRenderer : ResolveCharacterSpriteRenderer();
        if (renderTarget != null)
            renderTarget.flipX = facingLeft;

        _isFacingLeft = facingLeft;
        _hasFacing = true;
    }

    private void EnsureWeaponSortingManager()
    {
        if (_weaponSortingManager == null)
            _weaponSortingManager = GetComponent<WeaponSortingManager>();

        if (_weaponSortingManager == null)
            _weaponSortingManager = gameObject.AddComponent<WeaponSortingManager>();

        _characterSpriteRenderer = ResolveCharacterSpriteRenderer();
        if (_characterSpriteRenderer != null)
            _weaponSortingManager.Initialize(_characterSpriteRenderer, _currentCharacterData);
    }

    private SpriteRenderer ResolveCharacterSpriteRenderer()
    {
        Animator bodyAnimator = ResolveBodyAnimator();
        if (bodyAnimator != null)
        {
            SpriteRenderer bodyRenderer = bodyAnimator.GetComponent<SpriteRenderer>();
            if (bodyRenderer != null)
                return bodyRenderer;
        }

        if (_visualTransform != null)
        {
            SpriteRenderer visualRenderer = _visualTransform.GetComponentInChildren<SpriteRenderer>(true);
            if (visualRenderer != null)
                return visualRenderer;
        }

        return GetComponentInChildren<SpriteRenderer>(true);
    }

    private void UpdateMainWeaponPresentation()
    {
        if (!IsOwner || _currentMainWeaponSettings == null)
            return;

        WeaponHolder weaponHolder = GetOrCreateMainWeaponHolder();
        if (weaponHolder == null || !weaponHolder.HasWeapon())
            return;

        GameObject currentWeapon = weaponHolder.GetCurrentWeapon();
        if (currentWeapon == null)
            return;

        EnsureWeaponSortingManager();
        if (_weaponSortingManager == null || _characterSpriteRenderer == null)
            return;

        Vector2 origin = _rigidbody != null ? _rigidbody.position : (Vector2)transform.position;
        Vector2 aimWorld = GetAimWorldPosition();
        Vector2 aimDirection = aimWorld - origin;
        WeaponSortingManager.Direction aimDir = GetAimDirection(aimDirection);

        _weaponSortingManager.UpdateActiveAimingWeapon(
            currentWeapon.transform,
            _currentMainWeaponSettings,
            _equippedMainWeaponConfig != null ? _equippedMainWeaponConfig.weaponName : currentWeapon.name,
            aimDir,
            transform,
            Camera.main,
            _characterSpriteRenderer,
            false,
            transform.Find("BackpackHolder"),
            () => _isFacingLeft,
            value => _isFacingLeft = value,
            _ => { },
            IsClientStarted || IsServerStarted,
            IsOwner,
            null);
    }

    private void ApplyFacingVisual(bool facingLeft)
    {
        SpriteRenderer renderTarget = spriteRenderer != null ? spriteRenderer : ResolveCharacterSpriteRenderer();
        if (renderTarget != null)
            renderTarget.flipX = facingLeft;

        _isFacingLeft = facingLeft;
        _hasFacing = true;
    }

    private WeaponSortingManager.Direction GetAimDirection(Vector2 aimDirection)
    {
        if (aimDirection.sqrMagnitude <= 0.0001f)
            return _isFacingLeft ? WeaponSortingManager.Direction.SouthWest : WeaponSortingManager.Direction.SouthEast;

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        if (angle < 0f)
            angle += 360f;

        if (angle >= 22.5f && angle < 90f)
            return WeaponSortingManager.Direction.NorthEast;
        if (angle >= 90f && angle < 156.5f)
            return WeaponSortingManager.Direction.NorthWest;
        if (angle >= 156.5f && angle < 270f)
            return WeaponSortingManager.Direction.SouthWest;

        return WeaponSortingManager.Direction.SouthEast;
    }

    private bool IsFacingLeftFromAngle(float angleDegrees)
    {
        float normalized = Mathf.Repeat(angleDegrees, 360f);
        return normalized > 90f && normalized < 270f;
    }

    private Vector2 GetAimWorldPosition()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null)
            return _rigidbody != null ? _rigidbody.position : (Vector2)transform.position;

        Vector3 screenPoint = new Vector3(_aimInput.x, _aimInput.y, activeCamera.nearClipPlane);
        Vector3 worldPoint = activeCamera.ScreenToWorldPoint(screenPoint);
        worldPoint.z = 0f;
        return worldPoint;
    }

    protected override void HandleDeath()
    {
        if (_bodyAnimator != null)
            _bodyAnimator.Play("Death");
        DisableInputActions();
        EndScreenUI.Instance.ShowEndScreen(10);
    }

    private void InitializeInputActions()
    {
        string[] expectedAbilityActions = { "Ability1", "Ability2", "Ability3", "Ability4", "Ability5", "Ability6" };
        if (_abilityActionNames == null || _abilityActionNames.Length != expectedAbilityActions.Length)
            _abilityActionNames = expectedAbilityActions;

        if (_inputAsset == null)
        {
#if UNITY_EDITOR
            _inputAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/HeltowerInputs.inputactions");
#endif
        }

        if (_inputAsset != null)
        {
            _playerMap = _inputAsset.FindActionMap(_playerMapName);
            if (_playerMap != null)
            {
                _moveAction = _playerMap.FindAction(_moveActionName);
                _aimAction = _playerMap.FindAction(_aimActionName);

                for (int i = 0; i < _abilityActions.Length; i++)
                {
                    _abilityActions[i] = _playerMap.FindAction(_abilityActionNames[i]);
                }
            }
        }

        if (_moveAction == null)
        {
            _moveAction = new InputAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        if (_aimAction == null)
            _aimAction = new InputAction("Aim", InputActionType.Value);

        for (int i = 0; i < _abilityActions.Length; i++)
        {
            if (_abilityActions[i] == null)
            {
                InputAction action = new InputAction(_abilityActionNames[i], InputActionType.Button);
                switch (i)
                {
                    case 0:
                        action.AddBinding("<Mouse>/leftButton");
                        break;
                    case 1:
                        action.AddBinding("<Mouse>/rightButton");
                        break;
                    case 2:
                        action.AddBinding("<Keyboard>/shift");
                        break;
                    case 3:
                        action.AddBinding("<Keyboard>/q");
                        break;
                    case 4:
                        action.AddBinding("<Keyboard>/e");
                        break;
                    case 5:
                        action.AddBinding("<Keyboard>/r");
                        break;
                }

                _abilityActions[i] = action;
            }
        }
    }

    private void EnableInputActions()
    {
        if (_playerMap != null)
            _playerMap.Enable();
        else
        {
            if (_moveAction != null) _moveAction.Enable();
            if (_aimAction != null) _aimAction.Enable();
            for (int i = 0; i < _abilityActions.Length; i++)
            {
                if (_abilityActions[i] != null)
                    _abilityActions[i].Enable();
            }
        }
    }

    private void DisableInputActions()
    {
        if (_playerMap != null)
            _playerMap.Disable();
        else
        {
            if (_moveAction != null) _moveAction.Disable();
            if (_aimAction != null) _aimAction.Disable();
            for (int i = 0; i < _abilityActions.Length; i++)
            {
                if (_abilityActions[i] != null)
                    _abilityActions[i].Disable();
            }
        }
    }

    
}
