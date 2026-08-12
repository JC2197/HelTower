using UnityEngine;

/// <summary>
/// Reusable on-hit visual/audio feedback ("Effects Module") for any hitbox-based
/// sub-ability (projectile, melee, area, explosion, aura).
/// </summary>
[System.Serializable]
public class HitFeedbackModule
{
    [Tooltip("Particle effect spawned on hit.")]
    public GameObject hitEffectPrefab;

    [Tooltip("Sound effect played on hit.")]
    public AudioClip hitSound;

    [Tooltip("Flash color when hitting enemies (requires DamageFlash material on the target sprite).")]
    public Color hitFlashColor = Color.white;
}
