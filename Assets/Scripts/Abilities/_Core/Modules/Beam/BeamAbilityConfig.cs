using UnityEngine;
using UnityEngine.Serialization;

public enum BeamTargetingMode
{
    Cursor,
    AutoTargetEnemy
}

/// <summary>
/// Configuration for beam abilities (lasers, lightning, etc.).
/// Inline serializable configuration.
/// </summary>
[System.Serializable]
public class BeamAbilityConfig
{
    [Header("Beam Rendering")]
    [Tooltip("Prefab with a BeamRenderer component used for beam visuals.")]
    public GameObject beamRendererPrefab;

    [Tooltip("How beam endpoint is chosen.")]
    public BeamTargetingMode targetingMode = BeamTargetingMode.Cursor;

    [Tooltip("If auto-target finds no enemy, use cursor endpoint instead.")]
    public bool fallbackToCursorWhenNoEnemy = true;

    [Tooltip("Duration for non-hold beams before auto-stop.")]
    [Range(0.05f, 2f)]
    public float singleShotDuration = 0.35f;

    [Tooltip("Width/thickness of the beam (vertical scale)")]
    public float beamWidth = 0.5f;

    [Tooltip("Color tint for the beam")]
    public Color beamColor = Color.white;

    [Header("Beam Behavior")]
    [Tooltip("Maximum distance the beam can reach")]
    public float maxBeamDistance = 20f;

    [Tooltip("Number of simultaneous beams emitted per activation")]
    [Min(1)]
    public int beamAmount = 1;

    [Tooltip("Total angle spread for multi-beam emission")]
    public float multiBeamAngle = 15f;

    [Tooltip("Enable beam chaining to additional unique targets")]
    public bool chain = false;

    [Tooltip("Number of additional chain hops after the first target")]
    [Min(0)]
    public int chainAmount = 0;

    [Tooltip("Value applied each time the beam hits (single-shot: once, channeled: each tick)")]
    [FormerlySerializedAs("damagePerSecond")]
    public float value = 50f;

    [Tooltip("If enabled, targets on Heal Targets layer mask are healed instead of damaged")]
    public bool canHeal = false;

    [Tooltip("Targets on these layers receive healing when Can Heal is enabled")]
    public LayerMask healTargets = 0;

    [Tooltip("How many hit ticks occur per second for channeled beams")]
    public float hitsPerSecond = 2f;

    [Tooltip("Type of damage dealt")]
    [DamageTypeDropdown]
    public string damageTypeName = "Energy";

    [Tooltip("Layers the beam can hit")]
    public LayerMask hitLayers = -1;

    [Header("On Hit Status Effects")]
    [Tooltip("Effects applied to targets whenever this beam lands a hit tick")]
    public EffectData onHitEffects = new EffectData();

    [Header("Life Steal")]
    [Tooltip("Heal the ability owner on each beam hit tick.")]
    public LifeStealConfig lifeSteal = new LifeStealConfig();

    [Header("Hold to Fire")]
    [Tooltip("Can hold button to continuously fire beam")]
    public bool canHoldToFire = true;

    [Tooltip("Energy cost per second while beam is active (0 = no cost)")]
    public float channelCostPerSecond = 5f;

    [Header("Enemy Tracking")]
    [Tooltip("Radius around origin to search for enemies when auto-targeting")]
    [Range(1f, 10f)]
    public float trackingRadius = 3f;

    [Header("Muzzle Effect")]
    [Tooltip("Particle effect at beam origin when firing")]
    public GameObject muzzleFlashPrefab;

    [Tooltip("Enable light effect at beam origin")]
    public bool enableMuzzleLight = false;

    [Tooltip("Color of the muzzle light")]
    public Color muzzleLightColor = Color.white;

    [Tooltip("Intensity of the muzzle light")]
    [Range(0f, 10f)]
    public float muzzleLightIntensity = 2f;

    [Tooltip("Range/radius of the muzzle light")]
    [Range(0f, 10f)]
    public float muzzleLightRange = 2f;

    [Tooltip("Duration of the muzzle light fade")]
    [Range(0f, 1f)]
    public float muzzleLightDuration = 0.2f;

    [Header("Impact Effect")]
    [Tooltip("Animator-driven effect prefab spawned at beam impact point")]
    public GameObject impactEffectPrefab;

    [Tooltip("Animation to play on impact")]
    public string impactAnimationName = "Impact";

    [Tooltip("Particle effect at beam impact point")]
    public GameObject impactParticlePrefab;

    [Tooltip("Sound effect on beam activation")]
    public AudioClip beamSound;

    [Tooltip("Sound effect on impact")]
    public AudioClip impactSound;

    [Tooltip("Flash color when hitting enemies (requires DamageFlash material on enemy sprite)")]
    public Color hitFlashColor = Color.white;
}
