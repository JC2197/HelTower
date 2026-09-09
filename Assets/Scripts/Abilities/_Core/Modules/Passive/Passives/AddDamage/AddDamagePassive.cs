using UnityEngine;

/// <summary>
/// Applies configured bonus damage whenever an attack ability successfully deals damage.
/// </summary>
public class AddDamagePassive : PassiveAbility
{
    private AddDamagePassiveConfig addDamageConfig;

    private PlayerController player;

    public override void Initialize(
        AbilityDataConfig abilityConfig,
        DataDrivenAbility source,
        PassiveConfig runtimePassiveConfig = null,
        PassiveAbilityConfigBase runtimePassiveAsset = null)
    {
        base.Initialize(abilityConfig, source, runtimePassiveConfig, runtimePassiveAsset);

        addDamageConfig = runtimePassiveAsset as AddDamagePassiveConfig;
        if (addDamageConfig == null)
        {
            Debug.LogError("[AddDamagePassive] Missing or wrong passive asset. Expected AddDamagePassiveConfig.");
            enabled = false;
        }
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogError($"[AddDamagePassive] No PlayerController component found on {gameObject.name}! AddDamage requires PlayerController.");
            enabled = false;
            return;
        }
    }

    private void HandleAttackDamageDealt(AbilityDataConfig abilityConfig, GameObject target, float damageAmount, string damageType)
    {
        if (addDamageConfig == null)
            return;

        if (target == null)
            return;

        IDamageable damageable = target.GetComponent<IDamageable>() ?? target.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;

        damageable.TakeDamage(addDamageConfig.DamageDealt, addDamageConfig.DamageType);
        if (addDamageConfig.OnHitEffectPrefab != null)
        {
            GameObject effectInstance = Instantiate(addDamageConfig.OnHitEffectPrefab, target.transform.position, Quaternion.identity);
            Destroy(effectInstance, 2f);
        }
    }

    private void OnEnable()
    {
        player = GetComponent<PlayerController>();
        if (player != null)
            player.OnAttackDamage += HandleAttackDamageDealt;
    }

    private void OnDisable()
    {
        if (player != null)
            player.OnAttackDamage -= HandleAttackDamageDealt;
    }
}
