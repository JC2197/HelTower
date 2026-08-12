using UnityEngine;

/// <summary>
/// Base class for all constructs (turrets, pylons, totems, etc.)
/// Extends Organism to get health, damage, and health bar support automatically
/// </summary>
public class Construct : Organism
{
    protected ConstructConfig config;
    protected GameObject owner;
    protected string currentAnimationPlaying;
    
    protected float lastAttackTime;
    protected float attackCooldown;
    protected bool isActive = false;
    
    public bool IsActive => isActive;
    public ConstructConfig Config => config;
    public new GameObject Owner => owner;
    public Animator animator => GetComponentInChildren<Animator>();
    /// <summary>
    /// Initialize the construct with its configuration
    /// </summary>
    public virtual void Initialize(ConstructConfig constructConfig, GameObject caster)
    {
        config = constructConfig;
        owner = caster;

        PlayConfiguredAnimation(config != null ? config.spawnAnimationName : null);
        
        // Set up health from config (use Organism's internal health system)
        if (config != null)
        {
            // maxHealth == 0 means invulnerable — skip health initialisation entirely.
            // Calling ModifyHealth(0) would trigger Die() because current health starts at 0.
            if (config.maxHealth > 0)
            {
                if (statContainer != null)
                {
                    statContainer.SetStat("MaxHealth", config.maxHealth);
                }
                // Heal to full health after updating MaxHealth.
                // Guard: if Construct is added via AddComponent on an already-network-spawned
                // object, FishNet's SyncVar internal binding is incomplete and the setter throws.
                // The permanent fix is to add the Construct component to the construct prefab
                // directly so GetComponent<Construct>() succeeds before ServerManager.Spawn.
                try
                {
                    ModifyHealth(MaxHealth);
                }
                catch (System.NullReferenceException)
                {
                    Debug.LogWarning($"[Construct] '{gameObject.name}': ModifyHealth skipped — SyncVar not network-ready. " +
                        "Add the Construct component to this prefab prior to spawning to fix this permanently.");
                }
            }
            
            // Calculate attack cooldown from attack speed
            attackCooldown = config.attackSpeed > 0 ? 1f / config.attackSpeed : 1f;
            lastAttackTime = -attackCooldown; // Ready to attack immediately
        }
        
        // Set Construct tag
        gameObject.tag = "Construct";
    }
    
    /// <summary>
    /// Activate the construct (called after spawn delay if configured)
    /// </summary>
    public virtual void Activate()
    {
        isActive = true;
        PlayConfiguredAnimation(config != null ? config.activeAnimationName : null);
        Debug.Log($"[Construct] Activated");
    }

    /// <summary>
    /// Call this whenever the construct executes one of its configured abilities.
    /// Uses the shared fire animation name and scales playback by attack speed.
    /// </summary>
    protected virtual void OnConstructAbilityUsed()
    {
        float speed = config != null && config.attackSpeed > 0f ? config.attackSpeed : 1f;
        PlayConfiguredAnimation(config != null ? config.fireAnimationName : null, speed);
    }

    /// <summary>
    /// Plays a named animation safely on the construct animator.
    /// </summary>
    protected virtual void PlayConfiguredAnimation(string animationName, float speed = 1f)
    {
        if (string.IsNullOrEmpty(animationName))
            return;

        Animator anim = animator;
        if (anim == null)
            return;

        float clampedSpeed = Mathf.Max(0.01f, speed);
        anim.speed = clampedSpeed;
        anim.Play(animationName, 0, 0f);
        currentAnimationPlaying = animationName;
    }

    /// <summary>
    /// Applies a refreshed construct config at runtime (for trait modifier updates on already-spawned constructs).
    /// </summary>
    public virtual void ApplyRuntimeConfig(ConstructConfig refreshedConfig)
    {
        if (refreshedConfig == null)
            return;

        config = refreshedConfig;
        attackCooldown = config.attackSpeed > 0f ? 1f / config.attackSpeed : 1f;
    }
    
    protected override void HandleUpdate()
    {
        // Base constructs don't have update logic - override in subclasses
    }
    
    protected Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }
            
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    protected override void HandleDeath()
    {
        PlayConfiguredAnimation(config != null ? config.destructionAnimationName : null);
        Debug.Log($"[Construct] Destroyed");
        Destroy(gameObject, 0.1f);
    }
}
