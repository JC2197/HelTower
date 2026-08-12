using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet;

/// <summary>
/// Manages spawning and lifecycle of constructs (pylons, turrets, totems).
/// The actual construct behavior is handled by the Construct component which extends Organism.
/// Supports both local (single-player) and networked (multiplayer) spawning.
/// </summary>
public class ConstructAbility : MonoBehaviour, ISubAbility
{
    private const string AbilityPipelineTag = "[Ability pipeline]";

    private ConstructConfig config;
    private GameObject constructInstance;
    private float spawnTime;
    private bool isDestroying = false;
    private List<GameObject> sharedConstructList; // Reference to shared list from DataDrivenAbility
    private GameObject owner;
    private AbilityDataConfig parentConfig;
    
    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        owner = context.owner;
    }

    /// <summary>
    /// Initialize and spawn the construct at the specified position
    /// </summary>
    public void SpawnConstruct(ConstructConfig constructConfig, Vector3 spawnPosition, List<GameObject> sharedConstructs = null)
    {
        config = constructConfig;
        sharedConstructList = sharedConstructs ?? new List<GameObject>();

        Debug.Log($"{AbilityPipelineTag} ConstructAbility.SpawnConstruct: prefab={(constructConfig != null && constructConfig.constructPrefab != null ? constructConfig.constructPrefab.name : "NULL")}, spawnPosition={spawnPosition}, owner={(owner != null ? owner.name : "NULL")}, sharedCount={sharedConstructList.Count}");
        
        Debug.Log($"[ConstructAbility] SpawnConstruct called. Current constructs: {sharedConstructList.Count}, Max: {config.maxConstructs}");
        
        if (config.constructPrefab == null)
        {
            Debug.LogError("[ConstructAbility] No construct prefab assigned!");
            return;
        }
        
        // Clean up null references
        int nullCount = sharedConstructList.RemoveAll(c => c == null);
        if (nullCount > 0)
        {
            Debug.Log($"[ConstructAbility] Removed {nullCount} null construct references. New count: {sharedConstructList.Count}");
        }
        
        // Handle construct limits using shared list
        if (config.maxConstructs > 0 && sharedConstructList.Count >= config.maxConstructs)
        {
            Debug.Log($"[ConstructAbility] Construct limit reached ({sharedConstructList.Count}/{config.maxConstructs}). Limit behavior: {config.limitBehavior}");
            bool shouldProceed = HandleConstructLimit(spawnPosition);
            if (!shouldProceed)
            {
                Debug.Log($"[ConstructAbility] Cannot spawn construct - limit reached ({sharedConstructList.Count}/{config.maxConstructs})");
                return; // Don't spawn if limit is reached and behavior is PreventSpawn
            }
            Debug.Log($"[ConstructAbility] Proceeding with spawn after handling limit. Current count: {sharedConstructList.Count}");
        }
        
        // Instantiate the construct prefab (local first)
        constructInstance = Instantiate(config.constructPrefab, spawnPosition, Quaternion.identity);
        constructInstance.name = $"{config.constructPrefab.name}_Construct";
        
        // Network spawn if in multiplayer
        var networkManager = InstanceFinder.NetworkManager;
        if (networkManager != null && networkManager.IsServerStarted)
        {
            networkManager.ServerManager.Spawn(constructInstance);
            Debug.Log($"[ConstructAbility] Network-spawned construct: {constructInstance.name}");
        }
        
        sharedConstructList.Add(constructInstance);
        
        Debug.Log($"[ConstructAbility] Spawned construct '{constructInstance.name}' at {spawnPosition}. Total constructs in list: {sharedConstructList.Count}/{config.maxConstructs}");
        Debug.Log($"[ConstructAbility] Shared list instance ID: {sharedConstructList.GetHashCode()}");
        
        // Prefer whatever Construct-derived behavior is authored on the prefab.
        // This allows assigning an AutoTurret-scripted prefab directly in constructPrefab.
        Construct construct = constructInstance.GetComponent<Construct>();

        bool hasProjectileAbility = config.constructAbilities != null
            && config.constructAbilities.Exists(a => a != null && a.abilityType == ConstructAbilityConfig.AbilityType.Projectile);

        if (construct == null && hasProjectileAbility)
        {
            // Backward compatibility: legacy prefabs without a Construct script still fire
            // projectile construct abilities by adding AutoTurret at runtime.
            construct = constructInstance.AddComponent<AutoTurret>();
            Debug.LogWarning($"[ConstructAbility] Added AutoTurret at runtime on '{constructInstance.name}'. " +
                "Recommended: add AutoTurret on the prefab and configure its turret settings there.");
        }

        if (construct == null)
        {
            // Generic constructs can use the base Construct behavior.
            construct = constructInstance.AddComponent<Construct>();
            Debug.Log($"[ConstructAbility] Using generic Construct component");
        }
        else
        {
            Debug.Log($"[ConstructAbility] Using prefab-authored component: {construct.GetType().Name}");
        }
        
        // Initialize the construct (this handles health, turrets, etc.)
        construct.Initialize(config, owner);        
        // Apply spawn knockback if configured
        if (config.spawnKnockbackRadius > 0 && config.spawnKnockbackForce > 0)
        {
            ApplySpawnKnockback(spawnPosition);
        }
        
        // Activate after delay if configured
        if (config.activationDelay > 0)
        {
            StartCoroutine(ActivateAfterDelay(construct, config.activationDelay));
        }
        else
        {
            construct.Activate();
        }
        
        spawnTime = Time.time;
    }
    
    private bool HandleConstructLimit(Vector3 newSpawnPosition)
    {
        // Remove null references
        int nullCount = sharedConstructList.RemoveAll(c => c == null);
        if (nullCount > 0)
        {
            Debug.Log($"[ConstructAbility] HandleConstructLimit removed {nullCount} null references");
        }
        
        Debug.Log($"[ConstructAbility] HandleConstructLimit executing with behavior: {config.limitBehavior}, Current count: {sharedConstructList.Count}");
        
        switch (config.limitBehavior)
        {
            case ConstructLimitBehavior.DestroyOldest:
                if (sharedConstructList.Count > 0)
                {
                    GameObject oldest = sharedConstructList[0];
                    Debug.Log($"[ConstructAbility] Destroying oldest construct: {(oldest != null ? oldest.name : "null")}");
                    sharedConstructList.RemoveAt(0);
                    
                    // Destroy the construct GameObject
                    Destroy(oldest);
                    Debug.Log($"[ConstructAbility] After destroying oldest, count: {sharedConstructList.Count}");
                }
                return true; // Proceed with spawn
                
            case ConstructLimitBehavior.PreventSpawn:
                Debug.Log($"[ConstructAbility] PreventSpawn: Max constructs reached ({sharedConstructList.Count}/{config.maxConstructs}), blocking spawn");
                return false; // Don't proceed with spawn
                
            case ConstructLimitBehavior.ReplaceClosest:
                GameObject closest = null;
                float closestDist = float.MaxValue;
                foreach (GameObject construct in sharedConstructList)
                {
                    if (construct == null) continue;
                    float dist = Vector3.Distance(construct.transform.position, newSpawnPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = construct;
                    }
                }
                if (closest != null)
                {
                    Debug.Log($"[ConstructAbility] Replacing closest construct at distance {closestDist}: {closest.name}");
                    sharedConstructList.Remove(closest);
                    
                    Destroy(closest);
                }
                return true; // Proceed with spawn
        }
        
        return true; // Default: proceed with spawn
    }
    
    private void ApplySpawnKnockback(Vector3 spawnPosition)
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(spawnPosition, config.spawnKnockbackRadius);
        
        foreach (Collider2D hit in hitColliders)
        {
            if (hit.gameObject == constructInstance || hit.gameObject == owner) continue;
            
            // Check if it's an enemy
            if (((1 << hit.gameObject.layer) & LayerMask.GetMask("Enemy")) != 0)
            {
                Rigidbody2D targetRb = hit.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    Vector2 knockbackDir = (hit.transform.position - spawnPosition).normalized;
                    targetRb.AddForce(knockbackDir * config.spawnKnockbackForce, ForceMode2D.Impulse);
                    Debug.Log($"[ConstructAbility] Applied spawn knockback to {hit.gameObject.name}");
                }
            }
        }
    }
    
    private IEnumerator ActivateAfterDelay(Construct construct, float delay)
    {
        yield return new WaitForSeconds(delay);
        construct.Activate();
    }
    
    private void Update()
    {
        if (constructInstance == null || isDestroying) return;
        
        // Check lifetime
        if (config.lifetime > 0 && Time.time >= spawnTime + config.lifetime)
        {
            if (config.destroyOnLifetimeEnd)
            {
                Debug.Log($"[ConstructAbility] Lifetime expired, destroying construct");
                DestroyConstruct();
            }
        }
    }
    
    public void DestroyConstruct()
    {
        if (isDestroying || constructInstance == null) return;
        
        isDestroying = true;
        Debug.Log($"[ConstructAbility] Starting construct destruction");
        
        sharedConstructList.Remove(constructInstance);
        
        // Destroy the construct GameObject
        Destroy(constructInstance);
    }
    
    private void OnDestroy()
    {
        // Cleanup construct instance
        if (constructInstance != null)
        {
            sharedConstructList.Remove(constructInstance);
            Destroy(constructInstance);
        }
    }
    
    public GameObject ConstructInstance => constructInstance;
}
