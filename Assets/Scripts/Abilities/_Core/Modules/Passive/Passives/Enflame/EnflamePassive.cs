using UnityEngine;

/// <summary>
/// Applies configured bonus damage whenever an attack ability successfully deals damage.
/// </summary>
public class EnflamePassive : PassiveAbility
{
    private EnflamePassiveConfig enflameConfig;

    private PlayerController player;

    public override void Initialize(
        AbilityDataConfig abilityConfig,
        DataDrivenAbility source,
        PassiveConfig runtimePassiveConfig = null,
        PassiveAbilityConfigBase runtimePassiveAsset = null)
    {
        base.Initialize(abilityConfig, source, runtimePassiveConfig, runtimePassiveAsset);

        enflameConfig = runtimePassiveAsset as EnflamePassiveConfig;
        if (enflameConfig == null)
        {
            Debug.LogError("[EnflamePassive] Missing or wrong passive asset. Expected EnflamePassiveConfig.");
            enabled = false;
        }
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogError($"[EnflamePassive] No PlayerController component found on {gameObject.name}! Enflame requires PlayerController.");
            enabled = false;
            return;
        }
    }

    private void HandleAttackDamageDealt(AbilityDataConfig abilityConfig, GameObject target, float damageAmount, string damageType)
    {
        if (enflameConfig == null)
            return;

        if (target == null)
            return;

        IDamageable damageable = target.GetComponent<IDamageable>() ?? target.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;

        damageable.TakeDamage(enflameConfig.DamageDealt, enflameConfig.DamageType);
        if (enflameConfig.EnflameOnhitEffectPrefab != null)
        {
            GameObject effectInstance = Instantiate(enflameConfig.EnflameOnhitEffectPrefab, target.transform.position, Quaternion.identity);
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
