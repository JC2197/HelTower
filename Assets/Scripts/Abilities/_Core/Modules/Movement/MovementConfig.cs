using UnityEngine;


public enum MovementType
{
    Force = 0,
    DistanceOverTime = 1,
    SpeedOverTime = 2,
    Teleport = 3
}

[System.Serializable]
public class MovementConfig
{
    [Tooltip("If true, applies a force to the Rigidbody2D. Otherwise, sets velocity directly.")]
    public MovementType movementType = MovementType.DistanceOverTime;
    [Tooltip("Amount of force to apply if useForce is true.")]
    public float forceAmount = 10f;
    [Tooltip("Movement speed if not using force.")]
    public float speed = 10f;
    [Tooltip("Maximum distance to move.")]
    public float distance = 5f;
    [Tooltip("Duration of the movement ability in seconds.")]
    public float duration = 0.5f;

    [Tooltip("If true, wait for the ability precast animation to finish before movement actually begins.")]
    public bool activateAfterPrecast = false;
    [Tooltip("If true, deals damage to enemies passed through.")]
    public bool passThruDamage = false;
    [Tooltip("Amount of damage dealt when passing through enemies.")]
    public float passthruDamageAmount = 0f;
    [Tooltip("Type of damage dealt (e.g., Physical, Fire, etc.)")]
    public string damageTypeName = "Physical";
    // Add more fields as needed (e.g., direction, cooldown, etc.)
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