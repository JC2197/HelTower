using System;
using UnityEngine;

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
    [Tooltip("Health percentage threshold to trigger this action (0-100, -1 = always available)")]
    public float healthPercentThreshold = -1f;

    [Header("Movement Parameters (Chase/Retreat/Strafe/Patrol)")]
    [Tooltip("Speed multiplier for movement actions (1.0 = normal speed)")]
    public float movementSpeedMultiplier = 1f;

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
}
