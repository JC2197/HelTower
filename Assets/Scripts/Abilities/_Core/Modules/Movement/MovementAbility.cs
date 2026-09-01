using UnityEngine;
using System.Collections;

/// <summary>
/// Handles movement ability execution - applies velocity/force to character for a duration
/// </summary>
public class MovementAbility : MonoBehaviour
{
    private Rigidbody2D rb;
    public AbilityDataConfig config;
    private bool isExecuting = false;
    private float startTime;
    private GameObject caster;
    private IDamageable casterDamageable;
    private bool evadeApplied;

    // Teleport state
    private bool isTeleporting;
    private float teleportEndTime;
    private Vector2 teleportDestination;
    private SpriteRenderer[] cachedRenderers;

    private Vector2 movementDirection;
    private Vector2 baseAdditiveVelocity;

    public bool IsExecuting => isExecuting;
    public Vector2 AdditiveVelocity
    {
        get
        {
            if (!isExecuting || config?.movementConfig == null)
                return Vector2.zero;

            MovementConfig movementConfig = config.movementConfig;
            if (!movementConfig.lerp || movementConfig.duration <= 0f)
                return baseAdditiveVelocity;

            float normalizedTime = Mathf.Clamp01((Time.time - startTime) / movementConfig.duration);
            float velocityMultiplier = 6f * normalizedTime * (1f - normalizedTime);
            return baseAdditiveVelocity * velocityMultiplier;
        }
    }

    public void Initialize(AbilityDataConfig abilityConfig)
    {
        config = abilityConfig;
        rb = GetComponent<Rigidbody2D>();
        caster = gameObject;
        casterDamageable = GetComponent<IDamageable>();

        if (rb == null)
        {
            Debug.LogError($"[MovementAbility] No Rigidbody2D found on {gameObject.name} in scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}!");
        }
        else
        {
            Debug.Log($"[MovementAbility] Initialized on {gameObject.name} in scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}: rb.bodyType={rb.bodyType}, rb.constraints={rb.constraints}");
        }
    }

    /// <summary>
    /// Start executing the movement ability
    /// </summary>
    public bool Execute()
    {
        if (isExecuting)
        {
            Debug.LogWarning($"[MovementAbility] Already executing movement for {config?.abilityName}");
            return false;
        }

        if (rb == null || config == null || config.movementConfig == null)
        {
            Debug.LogError($"[MovementAbility] Missing required components: rb={rb != null}, config={config != null}, movementConfig={config?.movementConfig != null}, scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            return false;
        }

        // Check Rigidbody2D state
        Debug.Log($"[MovementAbility] Pre-execute state in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}: rb.bodyType={rb.bodyType}, isKinematic={rb.bodyType == RigidbodyType2D.Kinematic}, constraints={rb.constraints}, simulated={rb.simulated}");

        if (rb.bodyType == RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning($"[MovementAbility] Rigidbody2D is Kinematic in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}! Movement may not work. Setting to Dynamic.");
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        isExecuting = true;
        startTime = Time.time;
        movementDirection = GetMovementDirection();
        baseAdditiveVelocity = Vector2.zero;

        float distanceMultiplier = GetDashDistanceMultiplier();

        // Enable evade/invincibility if this is a dash (also used for teleport i-frames)
        if (config.movementConfig.isDashing && casterDamageable != null)
        {
            casterDamageable.SetEvading(true);
            evadeApplied = true;
            Debug.Log($"[MovementAbility] Evade ENABLED for {config.abilityName}");
        }

        // Calculate direction
        switch (config.movementConfig.movementType)
        {
            case MovementType.SpeedOverTime:
                if (config.movementConfig.speed > 0f)
                {
                    baseAdditiveVelocity = movementDirection * config.movementConfig.speed * distanceMultiplier;
                }
                else
                {
                    Debug.LogWarning($"[MovementAbility] SpeedOverTime requires speed > 0 for {config.abilityName}");
                }
                break;

            case MovementType.DistanceOverTime:
                if (config.movementConfig.duration > 0f)
                {
                    float distance = config.movementConfig.distance * distanceMultiplier;
                    baseAdditiveVelocity = movementDirection * (distance / config.movementConfig.duration);
                }
                else
                {
                    Debug.LogWarning($"[MovementAbility] DistanceOverTime requires duration > 0 for {config.abilityName}");
                }
                break;

            case MovementType.Teleport:
                teleportDestination = (Vector2)transform.position + movementDirection * (config.movementConfig.distance * distanceMultiplier);
                if (config.movementConfig.teleportAnimationPrefab != null)
                {   
                    Instantiate(config.movementConfig.teleportAnimationPrefab, transform.position, Quaternion.identity);
                }
                if (config.movementConfig.disappearDuringTeleport)
                {
                    cachedRenderers = caster.GetComponentsInChildren<SpriteRenderer>();
                    SetRenderersEnabled(false);
                }
                rb.transform.position = teleportDestination; // Move immediately to destination
                break;
            default:
                Debug.LogWarning($"[MovementAbility] Unknown movement type {config.movementConfig.movementType} for {config.abilityName}");
                break;
        }
        AudioManager.Instance?.PlaySpatialSound(config.movementConfig.dashSound, transform.position);
        return true;
    }

    /// <summary>
    /// Update movement each frame - maintains velocity for velocity-based movement
    /// </summary>
    public void UpdateMovement()
    {
        if (!isExecuting || config == null) return;

        // --- Teleport mode ---
        if (isTeleporting)
        {
            if (Time.time >= teleportEndTime)
            {
                CompleteTeleport();
            }
            return;
        }

        // --- Normal movement ---
        float elapsed = Time.time - startTime;

        // Check duration limit
        bool durationExpired = config.movementConfig.duration > 0f && elapsed >= config.movementConfig.duration;

        if (durationExpired)
        {
            Debug.Log($"[MovementAbility] Movement complete for {config.abilityName}: elapsed={elapsed:F3}s");
            End();
            return;
        }
    }

    /// <summary>
    /// End the movement ability
    /// </summary>
    public void End()
    {
        if (!isExecuting) return;

        Debug.Log($"[MovementAbility] Ended for {config?.abilityName}");
        isExecuting = false;
        isTeleporting = false;
        baseAdditiveVelocity = Vector2.zero;

        // Re-enable renderers in case teleport was interrupted
        if (cachedRenderers != null)
        {
            SetRenderersEnabled(true);
            cachedRenderers = null;
        }
        
        // Disable evade when movement ends
        if (evadeApplied && casterDamageable != null)
        {
            casterDamageable.SetEvading(false);
            evadeApplied = false;
            Debug.Log($"[MovementAbility] Evade DISABLED for {config?.abilityName}");
        }

    }

    private void OnDisable()
    {
        End();
    }

    /// <summary>
    /// Completes the teleport: moves character to destination, re-enables visuals, spawns end effect
    /// </summary>
    private void CompleteTeleport()
    {
        // Move character to destination
        transform.position = (Vector3)teleportDestination;
        Debug.Log($"[MovementAbility] Teleport complete for {config.abilityName}: arrived at {teleportDestination}");

        // Spawn animation prefab at end position
        if (config.movementConfig.teleportAnimationPrefab != null)
        {
            Instantiate(config.movementConfig.teleportAnimationPrefab, transform.position, Quaternion.identity);
        }

        // Re-enable renderers
        if (cachedRenderers != null)
        {
            SetRenderersEnabled(true);
            cachedRenderers = null;
        }

        End();
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (cachedRenderers == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = enabled;
        }
    }

    /// <summary>
    /// Returns the DashDistance multiplier for the caster (1.0 = no bonus).
    /// DashDistance stat is stored as a fraction: 0.5 = 50% further, so multiplier = 1 + value.
    /// </summary>
    private float GetDashDistanceMultiplier()
    {
        if (caster == null) return 1f;
        Organism organism = caster.GetComponent<Organism>();
        if (organism == null || organism.AllStats == null) return 1f;
        if (!organism.AllStats.HasStat("DashDistance")) return 1f;
        return 1f + organism.AllStats.GetStat("DashDistance");
    }

    private Vector2 GetMovementDirection()
    {
        if (config.movementConfig.towardMouse)
        {
            Vector3 mouseWorld = InputUtility.GetMouseWorldPosition();
            Vector2 dir = (mouseWorld - transform.position);
            return dir.normalized;
        }

        if (config.movementConfig.awayFromMouse)
        {
            Vector3 mouseWorld = InputUtility.GetMouseWorldPosition();
            Vector2 dir = (transform.position - mouseWorld);
            return dir.normalized;
        }

        // Try to get player input direction
        PlayerController player = GetComponent<PlayerController>();
        if (player != null)
        {
            Vector2 input = player.GetMovementInput();
            if (input.magnitude > 0.1f)
            {
                return input.normalized;
            }
        }

        // Default to facing direction
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            return sprite.flipX ? Vector2.left : Vector2.right;
        }

        return Vector2.right;
    }
}
