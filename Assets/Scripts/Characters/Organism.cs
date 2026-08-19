using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet;
using FishNet.Object.Synchronizing;
public abstract class Organism : NetworkBehaviour, IDamageable, IDamageFloaterSource
{
    [Header("Basic Properties")]
    [SerializeField] protected bool isTangible = true;
    [SerializeField] protected bool isAlive = true;

    [Header("Stat System")]
    [Tooltip("Unified stat container with all stats organized by category")]
    [SerializeField] protected StatContainer statContainer = new StatContainer();

    [Header("Visual Feedback")]
    [SerializeField] protected bool enableDamageFlash = true;
    [SerializeField] protected Color damageFlashColor = Color.white;
    [SerializeField] protected float damageFlashDuration = 0.2f;
    [SerializeField] protected int damageFlashCount = 2;

    [Header("Damage Type Registry")]
    [SerializeField] protected List<DamageTypeData> damageTypeRegistry = new List<DamageTypeData>();

    protected Rigidbody2D rb;
    protected List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    protected Collider2D col;

    protected List<Color> originalColors = new List<Color>();
    protected Coroutine damageFlashCoroutine;
    private List<Material> originalMaterials = new List<Material>();
    private Material damageFlashMaterial;
    private Vector3 _baselineScale = Vector3.one; // Stored once to prevent squash/stretch stacking
    
    // Cached network manager reference to avoid repeated lookups
    protected FishNet.Managing.NetworkManager _cachedNetworkManager;
    
    // Helper property to check if networking is active
    protected bool IsNetworkActive => _cachedNetworkManager != null &&
        (_cachedNetworkManager.IsServerStarted || _cachedNetworkManager.IsClientStarted) &&
        NetworkObject != null;

    // Force field regeneration (built-in, no component needed)
    [Header("Force Field Regeneration")]
    [SerializeField] protected float forceFieldRegenDelay = 3f; // Time without damage before regen starts
    [SerializeField] protected float forceFieldRegenDuration = 2f; // Time to fully regenerate
    protected float timeSinceLastForceFieldDamage = 0f;
    protected bool isRegeneratingForceField = false;

    public static event Action<Organism> OnOrganismDeath;
    public static event Action<Organism, float> OnHealthChanged;
    public static event Action<Organism, float> OnEnergyChanged;
    public static event Action<Organism, float> OnEnergySpent;
    public static event Action<Organism, float> OnForceFieldChanged;
    
    // Event invoked when this organism takes damage (for reactive effects like Thorns)
    // Parameters: (victim, damage, damageTypeName, attackerPosition, attackerObject)
    public event Action<Organism, float, string, Vector3, GameObject> OnDamageTaken;
     // Evade/Block system - invulnerability during dash/dodge
    protected bool _isEvading = false;
    public event Action<IDamageable, float, string, Vector3, GameObject> OnEvade;
    public event Action<IDamageable, float, string, Vector3, GameObject> OnBlock;

    // Static event fired from the attacker's perspective after damage lands.
    // Parameters: (attacker, finalDamage, damageTypeName, victim)
    public static event Action<GameObject, float, string, GameObject> OnDamageDealt;
    
    
   
    

    public float MoveSpeed => (statContainer != null && statContainer.HasStat("MoveSpeed"))
        ? statContainer.GetStat("MoveSpeed")
        : 5f;
    public bool IsTangible => isTangible;
    public StatContainer AllStats => statContainer;
    public virtual bool IsAlive => isAlive;
    public bool IsEvading => _isEvading;
    
    public void SetEvading(bool evading)
    {
        _isEvading = evading;
        Debug.Log($"[Organism] {gameObject.name} evading = {evading}");
    }



    private readonly SyncVar<float> _syncCurrentHealth = new SyncVar<float>();
    private readonly SyncVar<float> _syncCurrentEnergy = new SyncVar<float>();
    private readonly SyncVar<float> _syncCurrentForceField = new SyncVar<float>();

    public float CurrentHealth => _syncCurrentHealth.Value;
    public float MaxHealth => statContainer?.GetStat("MaxHealth") ?? 100f;
    public float CurrentEnergy => _syncCurrentEnergy.Value;
    public float MaxEnergy => statContainer?.GetStat("MaxEnergy") ?? 100f;
    public float CurrentForceField => _syncCurrentForceField.Value;
    public float MaxForceField => statContainer?.GetStat("ForceField") ?? 0f;

    public void RefreshMoveSpeedFromStats()
    {
        // MoveSpeed is stat-driven via the MoveSpeed getter.
        // This method remains for compatibility with existing callers.
    }

    protected virtual void Awake()
    {
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        _baselineScale = transform.localScale; // Store once to prevent squash/stretch stacking
        
        // Find all sprite renderers (exclude shadows or include based on name)
        SpriteRenderer[] foundRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in foundRenderers)
        {
            // Skip shadow renderers from damage flash (they shouldn't flash)
            if (sr.gameObject.name.ToLower().Contains("shadow")) continue;
            
            spriteRenderers.Add(sr);
            originalColors.Add(sr.color);
            originalMaterials.Add(sr.material);
            Debug.Log($"[Organism] Found SpriteRenderer on {gameObject.name}: {sr.gameObject.name}");
        }
        
        if (spriteRenderers.Count > 0)
        {
            Debug.Log($"[Organism] Registered {spriteRenderers.Count} sprite renderers for damage flash on {gameObject.name}");
        }
        
        // Load DamageFlash material from Resources (do this regardless of initial sprite count)
        damageFlashMaterial = Resources.Load<Material>("Materials/DamageFlash");
        if (damageFlashMaterial == null)
        {
            Debug.LogWarning($"[Organism] DamageFlash material not found in Resources/Materials/ for {gameObject.name}");
        }

        // Initialize stat container from database
        if (statContainer != null)
        {
            statContainer.InitializeFromDatabase();

            // Set base crit stats for all characters
            if (statContainer.HasStat("CritChance"))
            {
                statContainer.SetStat("CritChance", 0f); // 0.0 = no bonus to base crit
            }
            if (statContainer.HasStat("CritDamage"))
            {
                statContainer.SetStat("CritDamage", 1.5f); // 150% base crit damage (1.5x multiplier)
            }

            Debug.Log($"[Organism] {gameObject.name} initialized with {statContainer.GetAllStats().Count} stats from database");
        }

        // Initialize current health, energy, and force field from max values
        _syncCurrentHealth.Value = MaxHealth > 0 ? MaxHealth : 100f;
        _syncCurrentEnergy.Value = MaxEnergy > 0 ? MaxEnergy : 100f;
        _syncCurrentForceField.Value = MaxForceField;

        isAlive = true;
    }

    /// <summary>
    /// Re-scan for all sprite renderers in children. Call this after dynamically spawning
    /// visual elements (e.g., gear pieces on player characters) so damage flash affects them.
    /// </summary>
    public virtual void RefreshSpriteRenderers()
    {
        // Clear existing lists
        spriteRenderers.Clear();
        originalColors.Clear();
        originalMaterials.Clear();
        
        // Find all sprite renderers in children
        SpriteRenderer[] foundRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in foundRenderers)
        {
            // Skip shadow renderers from damage flash (they shouldn't flash)
            if (sr.gameObject.name.ToLower().Contains("shadow")) continue;
            
            spriteRenderers.Add(sr);
            originalColors.Add(sr.color);
            originalMaterials.Add(sr.material);
        }
        
        Debug.Log($"[Organism] RefreshSpriteRenderers on {gameObject.name}: found {spriteRenderers.Count} renderers (excluding shadows)");
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        // Cache NetworkManager reference
        _cachedNetworkManager = InstanceFinder.NetworkManager;
        
        // Subscribe to SyncVar changes
        _syncCurrentHealth.OnChange += OnHealthSync;
        _syncCurrentEnergy.OnChange += OnEnergySync;
        _syncCurrentForceField.OnChange += OnForceFieldSync;
    }

    protected virtual void Start()
    {
        // Cache NetworkManager if not already cached (for single-player mode)
        if (_cachedNetworkManager == null)
        {
            _cachedNetworkManager = InstanceFinder.NetworkManager;
        }
        
        SetupPhysics();
    }

    protected virtual void Update()
    {
        if (!isAlive) return;

        // Apply health and energy regeneration
        ApplyRegeneration();

        // Apply force field regeneration
        ApplyForceFieldRegeneration();

        HandleUpdate();
    }

    private void OnHealthSync(float prev, float next, bool asServer)
    {
        if (!asServer) // Only trigger on clients
        {
            OnHealthChanged?.Invoke(this, next);
        }
    }

    private void OnEnergySync(float prev, float next, bool asServer)
    {
        if (!asServer)
        {
            OnEnergyChanged?.Invoke(this, next);
        }
    }

    private void OnForceFieldSync(float prev, float next, bool asServer)
    {
        if (!asServer)
        {
            OnForceFieldChanged?.Invoke(this, next);
        }
    }


    protected virtual void SetupPhysics()
    {
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        if (col != null && !isTangible)
        {
            col.isTrigger = true;
        }
    }

    protected abstract void HandleUpdate();

    /// <summary>
    /// Apply health and energy regeneration from stat container
    /// </summary>
    protected virtual void ApplyRegeneration()
    {
        if (statContainer == null) return;

        // Apply health regeneration
        float healthRegen = statContainer.HasStat("HealthRegen") ? statContainer.GetStat("HealthRegen") : 0f;
        if (healthRegen > 0f && _syncCurrentHealth.Value < MaxHealth)
        {
            float healthToRegen = healthRegen * Time.deltaTime;
            ModifyHealth(healthToRegen);
        }

        // Apply energy regeneration
        float energyRegen = statContainer.HasStat("EnergyRegen") ? statContainer.GetStat("EnergyRegen") : 0f;
        if (energyRegen > 0f && _syncCurrentEnergy.Value < MaxEnergy)
        {
            float energyToRegen = energyRegen * Time.deltaTime;
            ModifyEnergy(energyToRegen);
        }
    }

    /// <summary>
    /// Apply force field regeneration (built-in to Organism)
    /// Force field regenerates after not taking damage for forceFieldRegenDelay seconds
    /// </summary>
    protected virtual void ApplyForceFieldRegeneration()
    {
        if (MaxForceField <= 0f) return; // No force field stat

        // If force field is already full, no need to regenerate
        if (_syncCurrentForceField.Value >= MaxForceField)
        {
            isRegeneratingForceField = false;
            return;
        }

        // Increment time since last damage
        timeSinceLastForceFieldDamage += Time.deltaTime;

        // Start regenerating after delay
        if (timeSinceLastForceFieldDamage >= forceFieldRegenDelay)
        {
            if (!isRegeneratingForceField)
            {
                isRegeneratingForceField = true;
                Debug.Log($"[Organism] Starting force field regeneration for {gameObject.name}");
            }

            // Regenerate over duration
            float regenRate = MaxForceField / forceFieldRegenDuration;
            float regenThisFrame = regenRate * Time.deltaTime;
            ModifyForceField(regenThisFrame);

            // Stop regenerating when full
            if (_syncCurrentForceField.Value >= MaxForceField)
            {
                isRegeneratingForceField = false;
                Debug.Log($"[Organism] Force field fully regenerated for {gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Heal the organism by a positive amount
    /// </summary>
    public virtual void Heal(float amount)
    {
        if (amount <= 0f) return;

        float before = _syncCurrentHealth.Value;
        ModifyHealth(Mathf.Abs(amount));
        float healed = _syncCurrentHealth.Value - before;
        if (healed <= 0f) return;

        if (IsNetworkActive)
        {
            if (IsServerInitialized)
                ShowHealingFloaterObserversRpc(healed);
        }
        else if (DamageFloaterManager.Instance != null)
        {
            DamageFloaterManager.Instance.ShowHealing(transform.position, healed, transform);
        }
    }
    
    public virtual void ModifyHealth(float amount)
    {
        // Only server can modify health in networked games
        // In single-player (no network), allow modification
        if (IsNetworkActive && (!IsServerInitialized || !IsSpawned)) return;
        
        float oldHealth = _syncCurrentHealth.Value;
        _syncCurrentHealth.Value = Mathf.Clamp(_syncCurrentHealth.Value + amount, 0f, MaxHealth);

        bool willDie = _syncCurrentHealth.Value <= 0 && isAlive;
        Debug.Log($"[MeleeAbility][KillTrace] ModifyHealth on {gameObject.name}: {oldHealth:F1} -> {_syncCurrentHealth.Value:F1} " +
            $"(amount={amount:F1}, willDie={willDie}, isServerStarted={IsServerStarted})");

        // SyncVar will automatically sync to clients in networked mode
        // Event triggers on clients via OnHealthSync callback
        OnHealthChanged?.Invoke(this, _syncCurrentHealth.Value);

        if (willDie)
        {
            Die();
        }
    }

    public virtual void ModifyForceField(float amount)
    {
        // Only server can modify in networked games; allow in single-player
        if (IsNetworkActive && (!IsServerInitialized || !IsSpawned)) return;
        
        _syncCurrentForceField.Value = Mathf.Clamp(_syncCurrentForceField.Value + amount, 0f, MaxForceField);
        OnForceFieldChanged?.Invoke(this, _syncCurrentForceField.Value);
    }

    /// <summary>
    /// Reinitialize force field to match new max value (call after traits change max force field)
    /// Only increases current force field, never decreases
    /// </summary>
    public virtual void ReinitializeForceField()
    {
        float newMax = MaxForceField;
        if (newMax > _syncCurrentForceField.Value)
        {
            Debug.Log($"[Organism] Force field max increased from {_syncCurrentForceField.Value} to {newMax}, reinitializing");
            _syncCurrentForceField.Value = newMax;
            OnForceFieldChanged?.Invoke(this, _syncCurrentForceField.Value);
        }
    }

    public virtual void ModifyEnergy(float amount)
    {
        // Only server can modify in networked games; allow in single-player
        if (IsNetworkActive && !IsServerInitialized) return;
        
        _syncCurrentEnergy.Value = Mathf.Clamp(_syncCurrentEnergy.Value + amount, 0f, MaxEnergy);
        OnEnergyChanged?.Invoke(this, _syncCurrentEnergy.Value);

        if (amount < 0f)
        {
            OnEnergySpent?.Invoke(this, -amount);
        }
    }

    #region IDamageable Implementation
    
    // Interface methods that intelligently route to networked or local damage
    public void TakeDamage(float damage, float critMultiplier = 1f)
    {
        if (IsNetworkActive)
        {
            TakeDamageServerRpc(damage, "Physical", critMultiplier);
        }
        else
        {
            // Single-player: call directly
            TakeDamageInternal(damage, "Physical", Color.white, critMultiplier);
        }
    }

    public void TakeDamage(float damage, string damageTypeName, float critMultiplier = 1f)
    {
        if (IsNetworkActive)
        {
            TakeDamageServerRpc(damage, damageTypeName, critMultiplier);
        }
        else
        {
            // Single-player: call directly
            TakeDamageInternal(damage, damageTypeName, Color.white, critMultiplier);
        }
    }

    public void TakeDamage(float damage, string damageTypeName, Vector3 attackerPosition, float critMultiplier = 1f)
    {
        if (IsNetworkActive)
        {
            TakeDamageServerRpc(damage, damageTypeName, attackerPosition, critMultiplier);
        }
        else
        {
            // Single-player: call directly
            TakeDamageInternal(damage, damageTypeName, attackerPosition, Color.white, critMultiplier);
        }
    }

    public void TakeDamage(float damage, string damageTypeName, bool suppressFloater, float critMultiplier = 1f)
    {
        if (IsNetworkActive)
        {
            // In networked mode, use ServerRpc with suppressFloater support
            TakeDamageServerRpc(damage, damageTypeName, suppressFloater, critMultiplier);
        }
        else
        {
            // Single-player: call directly with suppressFloater support
            TakeDamageInternal(damage, damageTypeName, Color.white, critMultiplier, suppressFloater);
        }
    }

    public void TakeDamage(float damage, string damageTypeName, Color flashColor, float critMultiplier = 1f)
    {
        if (IsNetworkActive)
        {
            TakeDamageServerRpc(damage, damageTypeName, critMultiplier);
        }
        else
        {
            // Single-player: call directly
            TakeDamageInternal(damage, damageTypeName, flashColor, critMultiplier);
        }
    }

    public void TakeDamage(float damage, string damageTypeName, Vector3 attackerPosition, Color flashColor, float critMultiplier = 1f)
    {
        if (IsNetworkActive)
        {
            TakeDamageServerRpc(damage, damageTypeName, attackerPosition, critMultiplier);
        }
        else
        {
            // Single-player: call directly
            TakeDamageInternal(damage, damageTypeName, attackerPosition, flashColor, critMultiplier);
        }
    }

    public void TakeDamage(float damage, string damageTypeName, Vector3 attackerPosition, Color flashColor, GameObject attacker, float critMultiplier = 1f)
    {
        if (IsNetworkActive)
        {
            if (IsServerInitialized)
            {
                // We're on the server - call directly to preserve attacker reference for thorns
                TakeDamageInternal(damage, damageTypeName, attackerPosition, flashColor, attacker, critMultiplier);
            }
            else
            {
                // Client needs to send to server via RPC (attacker ref cannot be sent over network)
                TakeDamageServerRpc(damage, damageTypeName, attackerPosition, critMultiplier);
            }
        }
        else
        {
            // Single-player: call directly with attacker reference
            TakeDamageInternal(damage, damageTypeName, attackerPosition, flashColor, attacker, critMultiplier);
        }
    }

    public void ShowDamageFloater(float damage, string damageTypeName)
    {
        // Show floater locally immediately (for instant feedback)
        if (DamageFloaterManager.Instance != null)
        {
            DamageFloaterManager.Instance.ShowDamage(transform.position, damage, damageTypeName, false, null, transform);
        }
    }
    
    #endregion
                                                            
    // Client calls this to request damage
    [ServerRpc(RequireOwnership = false)] // Anyone can call this
    public void TakeDamageServerRpc(float damage, string damageTypeName, float critMultiplier = 1f)
    {
        // This only runs on server
        TakeDamageInternal(damage, damageTypeName, Color.white, critMultiplier);
    }

    // Overload with attacker position
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage, string damageTypeName, Vector3 attackerPosition, float critMultiplier = 1f)
    {
        Debug.Log($"[MeleeAbility][KillTrace] TakeDamageServerRpc received on {gameObject.name} (server={IsServerStarted}) damage={damage:F1}, type={damageTypeName}");
        TakeDamageInternal(damage, damageTypeName, attackerPosition, Color.white, critMultiplier);
    }

    // Overload with suppressFloater support (for DoT effects)
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage, string damageTypeName, bool suppressFloater, float critMultiplier = 1f)
    {
        TakeDamageInternal(damage, damageTypeName, Color.white, critMultiplier, suppressFloater);
    }

    // Internal method that actually applies damage (server-only in multiplayer, local in single-player)
    private void TakeDamageInternal(float damage, string damageTypeName, Color flashColor, float critMultiplier = 1f, bool suppressFloater = false)
    {
        // In networked mode, require server authority; in single-player, allow damage
        if (IsNetworkActive && !IsServerInitialized) return;
        if (!isAlive || !isTangible) return;

        // Run is ending — don't apply damage (or its floater/flash/SyncVar network traffic)
        // while the server is tearing down the GameScene and loading CommandScene.
        
        // Evade check — if evading (dashing with i-frames), negate all damage
        if (_isEvading)
        {
            OnEvade?.Invoke(this, damage, damageTypeName, transform.position, null);
            ShowEvadeFloater();
            return;
        }

        // Roll dodge chance — if successful, show "Dodged!" and skip all damage
        if (RollDodge())
        {
            ShowDodgeFloater();
            return;
        }

        bool isCritical = critMultiplier > 1f;
        float finalDamage = CalculateDamage(damage, damageTypeName, critMultiplier);

        // Apply armor flat reduction before force field
        finalDamage = ApplyArmorReduction(finalDamage);
        
        // If armor blocked all damage, still invoke event for thorns, then show "Block" and return
        if (finalDamage <= 0f)
        {
            // Invoke OnDamageTaken even when blocked - thorns should still trigger
            OnDamageTaken?.Invoke(this, damage, damageTypeName, transform.position, null);
            
            if (!suppressFloater)
            {
                ShowBlockFloater();
            }
            return;
        }

        // Apply damage to force field first, then overflow to health
        float damageToHealth = ApplyDamageToForceField(finalDamage);
        if (damageToHealth > 0f)
        {
            ModifyHealth(-damageToHealth);
        }
        
        // Invoke OnDamageTaken event for reactive effects (e.g., Thorns)
        OnDamageTaken?.Invoke(this, finalDamage, damageTypeName, transform.position, null);
        
        // Show floater and flash - use RPC in networked mode, local in single-player
        // Only show if not suppressed (for smooth DoT damage application)
        if (!suppressFloater)
        {
            if (IsNetworkActive)
            {
                ShowDamageFloaterObserversRpc(finalDamage, damageTypeName, isCritical);
                TriggerDamageFlashObserversRpc(flashColor);
            }
            else
            {
                // Single-player: execute locally
                if (DamageFloaterManager.Instance != null)
                {
                    DamageFloaterManager.Instance.ShowDamage(transform.position, finalDamage, damageTypeName, isCritical, null, transform);
                }
                if (enableDamageFlash)
                {
                    TriggerDamageFlash(flashColor);
                }
            }
        }
        
        Debug.Log($"{gameObject.name} took {finalDamage:F1} {damageTypeName} damage! Health: {_syncCurrentHealth.Value}/{MaxHealth}");
    }

    // Overload with attacker position
    private void TakeDamageInternal(float damage, string damageTypeName, Vector3 attackerPosition, Color flashColor, float critMultiplier = 1f, bool suppressFloater = false)
    {
        // In networked mode, require server authority; in single-player, allow damage
        if (IsNetworkActive && !IsServerInitialized) return;
        if (!isAlive || !isTangible) return;

        // Run is ending — don't apply damage (or its floater/flash/SyncVar network traffic)
        // while the server is tearing down the GameScene and loading CommandScene.

        // Evade check — if evading (dashing with i-frames), negate all damage
        if (_isEvading)
        {
            OnEvade?.Invoke(this, damage, damageTypeName, transform.position, null);
            ShowEvadeFloater();
            return;
        }

        // Roll dodge chance — if successful, show "Dodged!" and skip all damage
        if (RollDodge())
        {
            ShowDodgeFloater();
            return;
        }

        bool isCritical = critMultiplier > 1f;
        float finalDamage = CalculateDamage(damage, damageTypeName, critMultiplier);

        // Apply armor flat reduction before force field
        finalDamage = ApplyArmorReduction(finalDamage);
        
        // If armor blocked all damage, still invoke event for thorns, then show "Block" and return
        if (finalDamage <= 0f)
        {
            // Invoke OnDamageTaken even when blocked - thorns should still trigger
            OnDamageTaken?.Invoke(this, damage, damageTypeName, attackerPosition, null);
            
            if (!suppressFloater)
            {
                ShowBlockFloater();
            }
            return;
        }

        float damageToHealth = ApplyDamageToForceField(finalDamage);
        if (damageToHealth > 0f)
        {
            ModifyHealth(-damageToHealth);
        }
        
        // Invoke OnDamageTaken event for reactive effects (e.g., Thorns)
        OnDamageTaken?.Invoke(this, finalDamage, damageTypeName, attackerPosition, null);
        
        // Show floater and flash - use RPC in networked mode, local in single-player
        // Only show if not suppressed (for smooth DoT damage application)
        if (!suppressFloater)
        {
            if (IsNetworkActive)
            {
                ShowDamageFloaterObserversRpc(transform.position, finalDamage, damageTypeName, isCritical, attackerPosition);
                TriggerDamageFlashObserversRpc(flashColor);
            }
            else
            {
                // Single-player: execute locally
                if (DamageFloaterManager.Instance != null)
                {
                    DamageFloaterManager.Instance.ShowDamage(transform.position, finalDamage, damageTypeName, isCritical, attackerPosition, transform);
                }
                if (enableDamageFlash)
                {
                    TriggerDamageFlash(flashColor);
                }
            }
        }
    }

    // Overload with attacker position AND attacker GameObject reference (for thorns/reflect)
    private void TakeDamageInternal(float damage, string damageTypeName, Vector3 attackerPosition, Color flashColor, GameObject attacker, float critMultiplier = 1f, bool suppressFloater = false)
    {
        // In networked mode, require server authority; in single-player, allow damage
        if (IsNetworkActive && !IsServerInitialized) return;
        if (!isAlive || !isTangible) return;

        // Run is ending — don't apply damage (or its floater/flash/SyncVar network traffic)
        // while the server is tearing down the GameScene and loading CommandScene.

        // Evade check — if evading (dashing with i-frames), negate all damage
        if (_isEvading)
        {
            OnEvade?.Invoke(this, damage, damageTypeName, transform.position, null);
            ShowEvadeFloater();
            return;
        }

        // Roll dodge chance — if successful, show "Dodged!" and skip all damage
        if (RollDodge())
        {
            ShowDodgeFloater();
            return;
        }

        // Apply attacker's stat-based damage bonuses (e.g. FireDamageBonus, generic Bonus).
        // This runs for every damage call that includes an attacker reference, so Area/Beam/Channel
        // abilities automatically benefit without needing to pre-calculate modifiers.
        if (attacker != null)
        {
            damage = DamageCalculator.CalculateFinalDamage(damage, damageTypeName, attacker);
        }

        bool isCritical = critMultiplier > 1f;
        float finalDamage = CalculateDamage(damage, damageTypeName, critMultiplier);

        // Apply armor flat reduction before force field
        finalDamage = ApplyArmorReduction(finalDamage);
        
        // If armor blocked all damage, still invoke event for thorns, then show "Block" and return
        if (finalDamage <= 0f)
        {
            // Invoke OnDamageTaken even when blocked - thorns should still trigger
            OnDamageTaken?.Invoke(this, damage, damageTypeName, attackerPosition, attacker);
            
            if (!suppressFloater)
            {
                ShowBlockFloater();
            }
            return;
        }

        float damageToHealth = ApplyDamageToForceField(finalDamage);
        if (damageToHealth > 0f)
        {
            ModifyHealth(-damageToHealth);
        }
        
        // Invoke OnDamageTaken event for reactive effects (e.g., Thorns)
        // Pass attacker reference directly so thorns can reflect damage without searching
        OnDamageTaken?.Invoke(this, finalDamage, damageTypeName, attackerPosition, attacker);

        // Notify attacker-side listeners (passive on-hit abilities)
        if (attacker != null)
            OnDamageDealt?.Invoke(attacker, finalDamage, damageTypeName, gameObject);
        
        // Show floater and flash - use RPC in networked mode, local in single-player
        // Only show if not suppressed (for smooth DoT damage application)
        if (!suppressFloater)
        {
            if (IsNetworkActive)
            {
                ShowDamageFloaterObserversRpc(transform.position, finalDamage, damageTypeName, isCritical, attackerPosition);
                TriggerDamageFlashObserversRpc(flashColor);
            }
            else
            {
                // Single-player: execute locally
                if (DamageFloaterManager.Instance != null)
                {
                    DamageFloaterManager.Instance.ShowDamage(transform.position, finalDamage, damageTypeName, isCritical, attackerPosition, transform);
                }
                if (enableDamageFlash)
                {
                    TriggerDamageFlash(flashColor);
                }
            }
        }
    }

    // Show floater on all clients
    [ObserversRpc]
    private void ShowDamageFloaterObserversRpc(float damage, string damageTypeName, bool isCritical)
    {
        if (DamageFloaterManager.Instance != null)
        {
            DamageFloaterManager.Instance.ShowDamage(transform.position, damage, damageTypeName, isCritical, null, transform);
        }
    }

    [ObserversRpc]
    private void ShowDamageFloaterObserversRpc(Vector3 position, float damage, string damageTypeName, bool isCritical, Vector3 attackerPosition)
    {
        if (DamageFloaterManager.Instance != null)
        {
            DamageFloaterManager.Instance.ShowDamage(position, damage, damageTypeName, isCritical, attackerPosition, transform);
        }
    }

    /// <summary>
    /// Finds a DataDrivenAbility on this Organism whose effective config matches abilityName.
    /// Used to resolve the shared MeleeConfig/AbilityDataConfig on remote clients so hitbox-based
    /// abilities (melee, area, explosion, projectiles) can replicate visuals without networking
    /// the config asset itself. Public so non-Organism NetworkBehaviours (e.g. Projectile) can
    /// resolve the owner's ability config the same way.
    /// </summary>
    public DataDrivenAbility FindDataDrivenAbilityByName(string abilityName)
    {
        if (string.IsNullOrEmpty(abilityName)) return null;

        DataDrivenAbility[] abilities = GetComponents<DataDrivenAbility>();
        foreach (DataDrivenAbility ability in abilities)
        {
            if (ability != null && ability.EffectiveAbilityConfig != null && ability.EffectiveAbilityConfig.abilityName == abilityName)
                return ability;
        }
        return null;
    }

    /// <summary>
    /// Replicates a melee hitbox's swing visual to observers. The instigating machine (server for
    /// enemies, owner for players) already shows its own local copy, so this is skipped there.
    /// </summary>
    [ObserversRpc]
    public void ObserversRpcSpawnMeleeSwingVisual(string abilityName, Vector3 spawnPos, float angle, bool firedFromOffhand)
    {
        if (InstanceFinder.IsServerStarted) return; // host already showed it locally
        if (IsOwner) return; // owner already showed it locally

        MeleeConfig meleeConfig = FindDataDrivenAbilityByName(abilityName)?.EffectiveAbilityConfig?.meleeConfig;
        if (meleeConfig == null || meleeConfig.hitbox.prefab == null) return;

        MeleeAbility.SpawnVisualOnly(meleeConfig, spawnPos, Quaternion.Euler(0f, 0f, angle));
    }

    /// <summary>
    /// Replicates a melee hit's particle/sound feedback to observers, mirroring RpcCreateHitEffect
    /// on Projectile. No owner-skip here (unlike the swing visual): the local melee copy on a
    /// non-server owner has its colliders disabled and never runs OnHitboxTriggerEnter itself, so
    /// the owner needs this RPC too in order to see their own hit feedback.
    /// </summary>
    [ObserversRpc]
    public void ObserversRpcSpawnMeleeHitFeedback(string abilityName, Vector3 position)
    {
        if (InstanceFinder.IsServerStarted) return; // host already showed it locally

        AbilityDataConfig abilityConfig = FindDataDrivenAbilityByName(abilityName)?.EffectiveAbilityConfig;
        if (abilityConfig == null) return;

        HitVisualHelper.SpawnHitVisual(abilityConfig, position);
    }

    [ObserversRpc]
    private void ShowHealingFloaterObserversRpc(float amount)
    {
        if (DamageFloaterManager.Instance != null)
        {
            DamageFloaterManager.Instance.ShowHealing(transform.position, amount, transform);
        }
    }

    // Trigger flash on all clients
    [ObserversRpc]
    private void TriggerDamageFlashObserversRpc(Color flashColor)
    {
        TriggerDamageFlash(flashColor);
    }

    /// <summary>
    /// Roll the organism's DodgeChance stat (0-1). Returns true if the attack is dodged.
    /// </summary>
    private bool RollDodge()
    {
        if (statContainer == null || !statContainer.HasStat("DodgeChance")) return false;
        float dodgeChance = statContainer.GetStat("DodgeChance");
        if (dodgeChance <= 0f) return false;
        return UnityEngine.Random.value < dodgeChance;
    }

    /// <summary>
    /// Apply flat armor reduction to damage. Returns the remaining damage after armor.
    /// Armor is subtracted directly from damage, minimum 0.
    /// </summary>
    private float ApplyArmorReduction(float damage)
    {
        if (statContainer == null || !statContainer.HasStat("Armor")) return damage;
        float armor = statContainer.GetStat("Armor");
        if (armor <= 0f) return damage;
        
        float reducedDamage = damage - armor;
        Debug.Log($"[Organism] Armor reduced damage: {damage:F1} - {armor:F1} armor = {reducedDamage:F1}");
        return Mathf.Max(0f, reducedDamage);
    }

    /// <summary>
    /// Show a "Block" floater on this organism, handling both networked and single-player.
    /// </summary>
    private void ShowBlockFloater()
    {
        if (IsNetworkActive)
        {
            ShowBlockTextObserversRpc();
        }
        else
        {
            ShowBlockTextLocal();
        }
    }

    [ObserversRpc]
    private void ShowBlockTextObserversRpc()
    {
        ShowBlockTextLocal();
    }

    private void ShowBlockTextLocal()
    {
        Debug.Log($"[Organism] ShowBlockTextLocal called on {gameObject.name}, DamageFloaterManager={(DamageFloaterManager.Instance != null ? "ready" : "NULL")}");
        if (DamageFloaterManager.Instance != null)
        {
            DamageFloaterManager.Instance.ShowText(transform.position, "Block", Color.gray, Vector2.up, transform);
        }
    }

    /// <summary>
    /// Show a "Dodged!" floater on this organism, handling both networked and single-player.
    /// </summary>
    private void ShowDodgeFloater()
    {
        if (IsNetworkActive)
        {
            ShowDodgeTextObserversRpc();
        }
        else
        {
            ShowDodgeTextLocal();
        }
    }
    
    /// <summary>
    /// Show an "Evade!" floater when damage is negated by dashing i-frames.
    /// </summary>
    private void ShowEvadeFloater()
    {
        if (IsNetworkActive)
        {
            ShowEvadeTextObserversRpc();
        }
        else
        {
            ShowEvadeTextLocal();
        }
    }
    
    [ObserversRpc]
    private void ShowEvadeTextObserversRpc()
    {
        ShowEvadeTextLocal();
    }
    
    private void ShowEvadeTextLocal()
    {
        if (DamageFloaterManager.Instance != null)
        {
            DamageFloaterManager.Instance.ShowText(transform.position, "Evade!", Color.cyan, Vector2.up, transform);
        }
    }

    [ObserversRpc]
    private void ShowDodgeTextObserversRpc()
    {
        ShowDodgeTextLocal();
    }

    private void ShowDodgeTextLocal()
    {
        if (DamageFloaterManager.Instance != null)
        {
            DamageFloaterManager.Instance.ShowText(transform.position, "Dodged!", Color.white, Vector2.up, transform);
        }
    }
    
    // Local version for both networked and single-player
    private void TriggerDamageFlash(Color flashColor)
    {
        Debug.Log($"[TriggerDamageFlash] Called on {gameObject.name}: enableDamageFlash={enableDamageFlash}, spriteRendererCount={spriteRenderers.Count}, flashColor={flashColor}");
        
        if (enableDamageFlash && spriteRenderers.Count > 0)
        {
            if (damageFlashCoroutine != null)
            {
                Debug.Log($"[TriggerDamageFlash] Stopping existing flash coroutine");
                StopCoroutine(damageFlashCoroutine);
                // Reset scale immediately to prevent stacking when coroutine is interrupted mid-squash
                transform.localScale = _baselineScale;
            }
            Debug.Log($"[TriggerDamageFlash] Starting DamageFlashCoroutine with color {flashColor} on {spriteRenderers.Count} renderers");
            damageFlashCoroutine = StartCoroutine(DamageFlashCoroutine(flashColor));
        }
        else
        {
            Debug.LogWarning($"[TriggerDamageFlash] Flash NOT triggered - enableDamageFlash={enableDamageFlash}, spriteRendererCount={spriteRenderers.Count}");
        }
    }

    /// <summary>
    /// Apply damage to force field first, return overflow damage that should be applied to health
    /// </summary>
    private float ApplyDamageToForceField(float damage)
{
    // Reset force field regeneration timer when taking damage
    timeSinceLastForceFieldDamage = 0f;
    isRegeneratingForceField = false;

    if (_syncCurrentForceField.Value <= 0f)
    {
        return damage; // No force field, all damage goes to health
    }

    if (_syncCurrentForceField.Value >= damage)
    {
        // Force field absorbs all damage
        ModifyForceField(-damage);
        return 0f;
    }
    else
    {
        // Force field absorbs partial damage, remainder goes to health
        float overflow = damage - _syncCurrentForceField.Value;
        ModifyForceField(-_syncCurrentForceField.Value); // Reduces to 0
        return overflow;
    }
}

protected virtual float CalculateDamage(float baseDamage, string damageTypeName, float critMultiplier = 1f)
{
    DamageTypeData damageType = damageTypeRegistry.Find(dt => dt.damageTypeName == damageTypeName);

    // NOTE: critMultiplier has already been applied by DamageCalculator before TakeDamage is called.
    // Applying it here again would double-multiply crit damage.  The parameter is kept in the
    // signature only so call-sites that pass it for the isCritical floater flag still compile.
    // Organism is responsible only for resistance and armor (handled by ApplyArmorReduction).
    float finalDamage = baseDamage;

    // Apply resistance based on damage type using StatContainer
    if (statContainer != null)
    {
        float resistance = 0f;

        // Check for specific resistance stat based on damage type name
        string resistanceStatName = $"{damageTypeName}Resistance";
        if (statContainer.HasStat(resistanceStatName))
        {
            resistance = statContainer.GetStat(resistanceStatName);
        }

        // Apply resistance (percentage reduction)
        // Resistance is stored as percentage (0.15 = 15% reduction)
        finalDamage *= (1f - resistance);
    }

    return Mathf.Max(0, finalDamage);
}

public virtual float GetCurrentHealth() => _syncCurrentHealth.Value;
public virtual float GetMaxHealth() => MaxHealth;

/// <summary>
/// Revive a dead organism: restore isAlive, reset health/energy to max.
/// Call before transitioning to CommandScene after death.
/// </summary>
public virtual void Revive()
{
    Debug.Log($"[DEATH-DIAG] [Organism.Revive] START for {gameObject.name} — isAlive={isAlive}, health={_syncCurrentHealth.Value}/{MaxHealth}");
    isAlive = true;
    _syncCurrentHealth.Value = MaxHealth > 0 ? MaxHealth : 100f;
    _syncCurrentEnergy.Value = MaxEnergy > 0 ? MaxEnergy : 100f;
    _syncCurrentForceField.Value = MaxForceField;
    Debug.Log($"[DEATH-DIAG] [Organism.Revive] COMPLETE for {gameObject.name} — isAlive={isAlive}, health={_syncCurrentHealth.Value}/{MaxHealth}");
}

protected virtual void Die()
{
    // In networked mode, only server handles death; in single-player, allow death
    if (IsNetworkActive && !IsServerInitialized) return;
    
    Debug.Log($"[DEATH-DIAG] [Organism.Die] {gameObject.name} died — health={_syncCurrentHealth.Value}, IsNetworkActive={IsNetworkActive}, IsServerInitialized={IsServerInitialized}");
    isAlive = false;
    
    // Notify clients of death - use RPC in networked mode, local in single-player
    if (IsNetworkActive)
    {
        Debug.Log($"[DEATH-DIAG] [Organism.Die] Calling OnDeathObserversRpc for {gameObject.name}");
        OnDeathObserversRpc();
    }
    else
    {
        // Single-player: execute locally
        Debug.Log($"[DEATH-DIAG] [Organism.Die] Single-player death, calling HandleDeath locally for {gameObject.name}");
        OnOrganismDeath?.Invoke(this);
        HandleDeath();
    }
}

[ObserversRpc]
private void OnDeathObserversRpc()
{
    Debug.Log($"[MeleeAbility][KillTrace] OnDeathObserversRpc executing on {gameObject.name} — isServerStarted={IsServerStarted}, isOwner={IsOwner}");
    OnOrganismDeath?.Invoke(this);
    HandleDeath();
}

protected abstract void HandleDeath();

public void SetTangible(bool tangible)
{
    isTangible = tangible;
    if (col != null)
    {
        col.isTrigger = !tangible;
    }
}

public float GetHealthPercentage()
{
    return MaxHealth > 0 ? _syncCurrentHealth.Value / MaxHealth : 0f;
}

public float GetEnergyPercentage()
{
    return MaxEnergy > 0 ? _syncCurrentEnergy.Value / MaxEnergy : 0f;
}

public float GetForceFieldPercentage()
{
    return MaxForceField > 0 ? _syncCurrentForceField.Value / MaxForceField : 0f;
}

protected virtual IEnumerator DamageFlashCoroutine(Color flashColor)
{
    Debug.Log($"[DamageFlashCoroutine] Started on {gameObject.name} with color {flashColor}, flashing {spriteRenderers.Count} renderers");
    
    if (spriteRenderers.Count == 0)
    {
        Debug.LogWarning($"[DamageFlashCoroutine] No sprite renderers, aborting");
        yield break;
    }

    // Use baseline scale for squash effect (prevents stacking if coroutine is interrupted)
    Vector3 squashScale = new Vector3(_baselineScale.x , _baselineScale.y , _baselineScale.z);

    // Use DamageFlash material if available
    if (damageFlashMaterial != null)
    {
        Debug.Log($"[DamageFlashCoroutine] Using DamageFlash material, flashing {damageFlashCount} times");
        // Set flash color on the material
        Material flashInstance = new Material(damageFlashMaterial);
        flashInstance.SetColor("_Color", flashColor);

        for (int i = 0; i < damageFlashCount; i++)
        {
            // Apply squash scale as an animation effect to enhance feedback (optional)
            //over time
            
            transform.localScale = squashScale;
            
            // Flash all sprite renderers
            foreach (var sr in spriteRenderers)
            {
                if (sr != null) sr.material = flashInstance;
            }
            yield return new WaitForSeconds(damageFlashDuration / 2f);

            // Restore baseline scale
            transform.localScale = _baselineScale;
            
            // Restore original materials
            for (int j = 0; j < spriteRenderers.Count; j++)
            {
                if (spriteRenderers[j] != null && j < originalMaterials.Count)
                    spriteRenderers[j].material = originalMaterials[j];
            }
            yield return new WaitForSeconds(damageFlashDuration / 2f);
        }

        Destroy(flashInstance); // Clean up material instance
    }
    else
    {
        Debug.Log($"[DamageFlashCoroutine] No DamageFlash material, using color-based flash fallback");
        // Fallback: color-based flash
        for (int i = 0; i < damageFlashCount; i++)
        {
            // Apply squash scale
            transform.localScale = squashScale;
            
            // Flash all sprite renderers
            foreach (var sr in spriteRenderers)
            {
                if (sr != null) sr.color = flashColor;
            }
            yield return new WaitForSeconds(damageFlashDuration / 2f);

            // Restore baseline scale
            transform.localScale = _baselineScale;
            
            // Restore original colors
            for (int j = 0; j < spriteRenderers.Count; j++)
            {
                if (spriteRenderers[j] != null && j < originalColors.Count)
                    spriteRenderers[j].color = originalColors[j];
            }
            yield return new WaitForSeconds(damageFlashDuration / 2f);
        }

        // Final restore
        for (int j = 0; j < spriteRenderers.Count; j++)
        {
            if (spriteRenderers[j] != null && j < originalColors.Count)
                spriteRenderers[j].color = originalColors[j];
        }
    }

    // Ensure scale is restored
    transform.localScale = _baselineScale;

    Debug.Log($"[DamageFlashCoroutine] Completed on {gameObject.name}");
    damageFlashCoroutine = null;
}

public void UpdateOriginalColor(Color newColor)
{
    // Update all original colors and apply if not currently flashing
    for (int i = 0; i < originalColors.Count; i++)
    {
        originalColors[i] = newColor;
    }
    
    if (damageFlashCoroutine == null)
    {
        foreach (var sr in spriteRenderers)
        {
            if (sr != null) sr.color = newColor;
        }
    }
}
}