using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Connection;

/// <summary>
/// Channeled ability implementation - spawns a channel object at weapon tip that follows mouse cursor.
/// Does not extend Ability - instantiated and called by DataDrivenAbility.
/// Deals damage and consumes energy while button is held.
/// 
/// Expected Channel Object Prefab Structure:
/// - Root GameObject (e.g., "Flamethrower")
///   - Child "Particles" with ParticleSystem component
///   - Child "Hitbox" with Collider2D component (PolygonCollider2D, BoxCollider2D, etc.)
/// The collider defines the damage area and should match the visual effect shape.
/// </summary>
public class ChannelAbility : MonoBehaviour, ISubAbility
{
    private ChannelAbilityConfig channelConfig;
    private AbilityDataConfig parentConfig;
    private PlayerController playerController;
    private Camera mainCamera;
    private Transform launchZone;
    private Transform weaponTransform;
    private Animator weaponAnimator;

    // Channel state
    private bool isChanneling = false;
    private GameObject channelObject;
    private Collider2D channelCollider;
    private ParticleSystem channelParticles;
    private float energyConsumptionTimer = 0f;
    private float damageTimer = 0f;
    private Dictionary<Enemy, float> enemyHitTimers = new Dictionary<Enemy, float>();
    private int abilitySlotIndex = 0;

    // Weapon config override state (for unlockRotation)
    private WeaponConfig originalWeaponConfig;
    private float originalAimingRadius;
    private bool originalFlipWeaponOnYAxis;
    private bool originalLockTo2Directions;
    private bool originalOverridePositioning;
    private bool hasOverriddenWeaponConfig = false;

    // Collision detection
    private ContactFilter2D contactFilter;
    private List<Collider2D> hitResults = new List<Collider2D>();

    // Visual effects
    private ParticleSystem muzzleFlash;
    private GameObject muzzleFlashLight;
    private AudioSource channelLoopAudioSource;

    private void Awake()
    {
        Debug.Log($"<color=yellow>[ChannelAbility] Awake() - Initializing on {gameObject.name}</color>");

        playerController = GetComponent<PlayerController>();
        mainCamera = Camera.main;

        Debug.Log($"[ChannelAbility] PlayerController: {playerController != null}, MainCamera: {mainCamera != null}");

        // Find weapon and LaunchZone in player hierarchy (Player > WeaponHolder > Weapon > LaunchZone)
        weaponTransform = transform.Find("WeaponHolder/Weapon");
        if (weaponTransform != null)
        {
            launchZone = WeaponLaunchPoint.FindLaunchZone(weaponTransform);

            weaponAnimator = weaponTransform.GetComponentInChildren<Animator>();

            if (launchZone == null)
            {
                Debug.LogWarning("[ChannelAbility] No LaunchZone found on weapon! Channel will spawn at weapon position.");
                launchZone = weaponTransform;
            }
        }
        else
        {
            Debug.LogWarning("[ChannelAbility] No weapon found in hierarchy! Channel will spawn at player position.");
            launchZone = transform;
        }

        Debug.Log($"<color=yellow>[ChannelAbility] Awake() complete - Ready for Initialize()</color>");
    }

    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
    }

    /// <summary>
    /// Called by DataDrivenAbility to initialize with config
    /// </summary>
    public void Initialize(AbilityDataConfig config, int slotIndex = 0)
    {
        Debug.Log($"<color=yellow>[ChannelAbility] Initialize() called - Config: {config?.abilityName}, SlotIndex: {slotIndex}</color>");

        parentConfig = config;
        this.abilitySlotIndex = slotIndex;

        if (config != null && config.channelConfig != null)
        {
            channelConfig = config.channelConfig;
            Debug.Log($"[ChannelAbility] ChannelConfig assigned:");
            Debug.Log($"  - Prefab: {channelConfig.channelObjectPrefab?.name}");
            Debug.Log($"  - Scale: {channelConfig.scale}");
            Debug.Log($"  - Start Animation: '{channelConfig.channelStartAnimationName}'");
            Debug.Log($"  - Loop Animation: '{channelConfig.channelAnimationName}'");
            Debug.Log($"  - End Animation: '{channelConfig.channelEndAnimationName}'");
            Debug.Log($"  - Energy/sec: {channelConfig.energyPerSecond}, Tick Rate: {channelConfig.energyTickRate}");
            Debug.Log($"  - Damage: {channelConfig.damage}, Tick Rate: {channelConfig.damageTickRate}");
        }
        else
        {
            Debug.LogError($"[ChannelAbility] No channelConfig found in AbilityDataConfig! Config null: {config == null}, channelConfig null: {config?.channelConfig == null}");
        }

        Debug.Log($"<color=yellow>[ChannelAbility] Initialize() complete - Ready for Activate()</color>");
    }

    /// <summary>
    /// Set which ability slot this ability is bound to (for input checking)
    /// </summary>
    public void SetAbilitySlot(int slotIndex)
    {
        abilitySlotIndex = slotIndex;
    }

    /// <summary>Ability slot this channel is bound to (used by network RPC routing).</summary>
    public int AbilitySlot => abilitySlotIndex;

    /// <summary>
    /// Server-side: instantiate and network-spawn the channel object, wire up local references,
    /// and return the spawned NetworkObject so the owner client can be notified.
    /// </summary>
    public NetworkObject ServerSpawnChannelObject(Vector3 position, Quaternion rotation)
    {
        if (channelConfig?.channelObjectPrefab == null)
            return null;

        GameObject obj = Instantiate(channelConfig.channelObjectPrefab, position, rotation, null);
        obj.transform.localScale = Vector3.one * channelConfig.scale;

        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.Spawn(obj);

        SetChannelObjectReferences(obj);
        return netObj;
    }

    /// <summary>
    /// Called by DataDrivenAbility to activate channeling
    /// Returns true if channeling started successfully
    /// </summary>
    public bool Activate()
    {
        Debug.Log($"<color=magenta>[ChannelAbility] ===== Activate() CALLED =====</color>");
        Debug.Log($"[ChannelAbility] Current state - IsChanneling: {isChanneling}, Config: {channelConfig != null}");

        if (channelConfig == null)
        {
            Debug.LogError("[ChannelAbility] Cannot activate - no config!");
            return false;
        }

        // Safety check: if we think we're channeling but have no channel object, restart
        if (isChanneling && channelObject == null)
        {
            Debug.LogWarning("<color=red>[ChannelAbility] BROKEN STATE DETECTED! IsChanneling=true but channelObject=null. Restarting channel...</color>");
            isChanneling = false;
        }

        if (!isChanneling)
        {
            Debug.Log($"<color=magenta>[ChannelAbility] Starting new channel...</color>");

            // Validate weapon references before starting (weapon may be equipped after Awake)
            ValidateWeaponReferences();

            StartChannel();
            return true;
        }
        else
        {
            Debug.Log($"[ChannelAbility] Already channeling - continuing");
            // Already channeling
            return true;
        }
    }

    /// <summary>
    /// Re-find weapon and LaunchZone if they're null or destroyed.
    /// Weapon equipment happens after Awake(), so we need to refresh references.
    /// </summary>
    private void ValidateWeaponReferences()
    {
        bool weaponValid = weaponTransform != null;
        bool animatorValid = weaponAnimator != null;
        bool launchZoneValid = launchZone != null;

        Debug.Log($"[ChannelAbility] ValidateWeaponReferences - Weapon: {weaponValid}, Animator: {animatorValid}, LaunchZone: {launchZoneValid}");

        // If any reference is missing or destroyed, re-find them
        if (!weaponValid || !animatorValid || !launchZoneValid)
        {
            Debug.Log("[ChannelAbility] Re-finding weapon references...");

            weaponTransform = transform.Find("WeaponHolder/Weapon");

            if (weaponTransform != null)
            {
                SpriteRenderer weaponSprite = weaponTransform.GetComponentInChildren<SpriteRenderer>();
                launchZone = WeaponLaunchPoint.FindLaunchZone(weaponTransform);
                weaponAnimator = weaponSprite.GetComponent<Animator>();

                Debug.Log($"[ChannelAbility] ✓ Weapon found: {weaponTransform.name}");
                Debug.Log($"[ChannelAbility] ✓ LaunchZone: {launchZone != null} (at {launchZone?.position})");
                Debug.Log($"[ChannelAbility] ✓ WeaponAnimator: {weaponAnimator != null} (controller: {weaponAnimator?.runtimeAnimatorController?.name})");

                if (launchZone == null)
                {
                    Debug.LogWarning("[ChannelAbility] No LaunchZone found on weapon! Channel will spawn at weapon position.");
                    launchZone = weaponTransform;
                }
            }
            else
            {
                Debug.LogError("[ChannelAbility] CRITICAL: No weapon found in hierarchy! Cannot spawn channel object.");
                launchZone = transform; // Fallback to player position
            }
        }
        else
        {
            Debug.Log("[ChannelAbility] All weapon references are valid.");
        }
    }

    /// <summary>
    /// Check if ability button is still being held.
    /// Uses a delegate set by DataDrivenAbility so the check exactly matches
    /// the same input path that triggered the ability (not a hardcoded mouse button).
    /// </summary>
    private System.Func<bool> holdChecker;

    public void SetHoldChecker(System.Func<bool> checker)
    {
        holdChecker = checker;
    }

    public bool IsHoldingButton()
    {
        if (holdChecker != null)
            return holdChecker.Invoke();

        // Fallback: match the slot-aware check DataDrivenAbility uses
        return InputHelper.GetMouseButton(abilitySlotIndex);
    }

    public bool IsChanneling => isChanneling;

    private void Update()
    {
        if (channelConfig == null) return;

        if (isChanneling)
        {
            // Check if button is still held
            if (!IsHoldingButton())
            {
                StopChannel();
                return;
            }

            // Consume energy
            if (channelConfig.energyPerSecond > 0f)
            {
                energyConsumptionTimer += Time.deltaTime;
                float energyToConsume = (channelConfig.energyPerSecond * channelConfig.energyTickRate);

                if (energyConsumptionTimer >= channelConfig.energyTickRate)
                {
                    if (playerController.CurrentEnergy >= energyToConsume)
                    {
                        playerController.ModifyEnergy(-energyToConsume);
                        energyConsumptionTimer = 0f;
                    }
                    else
                    {
                        // Not enough energy - stop channeling
                        StopChannel();
                        return;
                    }
                }
            }

            // Update channel object position (PlayerController handles weapon rotation)
            UpdateChannelObjectPosition();

            // Apply damage
            UpdateDamage();
        }
    }

    private void StartChannel()
    {
        Debug.Log($"<color=cyan>[ChannelAbility] ===== StartChannel() CALLED =====</color>");
        Debug.Log($"[ChannelAbility] weaponAnimator: {weaponAnimator != null}, launchZone: {launchZone != null}, channelConfig: {channelConfig != null}");

        isChanneling = true;
        energyConsumptionTimer = 0f;
        damageTimer = 0f;
        enemyHitTimers.Clear();

        // Apply weapon config overrides for orbital rotation
        if (channelConfig.unlockRotation)
        {
            ApplyWeaponConfigOverrides();
        }

        // Play start animation
        bool hasValidAnimator = false;
        try
        {
            hasValidAnimator = weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null;
        }
        catch (MissingReferenceException)
        {
            Debug.LogWarning("[ChannelAbility] weaponAnimator reference was destroyed, skipping animation.");
            weaponAnimator = null;
        }

        Debug.Log($"[ChannelAbility] Checking weapon animation - HasValidAnimator: {hasValidAnimator}, AnimName: '{channelConfig?.channelStartAnimationName}'");

        if (hasValidAnimator && !string.IsNullOrEmpty(channelConfig.channelStartAnimationName))
        {
            int stateHash = Animator.StringToHash(channelConfig.channelStartAnimationName);
            bool hasState = weaponAnimator.HasState(0, stateHash);
            Debug.Log($"[ChannelAbility] Animation state '{channelConfig.channelStartAnimationName}' - Hash: {stateHash}, HasState: {hasState}");

            if (hasState)
            {
                weaponAnimator.Play(channelConfig.channelStartAnimationName);
                Debug.Log($"<color=green>[ChannelAbility] ✓ Playing animation '{channelConfig.channelStartAnimationName}' on {weaponAnimator.gameObject.name}</color>");
            }
            else
            {
                Debug.LogWarning($"[ChannelAbility] Animation state '{channelConfig.channelStartAnimationName}' not found in weapon animator. Controller: {weaponAnimator.runtimeAnimatorController.name}");
            }
        }
        else
        {
            Debug.LogWarning($"[ChannelAbility] Cannot play animation - weaponAnimator is null: {weaponAnimator == null}, or no animation name configured");
        }

        // Spawn channel object
        Debug.Log($"[ChannelAbility] Spawning channel object - Prefab: {channelConfig?.channelObjectPrefab != null}, LaunchZone: {launchZone != null}");

        if (channelConfig.channelObjectPrefab != null && launchZone != null)
        {
            Debug.Log($"[ChannelAbility] Spawning channel object '{channelConfig.channelObjectPrefab.name}' at {launchZone.position}");

            var nm = InstanceFinder.NetworkManager;
            bool isNetworkActive = nm != null && (nm.IsServerStarted || nm.IsClientStarted);

            if (!isNetworkActive)
            {
                // Single player — instantiate and set up locally
                Debug.Log("[ChannelAbility] Single-player: instantiating channel object locally");
                GameObject obj = Instantiate(channelConfig.channelObjectPrefab, launchZone.position, weaponTransform.rotation, null);
                obj.transform.localScale = Vector3.one * channelConfig.scale;
                SetChannelObjectReferences(obj);
            }
            else if (nm.IsServerStarted)
            {
                // Server (or host): instantiate, network-spawn if prefab has NetworkObject, set up references
                Debug.Log("[ChannelAbility] Server: instantiating and network-spawning channel object");
                GameObject obj = Instantiate(channelConfig.channelObjectPrefab, launchZone.position, weaponTransform.rotation, null);
                obj.transform.localScale = Vector3.one * channelConfig.scale;
                NetworkObject netObj = obj.GetComponent<NetworkObject>();
                if (netObj != null)
                    nm.ServerManager.Spawn(obj);
                SetChannelObjectReferences(obj);
            }
            else if (playerController != null && playerController.IsOwner)
            {
                // Client owner: request the server to spawn the channel object.
                // SetChannelObjectReferences() will be called asynchronously when
                // TargetRpcReceiveChannelObject arrives from the server.
                Debug.Log("[ChannelAbility] Client owner: requesting server to spawn channel object via ServerRpc");
                playerController.ServerRpcSpawnChannelObject(abilitySlotIndex, launchZone.position, weaponTransform.rotation);
            }
            else
            {
                Debug.LogWarning("[ChannelAbility] Network active but not server or owner — skipping channel object spawn");
            }
        }
        else
        {
            Debug.LogError($"[ChannelAbility] Cannot spawn channel object - Prefab is null: {channelConfig?.channelObjectPrefab == null}, LaunchZone is null: {launchZone == null}");
        }

        // Play muzzle flash
        if (channelConfig.muzzleFlashPrefab != null && launchZone != null)
        {
            float angle = launchZone.eulerAngles.z;
            bool shouldFlipY = Mathf.Abs(angle) > 90f;
            muzzleFlash = ProjectileSpawner.InstantiateMuzzleFlashRoot(channelConfig.muzzleFlashPrefab, launchZone.position, launchZone.rotation, launchZone, shouldFlipY, false);
            muzzleFlash.Play();
        }

        // Create muzzle light
        if (channelConfig.enableMuzzleLight && launchZone != null)
        {
            muzzleFlashLight = new GameObject("ChannelLight");
            muzzleFlashLight.transform.SetParent(launchZone);
            muzzleFlashLight.transform.localPosition = Vector3.zero;

            Light2D light2D = muzzleFlashLight.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Point;
            light2D.color = channelConfig.muzzleLightColor;
            light2D.intensity = channelConfig.muzzleLightIntensity;
            light2D.pointLightOuterRadius = channelConfig.muzzleLightRange;
        }

        // Play start sound
        if (channelConfig.channelStartSound != null)
        {
            AudioSource.PlayClipAtPoint(channelConfig.channelStartSound, transform.position);
        }

        // Play looping sound
        if (channelConfig.channelLoopSound != null)
        {
            if (channelLoopAudioSource == null)
            {
                GameObject audioObj = new GameObject("ChannelLoopAudio");
                audioObj.transform.SetParent(transform);
                audioObj.transform.localPosition = Vector3.zero;
                channelLoopAudioSource = audioObj.AddComponent<AudioSource>();
            }

            channelLoopAudioSource.clip = channelConfig.channelLoopSound;
            channelLoopAudioSource.loop = true;
            channelLoopAudioSource.Play();
        }

        // After start animation, transition to channel loop animation
        if (weaponAnimator != null && !string.IsNullOrEmpty(channelConfig.channelAnimationName))
        {
            Debug.Log($"[ChannelAbility] Scheduling loop animation '{channelConfig.channelAnimationName}' in 0.1s");
            // Use a short delay to let start animation play
            Invoke(nameof(PlayChannelLoopAnimation), 0.1f);
        }

        Debug.Log($"<color=cyan>[ChannelAbility] ===== StartChannel() COMPLETE =====</color>");
    }

    /// <summary>
    /// Sets up references from a newly spawned channel object (collider, particles, contact filter).
    /// Called immediately after local instantiation (single-player / server host) OR asynchronously
    /// via TargetRpc after the server has spawned the object and sent the reference back to the owner client.
    /// </summary>
    public void SetChannelObjectReferences(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogError("[ChannelAbility] SetChannelObjectReferences called with null object!");
            return;
        }

        channelObject = obj;
        Debug.Log($"<color=green>[ChannelAbility] \u2713 Channel object references set: {channelObject.name}, scale: {channelConfig?.scale}</color>");

        // Find collider — look for "Hitbox" child first, then fall back to any Collider2D in children
        Transform hitboxTransform = channelObject.transform.Find("Hitbox");
        Debug.Log($"[ChannelAbility] Looking for Hitbox child: {hitboxTransform != null}");

        if (hitboxTransform != null)
        {
            channelCollider = hitboxTransform.GetComponent<Collider2D>();
            Debug.Log($"[ChannelAbility] Hitbox collider found: {channelCollider != null} (Type: {channelCollider?.GetType().Name})");
        }
        else
        {
            channelCollider = channelObject.GetComponentInChildren<Collider2D>();
            Debug.Log($"[ChannelAbility] Searching for any Collider2D in children: {channelCollider != null}");
        }

        channelParticles = channelObject.GetComponentInChildren<ParticleSystem>();
        Debug.Log($"[ChannelAbility] Particle system found: {channelParticles != null}");

        if (channelCollider != null)
        {
            channelCollider.enabled = true;
            contactFilter = new ContactFilter2D();
            contactFilter.useTriggers = true;
            contactFilter.useLayerMask = true;
            contactFilter.layerMask = channelConfig.hitLayers;
            Debug.Log($"[ChannelAbility] Collider setup complete - Type: {channelCollider.GetType().Name}, Enabled: {channelCollider.enabled}");
        }
        else
        {
            Debug.LogWarning("[ChannelAbility] No collider found on channel object! Damage detection will not work.");
        }

        if (channelParticles != null)
        {
            Debug.Log($"[ChannelAbility] Calling ParticleSystem.Play() on {channelParticles.gameObject.name}...");
            channelParticles.Play();
            Debug.Log($"<color=green>[ChannelAbility] \u2713 Particle system Play() called - IsPlaying: {channelParticles.isPlaying}</color>");
        }
        else
        {
            Debug.LogWarning($"[ChannelAbility] No ParticleSystem found in channel object '{channelObject.name}'!");
        }
    }

    private void PlayChannelLoopAnimation()
    {
        Debug.Log($"[ChannelAbility] PlayChannelLoopAnimation() called - IsChanneling: {isChanneling}");

        bool hasValidAnimator = false;
        try
        {
            hasValidAnimator = weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null;
        }
        catch (MissingReferenceException)
        {
            Debug.LogWarning("[ChannelAbility] weaponAnimator reference was destroyed.");
            weaponAnimator = null;
        }

        if (hasValidAnimator && isChanneling && !string.IsNullOrEmpty(channelConfig.channelAnimationName))
        {
            int stateHash = Animator.StringToHash(channelConfig.channelAnimationName);
            bool hasState = weaponAnimator.HasState(0, stateHash);
            Debug.Log($"[ChannelAbility] Loop animation '{channelConfig.channelAnimationName}' - Hash: {stateHash}, HasState: {hasState}");

            if (hasState)
            {
                weaponAnimator.Play(channelConfig.channelAnimationName);
                Debug.Log($"<color=green>[ChannelAbility] ✓ Playing loop animation '{channelConfig.channelAnimationName}'</color>");
            }
            else
            {
                Debug.LogWarning($"[ChannelAbility] Animation state '{channelConfig.channelAnimationName}' not found in weapon animator.");
            }
        }
        else
        {
            Debug.LogWarning($"[ChannelAbility] Cannot play loop animation - Animator: {weaponAnimator != null}, Controller: {weaponAnimator?.runtimeAnimatorController != null}, IsChanneling: {isChanneling}, AnimName: '{channelConfig?.channelAnimationName}'");
        }
    }

    private void StopChannel()
    {
        Debug.Log($"<color=orange>[ChannelAbility] ===== StopChannel() CALLED =====</color>");

        if (!isChanneling)
        {
            Debug.Log("[ChannelAbility] Not channeling, ignoring StopChannel call");
            return;
        }

        isChanneling = false;
        Debug.Log("[ChannelAbility] Stopping channel - cleaning up objects and playing end animation");

        // Play end animation
        bool hasValidAnimator = false;
        try
        {
            hasValidAnimator = weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null;
        }
        catch (MissingReferenceException)
        {
            Debug.LogWarning("[ChannelAbility] weaponAnimator reference was destroyed in StopChannel.");
            weaponAnimator = null;
        }

        if (hasValidAnimator && !string.IsNullOrEmpty(channelConfig.channelEndAnimationName))
        {
            int stateHash = Animator.StringToHash(channelConfig.channelEndAnimationName);
            if (weaponAnimator.HasState(0, stateHash))
            {
                weaponAnimator.Play(channelConfig.channelEndAnimationName);
                
                // Schedule return to idle after end animation completes
                if (!string.IsNullOrEmpty(channelConfig.weaponIdleAnimationName))
                {
                    StartCoroutine(ReturnWeaponToIdle());
                }
            }
            else
            {
                Debug.LogWarning($"[ChannelAbility] Animation state '{channelConfig.channelEndAnimationName}' not found in weapon animator.");
            }
        }
        else if (hasValidAnimator && !string.IsNullOrEmpty(channelConfig.weaponIdleAnimationName))
        {
            // If no end animation, go straight to idle
            weaponAnimator.Play(channelConfig.weaponIdleAnimationName, 0, 0f);
            Debug.Log($"[ChannelAbility] No end animation, returning directly to idle: {channelConfig.weaponIdleAnimationName}");
        }

        // Destroy channel object
        if (channelObject != null)
        {
            // Detach particles so they can finish in world space
            if (channelParticles != null)
            {
                channelParticles.transform.SetParent(null);
                var emission = channelParticles.emission;
                emission.enabled = false; // Stop new particles from spawning
                channelParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                // Destroy after particles finish (use max lifetime + duration)
                var main = channelParticles.main;
                float maxLifetime = main.startLifetime.constantMax + main.duration;
                Destroy(channelParticles.gameObject, maxLifetime);

                Debug.Log($"[ChannelAbility] Detached particles from channel object - will destroy in {maxLifetime}s");
            }

            // Network-despawn if this is a FishNet-spawned channel object; otherwise destroy locally
            NetworkObject netObj = channelObject.GetComponent<NetworkObject>();
            var nm = InstanceFinder.NetworkManager;
            if (netObj != null && netObj.IsSpawned && nm != null && nm.IsServerStarted)
            {
                nm.ServerManager.Despawn(channelObject);
            }
            else
            {
                Destroy(channelObject);
            }
            channelObject = null;
            channelParticles = null;
        }

        // Clean up muzzle flash
        if (muzzleFlash != null)
        {
            // Detach from parent so it finishes in world space
            muzzleFlash.transform.SetParent(null);

            var emission = muzzleFlash.emission;
            emission.enabled = false; // Stop new particles
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            // Destroy after particles finish
            var main = muzzleFlash.main;
            float maxLifetime = main.startLifetime.constantMax + main.duration;
            Destroy(muzzleFlash.gameObject, Mathf.Max(maxLifetime, 2f));

            Debug.Log($"[ChannelAbility] Detached muzzle flash - will destroy in {Mathf.Max(maxLifetime, 2f)}s");
            muzzleFlash = null;
        }

        // Clean up light
        if (muzzleFlashLight != null)
        {
            Destroy(muzzleFlashLight);
            muzzleFlashLight = null;
        }

        // Stop loop sound
        if (channelLoopAudioSource != null)
        {
            channelLoopAudioSource.Stop();
        }

        // Play end sound
        if (channelConfig.channelEndSound != null)
        {
            AudioSource.PlayClipAtPoint(channelConfig.channelEndSound, transform.position);
        }

        // Restore original weapon config settings
        if (hasOverriddenWeaponConfig)
        {
            RestoreWeaponConfig();
        }

        enemyHitTimers.Clear();
    }

    private void UpdateDamage()
    {
        if (channelCollider == null)
        {
            Debug.LogWarning("[ChannelAbility:Damage] channelCollider is NULL — damage detection impossible");
            return;
        }

        damageTimer += Time.deltaTime;

        if (damageTimer >= channelConfig.damageTickRate)
        {
            // --- Collider state ---
            Debug.Log($"[ChannelAbility:Damage] TICK — collider: {channelCollider.gameObject.name} "
                + $"| enabled: {channelCollider.enabled} "
                + $"| isTrigger: {channelCollider.isTrigger} "
                + $"| layer: {LayerMask.LayerToName(channelCollider.gameObject.layer)} ({channelCollider.gameObject.layer}) "
                + $"| pos: {channelCollider.transform.position}");

            // --- ContactFilter state ---
            System.Text.StringBuilder layerNames = new System.Text.StringBuilder();
            for (int l = 0; l < 32; l++)
            {
                if ((contactFilter.layerMask.value & (1 << l)) != 0)
                {
                    string n = LayerMask.LayerToName(l);
                    if (!string.IsNullOrEmpty(n)) layerNames.Append(n).Append(", ");
                }
            }
            Debug.Log($"[ChannelAbility:Damage] ContactFilter — useTriggers: {contactFilter.useTriggers} "
                + $"| useLayerMask: {contactFilter.useLayerMask} "
                    + $"| layerMask (raw): {contactFilter.layerMask.value} "
                + $"| layers: [{layerNames.ToString().TrimEnd(',', ' ')}]");

            hitResults.Clear();
            int hitCount = channelCollider.Overlap(contactFilter, hitResults);

            Debug.Log($"[ChannelAbility:Damage] Overlap returned {hitCount} result(s)");

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = hitResults[i];
                if (col == null) { Debug.LogWarning($"[ChannelAbility:Damage] hitResults[{i}] is null"); continue; }

                Debug.Log($"[ChannelAbility:Damage] Hit[{i}]: '{col.gameObject.name}' "
                    + $"| layer: {LayerMask.LayerToName(col.gameObject.layer)} ({col.gameObject.layer}) "
                    + $"| isTrigger: {col.isTrigger}");

                Enemy enemy = col.GetComponent<Enemy>();
                if (enemy == null)
                    enemy = col.GetComponentInParent<Enemy>();

                if (enemy != null)
                {
                    Debug.Log($"[ChannelAbility:Damage] → Enemy found: '{enemy.gameObject.name}', applying damage");
                    ApplyDamageToEnemy(enemy);
                }
                else
                {
                    IDamageable dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                    Debug.LogWarning($"[ChannelAbility:Damage] Hit '{col.gameObject.name}' has no Enemy component. "
                        + $"IDamageable present: {dmg != null}");
                }
            }

            damageTimer = 0f;
        }
    }

    private void ApplyDamageToEnemy(Enemy enemy)
    {
        if (enemy == null) return;

        DamageContext damageContext = DamageCalculator.CalculateDamageWithTraitEffects(
            channelConfig.damage,
            channelConfig.damageType,
            parentConfig?.abilityName,
            parentConfig?.abilityTags?.GetAllTags(),
            gameObject,
            enemy.gameObject,
            enemy.transform.position,
            parentConfig
        );
        float finalDamage = damageContext.FinalDamage;

        Debug.Log($"[ChannelAbility:Damage] ApplyDamageToEnemy '{enemy.gameObject.name}' "
            + $"| base: {channelConfig.damage} | final: {finalDamage} "
            + $"| type: '{channelConfig.damageType}'");

        // Apply damage with color flash (TakeDamage handles flash internally)
        // Pass channel owner as attacker for thorns/reflect damage
        enemy.TakeDamage(finalDamage, channelConfig.damageType, enemy.transform.position, channelConfig.hitFlashColor, gameObject, damageContext.CritMultiplier);

        PlayerController attackerPlayer = gameObject.GetComponent<PlayerController>();
        attackerPlayer?.NotifyAttackDamage(parentConfig, enemy.gameObject, finalDamage, channelConfig.damageType);

        // Life steal — gameObject is the player (ChannelAbility is a player component)
        LifeStealProcessor.Apply(channelConfig.lifeSteal, finalDamage, gameObject);

        // Centralized hit visual from AbilityDataConfig
        HitVisualHelper.SpawnHitVisual(parentConfig, enemy.transform.position, enemy.gameObject);

        Debug.Log($"[ChannelAbility:Damage] TakeDamage called on '{enemy.gameObject.name}'");

        // Apply status effects using EffectData system
        if (channelConfig.onHitEffects != null)
        {
            channelConfig.onHitEffects.ApplyEffects(enemy.gameObject, gameObject);
        }
    }

    private void OnDestroy()
    {
        // Clean up if destroyed while channeling
        if (isChanneling)
        {
            StopChannel();
        }

        if (channelLoopAudioSource != null)
        {
            Destroy(channelLoopAudioSource.gameObject);
        }
    }

    /// <summary>
    /// Temporarily override weapon config to enable orbital rotation.
    /// PlayerController's UpdateActiveAimingWeapon will handle the actual rotation logic.
    /// </summary>
    private void ApplyWeaponConfigOverrides()
    {
        CharacterData characterData = playerController.GetCurrentCharacterData();
        if (characterData?.mainHandWeaponConfig == null)
        {
            Debug.LogWarning("[ChannelAbility] Cannot apply weapon overrides - no weapon config found.");
            return;
        }

        originalWeaponConfig = characterData.mainHandWeaponConfig;
        originalAimingRadius = originalWeaponConfig.aimingRadius;
        originalFlipWeaponOnYAxis = originalWeaponConfig.flipWeaponOnYAxis;
        originalLockTo2Directions = originalWeaponConfig.lockTo2Directions;
        originalOverridePositioning = originalWeaponConfig.overridePositioning;

        // Copy current resolved values into the override so we modify a local copy
        var pos = originalWeaponConfig.Positioning;
        originalWeaponConfig.positioningOverride.aimingRadius = channelConfig.orbitalRadius;
        originalWeaponConfig.positioningOverride.flipWeaponOnYAxis = channelConfig.flipWeaponOnYAxis;
        originalWeaponConfig.positioningOverride.lockTo2Directions = false; // Enable 360° rotation
        originalWeaponConfig.overridePositioning = true;

        // Reset weapon scale to explicit (1,1,z) before PlayerController applies Y-flip for channeling.
        // Uses explicit 1f instead of Abs(current) to prevent Y-scale drift from external systems.
        if (weaponTransform != null)
        {
            weaponTransform.localScale = new Vector3(1f, 1f, weaponTransform.localScale.z);
            Debug.Log($"[ChannelAbility] Reset weapon scale to positive at channel start");
        }

        hasOverriddenWeaponConfig = true;

        Debug.Log($"[ChannelAbility] Applied weapon config overrides: radius={channelConfig.orbitalRadius}, flipY={channelConfig.flipWeaponOnYAxis}");
    }

    /// <summary>
    /// Restore original weapon config settings after channeling ends.
    /// </summary>
    private void RestoreWeaponConfig()
    {
        if (originalWeaponConfig != null)
        {
            originalWeaponConfig.positioningOverride.aimingRadius = originalAimingRadius;
            originalWeaponConfig.positioningOverride.flipWeaponOnYAxis = originalFlipWeaponOnYAxis;
            originalWeaponConfig.positioningOverride.lockTo2Directions = originalLockTo2Directions;
            originalWeaponConfig.overridePositioning = originalOverridePositioning;

            Debug.Log($"[ChannelAbility] Restored original weapon config: radius={originalAimingRadius}, flipY={originalFlipWeaponOnYAxis}, lock2Dir={originalLockTo2Directions}");
        }

        // Reset weapon scale to explicit (1,1,z) — removes any flips from channeling.
        // Uses explicit 1f instead of Abs(current) to prevent Y-scale drift.
        if (weaponTransform != null)
        {
            weaponTransform.localScale = new Vector3(1f, 1f, weaponTransform.localScale.z);
            Debug.Log($"[ChannelAbility] Reset weapon scale to positive");
        }

        hasOverriddenWeaponConfig = false;
        originalWeaponConfig = null;
    }

    /// <summary>
    /// Return weapon to idle animation after channel end animation completes
    /// </summary>
    private System.Collections.IEnumerator ReturnWeaponToIdle()
    {
        if (weaponAnimator == null) yield break;

        // Wait a frame for the end animation to start
        yield return null;

        // Get the current animation clip info
        AnimatorClipInfo[] clipInfo = weaponAnimator.GetCurrentAnimatorClipInfo(0);
        if (clipInfo.Length > 0)
        {
            // Get the animation length
            float animationLength = clipInfo[0].clip.length;

            // Wait for animation to complete
            yield return new WaitForSeconds(animationLength);
        }
        else
        {
            // Fallback: wait a default time if we can't get clip info
            yield return new WaitForSeconds(0.3f);
        }

        // Return weapon to idle animation
        if (weaponAnimator != null && !string.IsNullOrEmpty(channelConfig.weaponIdleAnimationName))
        {
            weaponAnimator.Play(channelConfig.weaponIdleAnimationName, 0, 0f);
            Debug.Log($"[ChannelAbility] Returned weapon to idle animation: {channelConfig.weaponIdleAnimationName}");
        }
    }

    /// <summary>
    /// Update channel object with hybrid transform tracking.
    /// Position follows LaunchZone (tracks sprite animations),
    /// Rotation follows weapon root (tracks player aiming).
    /// This allows channeled effects to stick to animated sprites while rotating with aim.
    /// </summary>
    private void UpdateChannelObjectPosition()
    {
        if (channelObject == null)
        {
            Debug.LogWarning("[ChannelAbility] UpdateChannelObjectPosition: channelObject is null!");
            return;
        }

        if (launchZone != null && weaponTransform != null)
        {
            // Position: Follow LaunchZone (sprite animation moves this)
            channelObject.transform.position = launchZone.position;

            // Rotation: Follow weapon root (PlayerController rotates this based on aiming)
            channelObject.transform.rotation = weaponTransform.rotation;
        }
    }
}
