using UnityEngine;
using System.Collections;

/// <summary>
/// Auto-targeting turret construct that tracks and fires at enemies
/// </summary>
public class AutoTurret : Construct
{
    private const string AbilityPipelineTag = "[Ability pipeline]";

    [Header("Turret Settings")]
    [Tooltip("Name of the turret child transform that rotates toward enemies")]
    [SerializeField] protected string turretChildName = "Turret";

    [Tooltip("Name of the launch zone transform for projectile spawning")]
    [SerializeField] protected string launchZoneChildName = "LaunchZone";

    [Tooltip("Detection range for finding enemies to target")]
    [SerializeField] protected float detectionRange = 10f;

    [Tooltip("How fast the turret rotates toward target (degrees per second)")]
    [SerializeField] protected float turretRotationSpeed = 180f;
    
    protected Transform turretTransform;
    protected Transform launchZoneTransform;
    protected GameObject currentTarget;
    public override void Initialize(ConstructConfig constructConfig, GameObject caster)
    {
        base.Initialize(constructConfig, caster);
        
        // Find turret and launch zone transforms if configured
        if (!string.IsNullOrEmpty(turretChildName))
        {
            turretTransform = FindChildRecursive(transform, turretChildName);
            if (turretTransform != null)
            {
                Debug.Log($"[AutoTurret] Found turret transform: {turretTransform.name}");
            }
            else
            {
                Debug.LogWarning($"[AutoTurret] Could not find turret child named '{turretChildName}'");
            }
        }
        
        if (!string.IsNullOrEmpty(launchZoneChildName))
        {
            launchZoneTransform = FindChildRecursive(transform, launchZoneChildName);
            if (launchZoneTransform != null)
            {
                Debug.Log($"[AutoTurret] Found launch zone transform: {launchZoneTransform.name}");
            }
        }
    }
    
    protected override void HandleUpdate()
    {
        if (!isActive) return;
        // Only run authoritative turret logic on the server (or in single-player).
        // Clients receive projectile state via FishNet replication — firing locally
        // would spawn cosmetic-only predictive clones that never destroy on collision.
        if (IsSpawned && !IsServerStarted) return;
        
        // Handle turret behavior
        if (turretTransform != null && config != null)
        {
            UpdateTurretBehavior();
        }
    }
    
    protected virtual void UpdateTurretBehavior()
    {
        // Find and track target
        if (currentTarget == null || !IsValidTarget(currentTarget))
        {
            currentTarget = FindNearestEnemy();
        }
        
        // Rotate turret toward target
        if (currentTarget != null)
        {
            RotateTurretToward(currentTarget.transform.position);
            
            // Fire projectile if ready
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                FireProjectile();
                lastAttackTime = Time.time;
            }
        }
    }
    
    protected virtual GameObject FindNearestEnemy()
    {
        if (config == null) return null;
        
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRange, LayerMask.GetMask("Enemy"));
        
        GameObject nearest = null;
        float nearestDist = float.MaxValue;
        
        foreach (Collider2D col in colliders)
        {
            if (col.gameObject == gameObject || col.gameObject == owner) continue;
            
            float dist = Vector2.Distance(transform.position, col.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = col.gameObject;
            }
        }
        
        return nearest;
    }
    
    protected virtual bool IsValidTarget(GameObject target)
    {
        if (target == null || config == null) return false;
        
        float dist = Vector2.Distance(transform.position, target.transform.position);
        return dist <= detectionRange;
    }
    
    protected virtual void RotateTurretToward(Vector3 targetPosition)
    {
        if (turretTransform == null) return;
        
        Vector2 direction = (targetPosition - turretTransform.position).normalized;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // Smooth rotation
        float currentAngle = turretTransform.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turretRotationSpeed * Time.deltaTime);
        
        turretTransform.rotation = Quaternion.Euler(0, 0, newAngle);
        
        // Flip turret sprite if rotated past 90 degrees (facing left/backward)
        SpriteRenderer turretSprite = turretTransform.GetComponent<SpriteRenderer>();
        if (turretSprite != null)
        {
            // Normalize angle to -180 to 180 range
            float normalizedAngle = newAngle;
            if (normalizedAngle > 180f) normalizedAngle -= 360f;
            
            // Flip if angle is between 90 and 270 (or -90 to -270)
            turretSprite.flipY = normalizedAngle > 90f || normalizedAngle < -90f;
        }
    }
    
    protected virtual void FireProjectile()
    {
        if (config == null || config.constructAbilities == null || config.constructAbilities.Count == 0)
        {
            return;
        }
        
        // Find projectile ability config
        ConstructAbilityConfig projectileAbility = null;
        foreach (var ability in config.constructAbilities)
        {
            if (ability.abilityType == ConstructAbilityConfig.AbilityType.Projectile)
            {
                projectileAbility = ability;
                break;
            }
        }
        
        if (projectileAbility == null || projectileAbility.projectileConfig == null)
        {
            Debug.LogWarning($"{AbilityPipelineTag} AutoTurret.FireProjectile aborted: no projectile config on {gameObject.name}");
            Debug.LogWarning($"[AutoTurret] No projectile config found for turret");
            return;
        }
        
        ProjectileConfig projConfig = projectileAbility.projectileConfig;
        
        // Determine spawn position (use launch zone if available, otherwise turret or construct position)
        Vector3 spawnPosition = transform.position;
        Vector3 launchZonePosition = Vector3.zero;
        if (launchZoneTransform != null)
        {
            launchZonePosition = launchZoneTransform.position;
        }
        else if (turretTransform != null)
        {
            spawnPosition = turretTransform.position;
        }
        else
        {
            spawnPosition = transform.position;
        }
        
        // Determine direction (use turret rotation if available)
        Vector3 direction;
        if (turretTransform != null)
        {
            float angle = turretTransform.eulerAngles.z * Mathf.Deg2Rad;
            direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }
        else if (currentTarget != null)
        {
            //create a direction from launch zone to target, taking into account the position of the launch zone relative to the turret's position
            direction = (currentTarget.transform.position - spawnPosition - launchZonePosition).normalized;
        }
        else
        {
            direction = Vector3.right;
        }

        OnConstructAbilityUsed();

        Vector3 projectileSpawnPosition = launchZoneTransform != null ? launchZoneTransform.position : spawnPosition;
        int salvoSize = Mathf.Max(1, projConfig.salvoSize);
        if (salvoSize == 1)
        {
            SpawnTurretProjectiles(projConfig, projectileSpawnPosition, direction);
        }
        else
        {
            StartCoroutine(FireSalvo(projConfig, projectileSpawnPosition, direction, salvoSize));
        }
    }

    private IEnumerator FireSalvo(ProjectileConfig projectileConfig, Vector3 spawnPosition, Vector3 baseDirection, int salvoSize)
    {
        uint seed = (uint)Time.frameCount;
        for (int i = 0; i < salvoSize; i++)
        {
            if (i > 0)
                yield return new WaitForSeconds(projectileConfig.salvoInterval);

            float angle = GetDeterministicSalvoAngle(projectileConfig.salvoAngle, seed, i);
            Vector3 direction = Quaternion.Euler(0f, 0f, angle) * baseDirection;
            SpawnTurretProjectiles(projectileConfig, spawnPosition, direction);
        }
    }

    private void SpawnTurretProjectiles(ProjectileConfig projectileConfig, Vector3 spawnPosition, Vector3 direction)
    {
        // ProjectileSpawner handles projectileCount; salvoSize is handled by FireSalvo above.
        ProjectileSpawner.SpawnProjectiles(
            projectileConfig,
            spawnPosition,
            direction,
            owner,
            1f,
            null,
            muzzleFlashEntity: gameObject
        );
    }

    private static float GetDeterministicSalvoAngle(float maxAngle, uint seed, int salvoIndex)
    {
        if (maxAngle <= 0f)
            return 0f;

        uint hash = seed ^ ((uint)(salvoIndex + 1) * 0x9E3779B9u);
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        hash ^= hash >> 16;

        float normalized = (hash & 0x00FFFFFFu) / 16777215f;
        return normalized * maxAngle;
    }
}
