using UnityEngine;

/// <summary>
/// Reusable knockback configuration for any hitbox-based sub-ability
/// (projectile, melee, area, explosion, aura).
/// </summary>
[System.Serializable]
public class KnockbackModule
{
    [Tooltip("Apply knockback to hit targets?")]
    public bool enabled = false;

    [Tooltip("Knockback force")]
    public float force = 5f;

    [Tooltip("Knockback direction relative to attacker (0 = away from attacker, 1 = attack/travel direction).")]
    [Range(0f, 1f)]
    public float directionality = 1f;
}
