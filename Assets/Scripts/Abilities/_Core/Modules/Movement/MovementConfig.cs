using UnityEngine;


public enum MovementType
{
    DistanceOverTime = 1,
    SpeedOverTime = 2,
    Teleport = 3
}

[System.Serializable]
public class MovementConfig
{
    public MovementType movementType = MovementType.DistanceOverTime;
    [Tooltip("Smoothly accelerates and decelerates timed movement while preserving total distance.")]
    public bool lerp = false;
    [Tooltip("Movement speed for Speed Over Time.")]
    public float speed = 10f;
    [Tooltip("Maximum distance to move.")]
    public float distance = 5f;
    [Tooltip("Duration of the movement ability in seconds.")]
    public float duration = 0.5f;

    [Tooltip("If true, wait for the ability precast animation to finish before movement actually begins.")]
    public bool activateAfterPrecast = false;
    public bool towardMouse;
    public bool awayFromMouse;

    [Header("Dash / Evade")]
    [Tooltip("When true, the character becomes invulnerable (evades all attacks) during this movement.")]
    public bool isDashing = false;

    [Tooltip("Prefab spawned at both start and end positions during teleport.")]
    public GameObject teleportAnimationPrefab;
    [Tooltip("When true, all SpriteRenderers on the character are disabled during teleport.")]
    public bool disappearDuringTeleport = true;
    public AudioClip dashSound;
}