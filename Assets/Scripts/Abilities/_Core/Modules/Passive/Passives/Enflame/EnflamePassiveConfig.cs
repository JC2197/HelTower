using UnityEngine;

[CreateAssetMenu(fileName = "EnflamePassiveConfig", menuName = "Abilities/Passives/Enflame Passive Config")]
public class EnflamePassiveConfig : PassiveAbilityConfigBase
{
    [Header("Enflame Settings")]
    [SerializeField] private float damageDealt = 5f;

    [DamageTypeDropdown]
    [SerializeField] private string damageType = "Fire";

    [SerializeField] private GameObject enflameOnhitEffectPrefab;

    public float DamageDealt => damageDealt;
    public string DamageType => damageType;
    public GameObject EnflameOnhitEffectPrefab => enflameOnhitEffectPrefab;
}
