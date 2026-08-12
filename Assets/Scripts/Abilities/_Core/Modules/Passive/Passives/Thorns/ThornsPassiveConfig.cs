using UnityEngine;
using System;

[CreateAssetMenu(fileName = "ThornsPassiveConfig", menuName = "Abilities/Passives/Thorns Passive Config")]
public class ThornsPassiveConfig : PassiveAbilityConfigBase
{
    [Header("Thorns Settings")]
    [Tooltip("Damage type dealt by thorns (damage value comes from Thorns stat)")]
    [DamageTypeDropdown]
    [SerializeField] private string thornsDamageType = "Piercing";
    [SerializeField] private float thornsBaseDamage = 1f;
    [Header("Visual Effects")]
    [Tooltip("Particle effect spawned when thorns activates")]
    [SerializeField] private GameObject thornsEffectPrefab;

    [Tooltip("Sound effect played when thorns activates")]
    [SerializeField] private AudioClip thornsSound;
    public string ThornsDamageType => thornsDamageType;
    public float ThornsBaseDamage => thornsBaseDamage;
    public GameObject ThornsEffectPrefab => thornsEffectPrefab;
    public AudioClip ThornsSound => thornsSound;

}
