using UnityEngine;

/// <summary>
/// Reusable pull (vacuum) configuration for any hitbox-based sub-ability
/// (projectile, melee, area, explosion, aura).
/// </summary>
[System.Serializable]
public class PullModule
{
    [Tooltip("Pull hit targets toward the hitbox origin?")]
    public bool enabled = false;

    [Tooltip("Pull force applied toward the hitbox origin.")]
    public float force = 5f;
}
