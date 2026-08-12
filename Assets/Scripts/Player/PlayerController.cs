using System;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
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

    [Header("Network Presentation")]
    [SerializeField, Min(0f)] private float _aimSyncSendThresholdDegrees = 0.35f;
    [SerializeField, Min(0f)] private float _remoteAimSmoothing = 20f;

    [Header("Abilities")]
    [SerializeField] private DataDrivenAbility[] _abilities = new DataDrivenAbility[5];
    [SerializeField] private InputActionAsset _inputAsset;
    [SerializeField] private string _playerMapName = "Player";
    [SerializeField] private string _moveActionName = "Movement";
    [SerializeField] private string _aimActionName = "Aim";
    [SerializeField] private string[] _abilityActionNames = { "Ability1", "Ability2", "Ability3", "Ability4", "Ability5" };

    public static bool InputEnabled { get; set; } = true;
    public static PlayerController LocalPlayer { get; private set; }
    public float CurrentEnergy { get; private set; } = 100f;

    private CharacterData _currentCharacterData;
    private NetworkObject _currentMainWeaponNob;
    private WeaponConfig _equippedMainWeaponConfig;
    private WeaponSettings _currentMainWeaponSettings;
    private WeaponSortingManager _weaponSortingManager;
    private SpriteRenderer _characterSpriteRenderer;
    private bool _isFacingLeft;
    private float _lastSentAimAngle;
    private bool _hasSentAimAngle;
    private bool _lastSentFacingLeft;
    private float _targetRemoteAimAngle;
    private float _smoothedRemoteAimAngle;
    private bool _hasRemoteAimAngle;
    private bool _syncWeaponFlipYValue;
    private string _assignedCharacterName;
    private Organism _organism;
    private bool _hasFacing;

    private readonly SyncVar<float> _syncAimAngle = new SyncVar<float>();
    private readonly SyncVar<bool> _syncFacingLeft = new SyncVar<bool>();
    private readonly SyncVar<bool> _syncWeaponFlipY = new SyncVar<bool>();
    /// <summary>Fired on the local owner when the player spawns / gains ownership.</summary>
    public static event Action<PlayerController> OnPlayerSpawned;

    /// <summary>Fired when an attack ability executes.</summary>
    public event Action<AbilityDataConfig> OnAttack;
    public event Action<AbilityDataConfig, GameObject, float, string> OnAttackDamage;


    /// <summary>Fired when a stats recalculation is requested (e.g. after trait changes).</summary>
    public event Action OnStatsRecalculationRequested;

    public CharacterData GetCurrentCharacterData() => _currentCharacterData;
    public void SetCurrentCharacterData(CharacterData characterData) => _currentCharacterData = characterData;

    public bool ApplyClassAnimator(ClassData classData)
    {
        if (classData == null)
        {
            Debug.LogWarning("[PlayerController] ApplyClassAnimator called with null class data.");
            return false;
        }

        Animator bodyAnimator = ResolveBodyAnimator();
        if (bodyAnimator == null)
        {
            Debug.LogWarning("[PlayerController] Cannot switch class animator because no Animator is attached.");
            return false;
        }

        RuntimeAnimatorController controller = classData.animatorController;
        if (controller == null)
        {
            Debug.LogWarning($"[PlayerController] Class '{classData.className}' has no animator controller assigned.");
            return false;
        }

        bodyAnimator.runtimeAnimatorController = controller;

        WeaponConfig defaultWeapon = GetDefaultWeaponForClass(classData);
        if (defaultWeapon != null)
            EquipMainHandWeapon(defaultWeapon);

        Debug.Log($"[PlayerController] Switched animator to class '{classData.className}'.");
        return true;
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

        if (_organism == null)
            _organism = GetComponent<Organism>();

        // Stats -> runtime Organism container, then refresh derived values.
        StatContainer target = AllStats;
        if (target != null && characterData.statContainer != null)
        {
            characterData.statContainer.CopyToStatContainer(target);
            _organism?.RefreshMoveSpeedFromStats();
        }

        // Refill to the new maximums.
        if (_organism != null)
            _organism.ModifyHealth(_organism.MaxHealth - _organism.CurrentHealth);
        CurrentEnergy = target != null ? target.GetStat("MaxEnergy", 100f) : 100f;

        // Body animator (class visual).
        Animator bodyAnimator = ResolveBodyAnimator();
        RuntimeAnimatorController controller = characterData.GetAnimatorController();
        if (bodyAnimator != null && controller != null)
            bodyAnimator.runtimeAnimatorController = controller;

        // Main-hand weapon.
        if (characterData.mainHandWeaponConfig != null)
            EquipMainHandWeapon(characterData.mainHandWeaponConfig);

        // Ability loadout.
        GetComponent<CharacterAbilityManager>()?.LoadCharacterAbilities(characterData);

        // Notify observers so dependent systems refresh.
        GetComponent<CharacterDataObserver>()?.SetCharacterData(characterData);
        GetComponent<CharacterTraitManager>()?.SetCharacterData(characterData);

        Debug.Log($"[PlayerController] Applied character '{characterData.displayName}'.");
    }

    /// <summary>Runtime, trait-merged stat container (sourced from the Organism on this object).</summary>
    public StatContainer AllStats
    {
        get
        {
            if (_organism == null)
                _organism = GetComponent<Organism>();
            return _organism != null ? _organism.AllStats : null;
        }
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

    /// <summary>
    /// Tells all non-server clients to spawn a muzzle flash for this player.
    /// The owner already spawned theirs immediately, so it is skipped here.
    /// </summary>
    [ObserversRpc]
    public void ObserversRpcSpawnMuzzleFlash(int abilitySlot, string abilityName, Vector3 position, float angle)
    {
        if (IsOwner) return;

        CharacterAbilityManager mgr = GetComponent<CharacterAbilityManager>();
        if (mgr == null) return;

        DataDrivenAbility ability = mgr.FindDataDrivenAbility(abilitySlot, abilityName);
        ability?.SpawnMuzzleFlashLocally(position, angle);
    }

    private InputActionMap _playerMap;
    private InputAction _moveAction;
    private InputAction _aimAction;
    private InputAction[] _abilityActions = new InputAction[5];
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

    private void Awake()
    {
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

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        _syncAimAngle.OnChange += OnAimAngleSyncChanged;
        _syncFacingLeft.OnChange += OnFacingSyncChanged;
        _syncWeaponFlipY.OnChange += OnWeaponFlipYSyncChanged;

        if (!base.Owner.IsLocalClient)
        {
            _targetRemoteAimAngle = _syncAimAngle.Value;
            _smoothedRemoteAimAngle = _targetRemoteAimAngle;
            _hasRemoteAimAngle = true;
        }
    }

    public override void OnStopNetwork()
    {
        _syncAimAngle.OnChange -= OnAimAngleSyncChanged;
        _syncFacingLeft.OnChange -= OnFacingSyncChanged;
        _syncWeaponFlipY.OnChange -= OnWeaponFlipYSyncChanged;
        base.OnStopNetwork();
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

        ClassData defaultClass = selectionConfig.defaultClass;
        if (defaultClass == null && selectionConfig.availableClasses != null && selectionConfig.availableClasses.Length > 0)
            defaultClass = selectionConfig.availableClasses[0];

        if (defaultClass == null)
        {
            Debug.LogWarning("[PlayerController] CharacterSelectionConfig has no default/available class.");
            return;
        }

        CharacterData runtimeCharacter = selectionConfig.CreateCharacterFromClass(defaultClass);
        if (runtimeCharacter == null)
            return;

        CharacterSelectionManager.Instance?.SelectCharacter(runtimeCharacter);
        ApplyCharacterData(runtimeCharacter);
    }

    private WeaponConfig GetDefaultWeaponForClass(ClassData classData)
    {
        if (classData == null || classData.availableWeapons == null || classData.availableWeapons.Length == 0)
            return null;

        return classData.availableWeapons[0];
    }

    private void EquipMainHandWeapon(WeaponConfig weaponConfig)
    {
        if (weaponConfig == null)
            return;

        _equippedMainWeaponConfig = weaponConfig;
        _currentMainWeaponSettings = weaponConfig.ToWeaponSettings();

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

        WeaponSettings settings = weaponConfig.ToWeaponSettings();
        if (settings.weaponPrefab == null)
        {
            Debug.LogWarning($"[PlayerController] Cannot equip weapon '{weaponName}': weaponPrefab is null.");
            return;
        }

        if (_currentMainWeaponNob != null)
        {
            InstanceFinder.ServerManager.Despawn(_currentMainWeaponNob);
            _currentMainWeaponNob = null;
        }

        WeaponHolder weaponHolder = GetOrCreateMainWeaponHolder();

        GameObject spawnedWeapon = Instantiate(settings.weaponPrefab);
        NetworkObject weaponNob = spawnedWeapon.GetComponent<NetworkObject>();
        if (weaponNob == null)
        {
            Debug.LogError($"[PlayerController] Weapon prefab '{settings.weaponPrefab.name}' has no NetworkObject component.");
            Destroy(spawnedWeapon);
            return;
        }

        InstanceFinder.ServerManager.Spawn(spawnedWeapon);
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
            }
        }
    }

    private WeaponHolder GetOrCreateMainWeaponHolder()
    {
        Transform namedHolder = transform.Find("WeaponHolder");
        if (namedHolder != null)
        {
            WeaponHolder holderOnNamedChild = namedHolder.GetComponent<WeaponHolder>();
            if (holderOnNamedChild == null)
            {
                holderOnNamedChild = namedHolder.gameObject.AddComponent<WeaponHolder>();
                Debug.LogWarning("[PlayerController] Added missing WeaponHolder component on existing child 'WeaponHolder'.");
            }

            return holderOnNamedChild;
        }

        WeaponHolder anyExistingHolder = GetComponentInChildren<WeaponHolder>(true);
        if (anyExistingHolder != null)
            return anyExistingHolder;

        GameObject holderObject = new GameObject("WeaponHolder");
        holderObject.transform.SetParent(transform);
        holderObject.transform.localPosition = Vector3.zero;
        holderObject.transform.localRotation = Quaternion.identity;
        holderObject.transform.localScale = Vector3.one;

        Debug.LogWarning("[PlayerController] Created missing child 'WeaponHolder' at runtime.");
        return holderObject.AddComponent<WeaponHolder>();
    }

    private void OnEnable()
    {
        EnableInputActions();
    }

    private void OnDisable()
    {
        DisableInputActions();
    }

    private void Update()
    {
        if (!InputEnabled)
            return;

        if (IsOwner)
        {
            ReadInputs();
            if (TryGetOwnerAimAngle(out float ownerAimAngle))
            {
                bool facingLeftFromAim = IsFacingLeftFromAngle(ownerAimAngle);
                ApplyFacingVisual(facingLeftFromAim);
                bool weaponFlipY = GetWeaponFlipYForAngle(ownerAimAngle);
                PushAimStateToNetwork(ownerAimAngle, facingLeftFromAim, weaponFlipY);
            }
        }
        else
        {
            UpdateRemoteAimSmoothingAndFacing();
        }

        UpdateMainWeaponPresentation();

        if (IsOwner)
            HandleAbilityInput();
    }

    private void FixedUpdate()
    {
        if (!InputEnabled || !IsOwner || _rigidbody == null)
            return;

        Vector2 velocity = _moveInput * _moveSpeed;
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

        if (slotIndex < 0 || slotIndex >= _abilities.Length)
            return;

        if (_abilities[slotIndex] == null)
            return;

        _abilities[slotIndex].SetAbilitySlot(slotIndex + 2);
        _abilities[slotIndex].TryUseAbility();
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
        if (_currentMainWeaponSettings == null)
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

        float aimAngle;
        WeaponSortingManager.Direction aimDir;
        float? overrideAngle = null;

        if (IsOwner)
        {
            Vector2 origin = _rigidbody != null ? _rigidbody.position : (Vector2)transform.position;
            Vector2 aimWorld = GetAimWorldPosition();
            Vector2 aimDirection = aimWorld - origin;
            aimDir = GetAimDirection(aimDirection);
            aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            if (aimAngle < 0f)
                aimAngle += 360f;
        }
        else
        {
            aimAngle = _hasRemoteAimAngle ? _smoothedRemoteAimAngle : _syncAimAngle.Value;
            Vector2 aimDirection = new Vector2(
                Mathf.Cos(aimAngle * Mathf.Deg2Rad),
                Mathf.Sin(aimAngle * Mathf.Deg2Rad));
            aimDir = GetAimDirection(aimDirection);
            overrideAngle = aimAngle;
            ApplyFacingVisual(IsFacingLeftFromAngle(aimAngle));
        }

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
            overrideAngle);

            if (!IsOwner)
                ApplyWeaponYFlip(currentWeapon.transform, _syncWeaponFlipYValue);
    }

            private void PushAimStateToNetwork(float aimAngle, bool facingLeft, bool weaponFlipY)
    {
        if (!IsOwner)
            return;

        if (IsServerStarted)
        {
            _syncAimAngle.Value = aimAngle;
            _syncFacingLeft.Value = facingLeft;
            _syncWeaponFlipY.Value = weaponFlipY;
            _hasSentAimAngle = true;
            _lastSentAimAngle = aimAngle;
            _lastSentFacingLeft = facingLeft;
            return;
        }

        bool shouldSend = !_hasSentAimAngle ||
                          Mathf.Abs(Mathf.DeltaAngle(_lastSentAimAngle, aimAngle)) >= _aimSyncSendThresholdDegrees ||
                          _lastSentFacingLeft != facingLeft;
        if (!shouldSend)
            return;

        ServerRpcSetAimPresentation(aimAngle, facingLeft, weaponFlipY);
        _hasSentAimAngle = true;
        _lastSentAimAngle = aimAngle;
        _lastSentFacingLeft = facingLeft;
    }

    [ServerRpc]
    private void ServerRpcSetAimPresentation(float aimAngle, bool facingLeft, bool weaponFlipY)
    {
        _syncAimAngle.Value = aimAngle;
        _syncFacingLeft.Value = facingLeft;
        _syncWeaponFlipY.Value = weaponFlipY;
    }

    private void OnAimAngleSyncChanged(float prev, float next, bool asServer)
    {
        if (IsOwner)
            return;

        _targetRemoteAimAngle = next;
        if (!_hasRemoteAimAngle)
        {
            _smoothedRemoteAimAngle = next;
            _hasRemoteAimAngle = true;
        }
    }

    private bool TryGetOwnerAimAngle(out float angle)
    {
        Vector2 origin = _rigidbody != null ? _rigidbody.position : (Vector2)transform.position;
        Vector2 aimWorld = GetAimWorldPosition();
        Vector2 aimDirection = aimWorld - origin;
        if (aimDirection.sqrMagnitude <= 0.000001f)
        {
            angle = 0f;
            return false;
        }

        angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        if (angle < 0f)
            angle += 360f;
        return true;
    }

    private void UpdateRemoteAimSmoothingAndFacing()
    {
        if (!_hasRemoteAimAngle)
        {
            _targetRemoteAimAngle = _syncAimAngle.Value;
            _smoothedRemoteAimAngle = _targetRemoteAimAngle;
            _hasRemoteAimAngle = true;
        }

        float smoothT = 1f - Mathf.Exp(-_remoteAimSmoothing * Time.deltaTime);
        _smoothedRemoteAimAngle = Mathf.LerpAngle(_smoothedRemoteAimAngle, _targetRemoteAimAngle, smoothT);
        ApplyFacingVisual(IsFacingLeftFromAngle(_smoothedRemoteAimAngle));
    }

    private void OnFacingSyncChanged(bool prev, bool next, bool asServer)
    {
        if (IsOwner)
            return;

        ApplyFacingVisual(next);
    }

    private void OnWeaponFlipYSyncChanged(bool prev, bool next, bool asServer)
    {
        _syncWeaponFlipYValue = next;
    }

    private bool GetWeaponFlipYForAngle(float aimAngle)
    {
        if (_currentMainWeaponSettings == null ||
            !_currentMainWeaponSettings.flipWeaponOnTurn ||
            !_currentMainWeaponSettings.flipWeaponOnYAxis)
            return false;

        return IsFacingLeftFromAngle(aimAngle);
    }

    private void ApplyWeaponYFlip(Transform weapon, bool flipY)
    {
        if (weapon == null)
            return;

        Vector3 scale = weapon.localScale;
        scale.y = flipY ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y);
        weapon.localScale = scale;
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

    public void ModifyEnergy(float amount)
    {
        CurrentEnergy = Mathf.Max(0f, CurrentEnergy + amount);
    }

    private void InitializeInputActions()
    {
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
                        action.AddBinding("<Keyboard>/q");
                        break;
                    case 1:
                        action.AddBinding("<Keyboard>/e");
                        break;
                    case 2:
                        action.AddBinding("<Keyboard>/r");
                        break;
                    case 3:
                        action.AddBinding("<Keyboard>/f");
                        break;
                    case 4:
                        action.AddBinding("<Keyboard>/c");
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
