using UnityEngine;

/// <summary>
/// Standalone DoT effect that can be applied by abilities (Area, Projectile, etc.)
/// Uses time accumulator for smooth damage application independent of framerate
/// </summary>
public class DotEffect : MonoBehaviour
{
    private IDamageable target;
    private GameObject source;
    private string damageTypeName;
    
    private float dotDuration;
    private float dotInterval; // Original tick interval (e.g., 0.5s) - for floater display
    private float damagePerTick; // Actual damage per tick for display
    
    private float remainingDuration;
    private float damageAccumulator;
    private float floaterTimer; // Timer for displaying floaters
    
    private ParticleSystem particlePrefab;
    private GameObject activeParticles;
    private bool startParticlesFromFeet = false;
    
    private const float DAMAGE_UPDATE_INTERVAL = 0.1f; // Apply damage every 0.1s for smoothness
    
    /// <summary>
    /// Initialize and start the DoT effect
    /// </summary>
    public void Initialize(IDamageable target, GameObject source, string damageTypeName, 
        float damagePerTick, float dotInterval, float dotDuration, ParticleSystem particlePrefab = null, bool startParticlesFromFeet = false)
    {
        this.target = target;
        this.source = source;
        this.damageTypeName = damageTypeName;
        this.dotInterval = dotInterval;
        this.dotDuration = dotDuration;
        this.particlePrefab = particlePrefab;
        this.startParticlesFromFeet = startParticlesFromFeet;
        this.damagePerTick = damagePerTick;
        this.remainingDuration = dotDuration;
        this.damageAccumulator = 0f;
        this.floaterTimer = 0f; // Start at 0, accumulate to dotInterval before showing first floater

        // Scale the DPS by the source's damage bonuses so stat-container bonuses apply to DoTs.
        // DamageCalculator.CalculateFinalDamage returns baseDamage * (1 + bonus), so passing 1.0
        // gives us the multiplier directly.
        float bonusMultiplier = 1f;
        if (source != null)
        {
            bonusMultiplier = DamageCalculator.CalculateFinalDamage(1f, damageTypeName, source);
        }

        damagePerTick *= bonusMultiplier;
        // Debug log to verify correct initialization
        
        // Spawn particle effect if provided
        if (particlePrefab != null)
        {
            SpawnParticleEffect();
        }
    }
    
    void Update()
    {
        if (target == null || remainingDuration <= 0)
        {
            Destroy(gameObject);
            return;
        }
        
        // Accumulate time for smooth damage
        damageAccumulator += Time.deltaTime;
        floaterTimer += Time.deltaTime;
        remainingDuration -= Time.deltaTime;
        
        // Apply smooth damage WITHOUT floaters
        if (damageAccumulator >= DAMAGE_UPDATE_INTERVAL)
        {
            float smoothDamage = damagePerTick * damageAccumulator;
            
            // Apply damage silently (no floater)
            target.TakeDamage(smoothDamage, damageTypeName, suppressFloater: true);
            
            damageAccumulator = 0f;
        }
        
        // Display floater at tick intervals
        if (floaterTimer >= dotInterval)
        {
            // Show floater for the actual tick damage (not smoothed/calculated)
            if (target is IDamageFloaterSource floaterSource)
            {
                floaterSource.ShowDamageFloater(damagePerTick, damageTypeName);
            }
            floaterTimer = 0f;
        }
        
        // Clean up when duration expires
        if (remainingDuration <= 0)
        {
            Destroy(gameObject);
        }
    }
    
    private void SpawnParticleEffect()
    {
        if (particlePrefab == null || target == null) return;
        
        // Get the target's MonoBehaviour component to access its GameObject
        MonoBehaviour targetMono = target as MonoBehaviour;
        if (targetMono == null) return;
        
        // Get target bounds for particle shape scaling
        SpriteRenderer targetRenderer = targetMono.GetComponent<SpriteRenderer>();
        Collider2D targetCollider = targetMono.GetComponent<Collider2D>();
        int targetSortingOrder = targetRenderer != null ? targetRenderer.sortingOrder : 0;
        string targetSortingLayer = targetRenderer != null ? targetRenderer.sortingLayerName : "Default";
        
        Vector3 targetBounds = Vector3.one;
        Vector3 bottomOffset = Vector3.zero;
        if (targetRenderer != null && targetRenderer.sprite != null)
        {
            Bounds spriteBounds = targetRenderer.bounds;
            targetBounds = new Vector3(spriteBounds.size.x, spriteBounds.size.y, spriteBounds.size.z);
            if (startParticlesFromFeet)
            {
                // Offset from center to bottom (local space)
                bottomOffset = new Vector3(0, -0.5f * spriteBounds.size.y, 0);
            }
        }
        else if (targetCollider != null)
        {
            Bounds colliderBounds = targetCollider.bounds;
            targetBounds = new Vector3(colliderBounds.size.x, colliderBounds.size.y, colliderBounds.size.z);
            if (startParticlesFromFeet)
            {
                bottomOffset = new Vector3(0, -0.5f * colliderBounds.size.y, 0);
            }
        }
        
        // Spawn particles as child of target
        activeParticles = Instantiate(particlePrefab.gameObject, targetMono.transform);
        activeParticles.transform.localPosition = bottomOffset;
        
        // Configure all particle systems
        ParticleSystem[] allParticleSystems = activeParticles.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in allParticleSystems)
        {
            var shape = ps.shape;
            if (shape.enabled)
            {
                shape.scale = new Vector3(targetBounds.x, targetBounds.y, targetBounds.y);
            }
            
            ps.Play();
            
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerName = targetSortingLayer;
                // Randomly choose front or back for visual variety
                int randomOffset = UnityEngine.Random.value > 0.2f ? 10000 : -150;
                renderer.sortingOrder = targetSortingOrder + randomOffset;
            }
        }
    }
    
    void OnDestroy()
    {
        // Apply any remaining accumulated damage before destroying (silently)
        if (target != null && damageAccumulator > 0)
        {
            float finalDamage = damagePerTick * damageAccumulator;
            target.TakeDamage(finalDamage, damageTypeName, suppressFloater: true);
        }
        
        // Clean up particle effect
        if (activeParticles != null)
        {
            Destroy(activeParticles);
        }
    }
}
