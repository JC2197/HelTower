using UnityEngine;

/// <summary>
/// Configures a stacking damage-over-time debuff. EffectManager owns its runtime state.
/// </summary>
[CreateAssetMenu(fileName = "Debuff Effect", menuName = "Effects/Debuff Effect")]
public class DebuffEffect : EffectConfig
{
    [Header("Damage")]
    [DamageTypeDropdown]
    public string damageTypeName = "Physical";

    [Min(0f)]
    public float damagePerTick = 5f;

    [Min(0.01f)]
    public float tickInterval = 1f;

    public DamageTypeData GetDamageType()
    {
        return DamageTypeDatabase.Instance?.GetDamageType(damageTypeName);
    }

    public override void OnApply(GameObject target, GameObject source)
    {
    }

    public override void OnUpdate(GameObject target, float deltaTime)
    {
    }

    public override void OnRemove(GameObject target)
    {
    }

    public virtual void OnDamageTick(GameObject target, float damage)
    {
    }

    public DebuffEffect WithDamageAndDuration(float newDamage, float newDuration)
    {
        DebuffEffect copy = Instantiate(this);
        copy.damagePerTick = newDamage;
        copy.duration = newDuration;
        return copy;
    }
}
