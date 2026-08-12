using System;
using UnityEngine;

/// <summary>
/// Current state of the enemy
/// </summary>
public enum EnemyState
{
    Patrol,
    Chase,
    Attack,
    Strafe,
    Retreat,
    Kite
}

/// <summary>
/// Available actions an enemy can perform
/// </summary>
public enum EnemyActionType
{
    Chase,
    Retreat,
    Strafe,
    Patrol,
    Attack
}

/// <summary>
/// Configuration for a single enemy action with contextual parameters
/// </summary>
[Serializable]
public class EnemyActionConfig
{
    [Tooltip("The type of action this enemy can perform")]
    public EnemyActionType actionType;
    
    [Header("Trigger Conditions")]
    [Tooltip("Minimum distance from target to trigger this action (-1 = no minimum)")]
    public float minDistance = -1f;
    
    [Tooltip("Maximum distance from target to trigger this action (-1 = no maximum)")]
    public float maxDistance = -1f;
    
    [Tooltip("Health percentage threshold to trigger this action (0-100, -1 = always available)")]
    public float healthPercentThreshold = -1f;
    
    [Header("Movement Parameters (Chase/Retreat/Strafe/Patrol)")]
    [Tooltip("Speed multiplier for movement actions (1.0 = normal speed)")]
    public float movementSpeedMultiplier = 1f;
    
    [Tooltip("Duration to perform this movement action before re-evaluating (seconds)")]
    public float movementDuration = 2f;
    
    [Header("Strafe Parameters")]
    [Tooltip("For Strafe: maintain this distance from target while strafing")]
    public float strafeDistance = 5f;
    
    [Tooltip("For Strafe: direction to strafe (true = clockwise, false = counter-clockwise)")]
    public bool strafeClockwise = true;
    
    [Header("Patrol Parameters")]
    [Tooltip("For Patrol: radius around spawn point to patrol")]
    public float patrolRadius = 10f;
    
    [Tooltip("For Patrol: time to wait at each patrol point")]
    public float patrolWaitTime = 2f;
    
    [Header("Attack Parameters")]
    [Tooltip("Minimum time between attacks (seconds)")]
    public float attackCooldownMin = 1f;
    
    [Tooltip("Maximum time between attacks (seconds)")]
    public float attackCooldownMax = 3f;
    
    [Tooltip("Which ability index to use for this attack (0 = first ability, 1 = second, etc.)")]
    public int abilityIndex = 0;
    
    /// <summary>
    /// Check if this action is available given current conditions
    /// </summary>
    public bool IsAvailable(float distanceToTarget, float currentHealthPercent, float weaponAbilityRange)
    {
        // Chase: only available if outside weapon ability range
        if (actionType == EnemyActionType.Chase)
        {
            return distanceToTarget > weaponAbilityRange;
        }
        
        // Other actions: check distance requirements
        if (minDistance >= 0 && distanceToTarget < minDistance) return false;
        if (maxDistance >= 0 && distanceToTarget > maxDistance) return false;
        
        // Check health requirements
        if (healthPercentThreshold >= 0 && currentHealthPercent > healthPercentThreshold) return false;
        
        return true;
    }
    
    /// <summary>
    /// Get random attack cooldown within configured range
    /// </summary>
    public float GetRandomAttackCooldown()
    {
        return UnityEngine.Random.Range(attackCooldownMin, attackCooldownMax);
    }
}
