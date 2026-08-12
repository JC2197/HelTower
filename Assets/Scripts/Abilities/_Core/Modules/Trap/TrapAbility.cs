using UnityEngine;
using System.Collections;

/// <summary>
/// Trap component - dormant object that triggers an ability when enemies enter range
/// </summary>
public class TrapAbility : MonoBehaviour, ISubAbility
{
    private TrapAbilityConfig config;
    private GameObject owner;
    private GameObject statOwner;
    private string abilityName;
    private System.Collections.Generic.List<string> abilityTags;
    private AbilityDataConfig parentConfig;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    
    private bool isArmed = false;
    private bool hasTriggered = false;
    private float lastTriggerTime = -999f;
    private float spawnTime;
    private GameObject triggeringEnemy;
    
    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        owner = context.owner;
        statOwner = context.statOwner != null ? context.statOwner : context.owner;
        abilityName = context.AbilityName;
        abilityTags = context.AbilityTags;
    }

    /// <summary>
    /// Initialize the trap with its configuration
    /// </summary>
    public void Initialize(TrapAbilityConfig trapConfig)
    {
        config = trapConfig;
        spawnTime = Time.time;
        
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Set Trap tag
        gameObject.tag = "Trap";
        
        // Setup collider as trigger for detection (only if not already configured in prefab)
        CircleCollider2D triggerCollider = GetComponent<CircleCollider2D>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<CircleCollider2D>();
            triggerCollider.radius = config.triggerRange;
            triggerCollider.isTrigger = true;
        }
        
        // Play idle animation
        if (animator != null && !string.IsNullOrEmpty(config.idleAnimationName))
        {
            animator.Play(config.idleAnimationName);
        }
        
        // Spawn visual effect
        if (config.spawnEffect != null)
        {
            Instantiate(config.spawnEffect, transform.position, Quaternion.identity);
        }
        
        // Start arming delay
        if (config.armingDelay > 0f)
        {
            StartCoroutine(ArmTrapDelayed());
        }
        else
        {
            isArmed = true;
        }
        
        // Handle lifetime
        if (config.lifetime > 0f && config.destroyOnLifetimeEnd)
        {
            Destroy(gameObject, config.lifetime);
        }
        
        Debug.Log($"[TrapAbility] Initialized trap at {transform.position}, armed in {config.armingDelay}s");
    }
    
    private IEnumerator ArmTrapDelayed()
    {
        yield return new WaitForSeconds(config.armingDelay);
        isArmed = true;
        Debug.Log($"[TrapAbility] Trap armed and ready");
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isArmed || config == null) return;
        
        // Check if this object can trigger the trap
        if (((1 << other.gameObject.layer) & config.triggerLayers) == 0) return;
        
        // Check if single trigger and already triggered
        if (config.singleTrigger && hasTriggered) return;
        
        // Check retrigger cooldown
        if (!config.singleTrigger && Time.time < lastTriggerTime + config.retriggerCooldown) return;
        
        // Trigger the trap!
        TriggerTrap(other.gameObject);
    }
    
    private void TriggerTrap(GameObject triggeringEntity)
    {
        hasTriggered = true;
        lastTriggerTime = Time.time;
        triggeringEnemy = triggeringEntity;
        
        Debug.Log($"[TrapAbility] Trap triggered by {triggeringEntity.name}!");
        
        // Play trigger animation
        if (animator != null && !string.IsNullOrEmpty(config.triggerAnimationName))
        {
            animator.Play(config.triggerAnimationName);
        }
        
        // Play trigger effect
        if (config.triggerEffect != null)
        {
            Instantiate(config.triggerEffect, transform.position, Quaternion.identity);
        }
        
        // Execute the triggered ability
        ExecuteTriggeredAbility();
        
        // Destroy trap after delay (or immediately if single trigger)
        if (config.singleTrigger)
        {
            Destroy(gameObject, config.destroyDelay);
        }
    }
    
    private void ExecuteTriggeredAbility()
    {
        if (config == null) return;
        
        switch (config.abilityType)
        {
            case TrapAbilityType.Area:
                SpawnAreaAbility();
                break;
                
            case TrapAbilityType.Projectile:
                SpawnProjectiles();
                break;
                
            case TrapAbilityType.Explosion:
                TriggerExplosion();
                break;
        }
    }
    
    private void SpawnAreaAbility()
    {
        if (config.areaConfig == null || config.areaConfig.hitbox.prefab == null)
        {
            Debug.LogWarning("[TrapAbility] No area config or spell prefab assigned!");
            return;
        }
        
        // Spawn area effect at trap position
        GameObject areaObject = Instantiate(
            config.areaConfig.hitbox.prefab,
            transform.position,
            Quaternion.identity
        );
        
        // Initialize the area ability
        var areaAbility = areaObject.GetComponent<AreaAbility>();
        if (areaAbility != null)
        {
            areaAbility.SetContext(new SubAbilityContext { parentConfig = parentConfig, owner = owner, statOwner = statOwner });
            areaAbility.InitializeFromConfig(config.areaConfig);
            areaAbility.ConfigureParticles(config.areaConfig);
            areaAbility.Activate();
        }
        
        Debug.Log($"[TrapAbility] Spawned area ability at trap location");
    }
    
    private void SpawnProjectiles()
    {
        if (config.projectileConfig == null)
        {
            Debug.LogWarning("[TrapAbility] No projectile config assigned!");
            return;
        }
        
        Vector3 spawnPosition = transform.position;
        
        // If projectileCount = 0, aim at triggering enemy
        if (config.projectileCount == 0)
        {
            Vector3 direction = triggeringEnemy != null 
                ? (triggeringEnemy.transform.position - spawnPosition).normalized 
                : Vector3.right;
            
            ProjectileSpawner.SpawnProjectiles(
                config.projectileConfig,
                spawnPosition,
                direction,
                statOwner,
                1f,
                abilityName: abilityName,
                abilityTags: abilityTags,
                parentConfig: parentConfig
            );
        }
        else
        {
            // Spawn multiple projectiles in a spread pattern
            float angleStep = config.projectileSpread / config.projectileCount;
            float startAngle = -config.projectileSpread / 2f;
            
            for (int i = 0; i < config.projectileCount; i++)
            {
                float angle = startAngle + (angleStep * i);
                Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.right;
                
                ProjectileSpawner.SpawnProjectiles(
                    config.projectileConfig,
                    spawnPosition,
                    direction,
                    statOwner,
                    1f,
                    abilityName: abilityName,
                    abilityTags: abilityTags,
                    parentConfig: parentConfig
                );
            }
        }
        
        Debug.Log($"[TrapAbility] Fired {(config.projectileCount == 0 ? 1 : config.projectileCount)} projectile(s)");
    }
    
    private void TriggerExplosion()
    {
        // Immediate damage in radius - uses explosion config
        if (config.explosionConfig == null)
        {
            Debug.LogWarning("[TrapAbility] No explosion config assigned!");
            return;
        }
        
        // Create explosion ability object at trap position
        GameObject explosionObject = new GameObject("Explosion");
        explosionObject.transform.position = transform.position;
        ExplosionAbility explosionAbility = explosionObject.AddComponent<ExplosionAbility>();
        explosionAbility.SetContext(new SubAbilityContext { parentConfig = parentConfig, owner = owner, statOwner = statOwner });
        explosionAbility.Initialize(config.explosionConfig);
        
        Debug.Log($"[TrapAbility] Triggered explosion at trap location");
    }
    

    
    private void OnDrawGizmosSelected()
    {
        if (config != null && config.showTriggerRadius)
        {
            Gizmos.color = config.triggerRadiusColor;
            Gizmos.DrawWireSphere(transform.position, config.triggerRange);
        }
    }
}
