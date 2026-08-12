using UnityEngine;
using System;

/// <summary>
/// Configuration for custom passive effects. The shared <see cref="CustomPassiveEffectConfig"/> prefab is instantiated
/// at spawn time.
/// </summary>

[Serializable]
public class PassiveConfig
{
    [Tooltip("Particle effect or animation object spawned when this passive is active")]
    [SerializeField] private GameObject passiveVisualsPrefab;
    [Tooltip("Passive ScriptableObject asset that defines runtime behavior and modifiable fields")]
    [SerializeField] private PassiveAbilityConfigBase passiveAbility;

    public GameObject PassiveVisualsPrefab => passiveVisualsPrefab;
    public PassiveAbilityConfigBase PassiveAbility => passiveAbility;
}

