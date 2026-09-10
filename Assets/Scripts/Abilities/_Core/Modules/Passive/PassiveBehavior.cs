using UnityEngine;

public class PassiveBehavior : PassiveAbility
{
    private PassiveBehaviorConfig config;
    private PlayerController player;
    private Organism organism;
    private DataDrivenAbility abilityToProc;
    private int triggerProgress;

    public override void Initialize(AbilityDataConfig abilityConfig, DataDrivenAbility source, PassiveConfig runtimePassiveConfig = null, PassiveAbilityConfigBase runtimePassiveAsset = null)
    {
        base.Initialize(abilityConfig, source, runtimePassiveConfig, runtimePassiveAsset);
        config = runtimePassiveAsset as PassiveBehaviorConfig;
        if (config == null)
        {
            enabled = false;
            return;
        }

        if (config.abilityToProc != null)
        {
            abilityToProc = gameObject.AddComponent<DataDrivenAbility>();
            abilityToProc.SetAbilityReference(new AbilityReference(config.abilityToProc));
            abilityToProc.ConfigureAsTriggeredProjectile();
            abilityToProc.InitializeAbility();
            abilityToProc.RebuildConfigModifiers();
        }
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        organism = GetComponent<Organism>();
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.OnAttack += HandleAttack;
            player.OnAttackDamage += HandleAttackDamage;
            player.OnAbilityCast += HandleAbilityCast;
        }
        if (organism != null)
        {
            organism.OnDamageTaken += HandleDamaged;
            organism.OnBlock += HandleBlock;
            organism.OnHealed += HandleHeal;
        }
        Organism.OnDamageDealt += HandleDamageDealt;
        Organism.OnOrganismKilled += HandleKill;
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.OnAttack -= HandleAttack;
            player.OnAttackDamage -= HandleAttackDamage;
            player.OnAbilityCast -= HandleAbilityCast;
        }
        if (organism != null)
        {
            organism.OnDamageTaken -= HandleDamaged;
            organism.OnBlock -= HandleBlock;
            organism.OnHealed -= HandleHeal;
        }
        Organism.OnDamageDealt -= HandleDamageDealt;
        Organism.OnOrganismKilled -= HandleKill;
    }

    private void OnDestroy()
    {
        if (abilityToProc != null)
            Destroy(abilityToProc);
    }

    private void HandleAttack(AbilityDataConfig ability) => TryTrigger(PassiveTrigger.OnAttack, ability, null, null);
    private void HandleAttackDamage(AbilityDataConfig ability, GameObject target, float damage, string damageType) => TryTrigger(PassiveTrigger.OnDamageDealt, ability, target, damageType);
    private void HandleAbilityCast(AbilityDataConfig ability) => TryTrigger(PassiveTrigger.OnSpecificAbilityCast, ability, null, null);
    private void HandleDamaged(Organism victim, float damage, string damageType, Vector3 position, GameObject attacker) => TryTrigger(PassiveTrigger.OnDamaged, null, attacker, damageType);
    private void HandleBlock(IDamageable victim, float damage, string damageType, Vector3 position, GameObject attacker) => TryTrigger(PassiveTrigger.OnBlock, null, attacker, damageType);
    private void HandleHeal(Organism healed, float amount) => TryTrigger(PassiveTrigger.OnHeal, null, null, null);
    private void HandleDamageDealt(GameObject attacker, float damage, string damageType, GameObject target)
    {
        if (attacker == gameObject)
            TryTrigger(PassiveTrigger.OnDamageDealt, null, target, damageType);
    }
    private void HandleKill(GameObject killer, Organism victim)
    {
        if (killer == gameObject)
            TryTrigger(PassiveTrigger.OnKill, null, victim.gameObject, null);
    }

    private void TryTrigger(PassiveTrigger trigger, AbilityDataConfig ability, GameObject eventTarget, string damageType)
    {
        if (config == null || config.trigger != trigger)
            return;
        if (config.requiredAbility != null && config.requiredAbility != ability)
            return;
        if (!string.IsNullOrEmpty(config.requiredDamageType) && !string.Equals(config.requiredDamageType, damageType, System.StringComparison.OrdinalIgnoreCase))
            return;

        triggerProgress++;
        if (triggerProgress < Mathf.Max(1, config.triggerCount))
            return;

        triggerProgress = 0;
        GameObject target = config.actionTarget == PassiveActionTarget.EventTarget && eventTarget != null ? eventTarget : gameObject;
        config.effects?.ApplyEffects(target, gameObject, gameObject);

        IDamageable damageable = target.GetComponent<IDamageable>() ?? target.GetComponentInParent<IDamageable>();
        if (config.damage > 0f && damageable != null)
            damageable.TakeDamage(config.damage, config.damageType, transform.position);

        if (config.healing > 0f)
            organism?.Heal(config.healing);

        abilityToProc?.TryUseAbility();
    }
}