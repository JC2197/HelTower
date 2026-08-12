using UnityEngine;
using System;

[CreateAssetMenu(fileName = "RocketsPassiveConfig", menuName = "Abilities/Passives/Rockets Passive Config")]
public class RocketsPassiveConfig : PassiveAbilityConfigBase
{
    [Header("Rockets Settings")]
    [Tooltip("Ability fired whenever the required number of attacks is reached.")]
    [SerializeField] private AbilityDataConfig rocketAbility;
    [SerializeField] private int numberOfAttacksNeeded = 5;

    public AbilityDataConfig RocketAbility => rocketAbility;
    public int NumberOfAttacksNeeded => Mathf.Max(1, numberOfAttacksNeeded);
}