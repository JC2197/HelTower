using UnityEngine;

/// <summary>
/// Configuration for channeled abilities (flamethrower, freezing ray, etc.).
/// Channel follows mouse cursor and deals continuous damage while button is held.
/// Inline serializable configuration.
/// </summary>
[System.Serializable]
public class ChannelAbilityConfig
{
    [Header("Channel Behavior")]
    [Tooltip("Allow weapon to rotate toward mouse cursor during channel (temporarily enables orbital rotation)")]
    public bool unlockRotation = true;
    
    [Tooltip("Orbital radius when unlockRotation is enabled (distance from player center). 0 = rotate in place.")]
    public float orbitalRadius = 0.4f;
    
    [Tooltip("Flip weapon on Y-axis when aiming left (prevents upside-down appearance during orbital rotation)")]
    public bool flipWeaponOnYAxis = true;
    
    [Tooltip("Energy consumed per second while channeling")]
    public float energyPerSecond = 5f;
    
    [Tooltip("How often energy is consumed (0.25 = consume energy 4 times per second)")]
    [Range(0.01f, 1f)]
    public float energyTickRate = 0.25f;
    
    [Header("Channel Object")]
    [Tooltip("Prefab spawned at weapon tip (should have hitbox collider and particle effects)")]
    public GameObject channelObjectPrefab;
    
    [Tooltip("Scale multiplier for channel object")]
    [Range(0.1f, 5f)]
    public float scale = 1f;
    
    [Header("Damage")]
    [Tooltip("Damage dealt per tick")]
    public float damage = 10f;
    
    [Tooltip("Type of damage dealt")]
    [DamageTypeDropdown]
    public string damageType = "Physical";
    
    [Tooltip("How often damage is applied (0.25 = damage 4 times per second)")]
    [Range(0.01f, 2f)]
    public float damageTickRate = 0.25f;
    
    [Tooltip("Layers the channel can hit")]
    public LayerMask hitLayers = -1;
    
    [Header("Animations")]
    [Tooltip("Animation played when channel starts")]
    public string channelStartAnimationName = "ChannelStart";
    
    [Tooltip("Animation looped while channeling")]
    public string channelAnimationName = "Channel";
    
    [Tooltip("Animation played when channel ends")]
    public string channelEndAnimationName = "ChannelEnd";
    
    [Tooltip("Animation to return to after channel ends (typically 'Idle')")]
    public string weaponIdleAnimationName = "Idle";
    
    [Header("Visual Effects")]
    [Tooltip("Particle effect at weapon origin when channeling starts")]
    public GameObject muzzleFlashPrefab;
    
    [Tooltip("Enable light effect at channel origin")]
    public bool enableMuzzleLight = false;
    
    [Tooltip("Color of the muzzle light")]
    public Color muzzleLightColor = Color.white;
    
    [Tooltip("Intensity of the muzzle light")]
    [Range(0f, 10f)]
    public float muzzleLightIntensity = 2f;
    
    [Tooltip("Range/radius of the muzzle light")]
    [Range(0f, 10f)]
    public float muzzleLightRange = 2f;
    
    [Header("Audio")]
    [Tooltip("Sound effect when channel starts")]
    public AudioClip channelStartSound;
    
    [Tooltip("Looping sound effect while channeling")]
    public AudioClip channelLoopSound;
    
    [Tooltip("Sound effect when channel ends")]
    public AudioClip channelEndSound;
    
    [Header("Hit Effects")]
    [Tooltip("Flash color when hitting enemies (requires DamageFlash material on enemy sprite)")]
    public Color hitFlashColor = Color.white;
    
    [Header("Status Effects")]
    [Tooltip("Status effects applied to targets on damage tick")]
    public EffectData onHitEffects = new EffectData();

    [Header("Life Steal")]
    [Tooltip("Heal the ability owner on each channel damage tick.")]
    public LifeStealConfig lifeSteal = new LifeStealConfig();
}
