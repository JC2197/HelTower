using UnityEngine;

/// <summary>
/// Configuration for explosion effects - instant damage in an area
/// Similar to AreaConfig but instantaneous
/// </summary>
[System.Serializable]
public class ExplosionConfig
{
    [Header("Hitbox")]
    [Tooltip("Shared hitbox config: scale, hit layers, damage, weapon damage, knockback, pull, on-hit effects, life steal, and hit feedback. The explosion uses shape/dimensions below for its area rather than a collider prefab.")]
    public HitboxConfig hitbox = new HitboxConfig();

    [Header("Area Settings")]
    [Tooltip("Shape of the explosion area. Ignored when singleTargetMode is enabled.")]
    public ExplosionShape shape = ExplosionShape.Circle;
    
    [Tooltip("Size of explosion (radius for circle, width/height for rectangle). Ignored when singleTargetMode is enabled.")]
    public Vector2 dimensions = new Vector2(3f, 3f);

    [Header("Single-Target Mode (Point & Click)")]
    [Tooltip("When enabled, this ability skips the area/collider overlap check entirely. Instead " +
        "it finds the single nearest living enemy near the target position, applies damage to it " +
        "alone, and attaches explosionEffectPrefab (and delayEffectPrefab) directly to that enemy so " +
        "the visual plays on them and is destroyed afterward. Ideal for autocast bolt/zap-style " +
        "abilities that should always land on one specific enemy rather than running an area check.")]
    public bool singleTargetMode = false;

    [Tooltip("Radius around the target position to search for the nearest living enemy when " +
        "singleTargetMode is enabled. Falls back to activationRange, then a default of 3 units, when left at 0.")]
    public float singleTargetSearchRadius = 0f;
    
    [Header("Effects")]
    [Tooltip("Delay in seconds before the explosion triggers (0 = instant)")]
    public float timeDelay = 0f;
    
    [Tooltip("Prefab displayed during the delay period before the explosion fires")]
    public GameObject delayEffectPrefab;
    
    [Tooltip("Particle effect to spawn at explosion center")]
    public GameObject explosionEffectPrefab;
    
    [Tooltip("Sound to play on explosion")]
    public AudioClip explosionSound;
    
    [Header("Activation")]
    [Tooltip("Range within which explosion can be activated (0 = unlimited)")]
    public float activationRange = 0f;
}

public enum ExplosionShape
{
    Circle,
    Rectangle
}
