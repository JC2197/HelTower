using UnityEngine;
using System;

[CreateAssetMenu(fileName = "NumberOfAttacksPassiveConfig", menuName = "Abilities/Passives/Number Of Attacks Passive Config")]
public class NumberOfAttacksPassiveConfig : PassiveAbilityConfigBase
{
    [Header("Attack Counter")]
    [Tooltip("Optional ability fired whenever the required number of attacks is reached.")]
    [SerializeField] private AbilityDataConfig abilityToProc;
    [SerializeField] private int numberOfAttacksNeeded = 5;

    [Header("Optional Bonus Damage")]
    [Tooltip("Optional bonus damage applied whenever one of the owner's attacks deals damage.")]
    [SerializeField] private AddDamagePassiveConfig addDamageConfig;

    public AbilityDataConfig AbilityToProc => abilityToProc;
    public int NumberOfAttacksNeeded => Mathf.Max(1, numberOfAttacksNeeded);
    public AddDamagePassiveConfig AddDamageConfig => addDamageConfig;
}