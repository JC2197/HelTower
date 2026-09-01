using UnityEngine;

/// <summary>
/// Configuration for explosion effects - instant damage in an area
/// Similar to AreaConfig but instantaneous
/// </summary>
[System.Serializable]
public class ExplosionConfig
{
    [Header("Hitbox")]
    [Tooltip("Complete explosion prefab and hit processing. The prefab supplies collider geometry, visuals, and animation.")]
    public HitboxConfig hitbox = new HitboxConfig();

    [Header("Single-Target Mode (Point & Click)")]
    [Tooltip("When enabled, this ability skips the area/collider overlap check entirely. Instead " +
        "it finds the single nearest living enemy near the target position, applies damage to it " +
        "alone, and attaches the hitbox prefab directly to that enemy so its visual plays on them. Ideal for autocast bolt/zap-style " +
        "abilities that should always land on one specific enemy rather than running an area check.")]
    public bool singleTargetMode = false;

    [Tooltip("Radius around the target position to search for the nearest living enemy when " +
        "singleTargetMode is enabled. Falls back to activationRange, then a default of 3 units, when left at 0.")]
    public float singleTargetSearchRadius = 0f;

    [Header("Delay")]
    [Min(0f)]
    public float timeDelay = 0f;
    public GameObject delayEffectPrefab;
    
    [Header("Activation")]
    [Tooltip("Range within which explosion can be activated (0 = unlimited)")]
    public float activationRange = 0f;

    [Header("Salvo Settings")]
    [Tooltip("When enabled, the explosion will fire in multiple salvos instead of a single instance.")]
    public bool salvos;

    [Tooltip("Number of salvos to fire when salvos are enabled.")]
    public int salvoAmount;
    public int multiCastAmount;
    public float salvoDelay;
    [Tooltip("Moves the salvo")]
    public bool salvoOffset;
    public float salvoOffsetDistance;
    public bool salvoOffsetTarget;
    public bool salvoOffsetMouse;
    public bool salvoRandom;
    public bool salvoRadial;
}
