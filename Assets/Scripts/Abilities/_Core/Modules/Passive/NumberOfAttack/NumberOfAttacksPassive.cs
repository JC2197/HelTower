using UnityEngine;
using System;

/// <summary>
/// Fires a configured ability after the player performs a set number of attacks.
/// </summary>
public class NumberOfAttacksPassive : PassiveAbility
{
    private NumberOfAttacksPassiveConfig config;
    private PlayerController player;
    private AddDamagePassiveConfig addDamageConfig;
    private DataDrivenAbility abilityToProc;
    private int attackCounter;

    public override void Initialize(
        AbilityDataConfig abilityConfig,
        DataDrivenAbility source,
        PassiveConfig runtimePassiveConfig = null,
        PassiveAbilityConfigBase runtimePassiveAsset = null)
    {
        base.Initialize(abilityConfig, source, runtimePassiveConfig, runtimePassiveAsset);

        config = runtimePassiveAsset as NumberOfAttacksPassiveConfig;
        if (config == null)
        {
            Debug.LogError("[NumberOfAttacksPassive] Missing or wrong passive asset. Expected NumberOfAttacksPassiveConfig.");
            enabled = false;
            return;
        }

        addDamageConfig = config.AddDamageConfig;

        if (config.AbilityToProc != null)
        {
            abilityToProc = gameObject.AddComponent<DataDrivenAbility>();
            abilityToProc.SetAbilityReference(new AbilityReference(config.AbilityToProc));
            abilityToProc.ConfigureAsTriggeredProjectile();
            abilityToProc.InitializeAbility();
            abilityToProc.RebuildConfigModifiers();
        }
    }

    private void Awake()
    {
        attackCounter = 0;
        player = GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogError($"[NumberOfAttacksPassive] No PlayerController component found on {gameObject.name}! NumberOfAttacksPassive   requires PlayerController.");
            enabled = false;
            return;
        }
    }

    private void HandleAttack(AbilityDataConfig attackConfig)
    {
        if (config == null || attackConfig == config.AbilityToProc)
            return;

        attackCounter++;
        if (attackCounter >= config.NumberOfAttacksNeeded)
        {
            attackCounter = 0;
            abilityToProc?.TryUseAbility();
        }
    }

    private void HandleAttackDamageDealt(AbilityDataConfig attackConfig, GameObject target, float damageAmount, string damageType)
    {
        if (addDamageConfig == null || target == null || damageAmount <= 0f)
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
        {
            player.OnAttack += HandleAttack;
            player.OnAttackDamage += HandleAttackDamageDealt;
        }
    }


    private void OnDisable()
    {
        if (player != null)
        {
            player.OnAttack -= HandleAttack;
            player.OnAttackDamage -= HandleAttackDamageDealt;
        }
    }

    private void OnDestroy()
    {
        if (abilityToProc != null)
            Destroy(abilityToProc);
    }
}
