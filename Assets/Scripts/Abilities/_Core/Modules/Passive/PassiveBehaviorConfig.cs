using UnityEngine;

public enum PassiveTrigger
{
    OnAttack,
    OnDamageDealt,
    OnDamaged,
    OnBlock,
    OnHeal,
    OnKill,
    OnSpecificAbilityCast
}

public enum PassiveActionTarget
{
    Self,
    EventTarget
}

[CreateAssetMenu(fileName = "PassiveBehavior", menuName = "Abilities/Passives/Passive Behavior")]
public class PassiveBehaviorConfig : PassiveAbilityConfigBase
{
    [Header("Trigger")]
    public PassiveTrigger trigger;
    [Min(1)] public int triggerCount = 1;
    public AbilityDataConfig requiredAbility;
    public string requiredDamageType;

    [Header("Actions")]
    public PassiveActionTarget actionTarget = PassiveActionTarget.EventTarget;
    public EffectData effects = new EffectData();
    [Min(0f)] public float damage;
    public string damageType = "Physical";
    [Min(0f)] public float healing;
    public AbilityDataConfig abilityToProc;
    [Min(0.1f)] public float triggeredAbilityLifetime = 5f;

    public override PassiveAbility CreateRuntime(GameObject owner)
    {
        return owner != null ? owner.AddComponent<PassiveBehavior>() : null;
    }
}