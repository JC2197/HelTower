using UnityEngine;

[CreateAssetMenu(fileName = "AddDamagePassiveConfig", menuName = "Abilities/Passives/Add Damage Passive Config")]
public class AddDamagePassiveConfig : PassiveAbilityConfigBase
{
    [Header("Enflame Settings")]
    [SerializeField] private float damageDealt = 5f;

    [DamageTypeDropdown]
    [SerializeField] private string damageType = "Fire";

    [SerializeField] private GameObject onHitEffectPrefab;

    public float DamageDealt => damageDealt;
    public string DamageType => damageType;
    public GameObject OnHitEffectPrefab => onHitEffectPrefab;
}
