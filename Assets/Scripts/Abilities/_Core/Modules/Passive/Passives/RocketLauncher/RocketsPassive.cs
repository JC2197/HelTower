using UnityEngine;

/// <summary>
/// Fires a configured ability after the player performs a set number of attacks.
/// </summary>
public class RocketsPassive : PassiveAbility
{
    private RocketsPassiveConfig rocketsConfig;
    private PlayerController player;
    private DataDrivenAbility rocketAbility;
    private int attackCounter;

    public override void Initialize(
        AbilityDataConfig abilityConfig,
        DataDrivenAbility source,
        PassiveConfig runtimePassiveConfig = null,
        PassiveAbilityConfigBase runtimePassiveAsset = null)
    {
        base.Initialize(abilityConfig, source, runtimePassiveConfig, runtimePassiveAsset);

        rocketsConfig = runtimePassiveAsset as RocketsPassiveConfig;
        if (rocketsConfig == null)
        {
            Debug.LogError("[RocketsPassive] Missing or wrong passive asset. Expected RocketsPassiveConfig.");
            enabled = false;
            return;
        }

        if (rocketsConfig.RocketAbility == null)
        {
            Debug.LogError("[RocketsPassive] No rocket ability configured.");
            enabled = false;
            return;
        }

        rocketAbility = gameObject.AddComponent<DataDrivenAbility>();
        rocketAbility.SetAbilityReference(new AbilityReference(rocketsConfig.RocketAbility));
        rocketAbility.ConfigureAsTriggeredProjectile();
        rocketAbility.InitializeAbility();
        rocketAbility.RebuildConfigModifiers();
    }

    private void Awake()
    {
        attackCounter = 0;
        player = GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogError($"[RocketsPassive] No PlayerController component found on {gameObject.name}! Rockets requires PlayerController.");
            enabled = false;
            return;
        }
    }

    private void HandleAttack(AbilityDataConfig attackConfig)
    {
        if (rocketsConfig == null || rocketAbility == null || attackConfig == rocketsConfig.RocketAbility)
            return;

        attackCounter++;
        if (attackCounter >= rocketsConfig.NumberOfAttacksNeeded)
        {
            attackCounter = 0;
            rocketAbility.FireTriggeredProjectile();
        }
    }

    private void OnEnable()
    {
        player = GetComponent<PlayerController>();
        if (player != null)
            player.OnAttack += HandleAttack;
    }


    private void OnDisable()
    {
        if (player != null)
            player.OnAttack -= HandleAttack;
    }

    private void OnDestroy()
    {
        if (rocketAbility != null)
            Destroy(rocketAbility);
    }
}
