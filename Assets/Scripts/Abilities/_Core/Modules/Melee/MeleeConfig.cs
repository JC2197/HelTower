using UnityEngine;
using System;
/// <summary>
/// Configuration for melee attacks. The shared <see cref="HitboxConfig"/> prefab is instantiated
/// at spawn time, oriented toward the attack direction. Its animator controls when the collider is active.
/// </summary>
[System.Serializable]
public class MeleeConfig
{
    [Header("Hitbox")]
    [Tooltip("Shared hitbox configuration: prefab (the meleeFX), scale, hit layers, damage, effects, knockback, etc. The prefab should contain a Collider2D driven by its own Animator.")]
    public HitboxConfig hitbox = new HitboxConfig();

    [Header("MeleeFX")]
    [Tooltip("Distance from the character center at which the meleeFX spawns, along the attack direction.")]
    public float meleeFXRadiusDistance = 0.5f;

    [Tooltip("Speed at which the meleeFX travels after spawning (0 = stationary).")]
    public float meleeFXSpeed = 0f;

    [Tooltip("Can hit the same target multiple times in one attack?")]
    public bool allowMultiHit = false;

    [Tooltip("Sound played when the melee attack is executed.")]
    public AudioClip meleeSound;
}
