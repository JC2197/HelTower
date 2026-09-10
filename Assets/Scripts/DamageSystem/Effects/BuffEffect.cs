using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Buff Effect", menuName = "Effects/Buff Effect")]
public class BuffEffect : EffectConfig
{
    [Header("Stat Modifiers")]
    public List<StatModifier> statModifiers = new List<StatModifier>();

    [Header("Shield")]
    public bool grantsShield;
    [Min(0f)] public float shieldAmount;

    [Header("Next Attack")]
    public bool nextAttack;
    public AbilityDataConfig abilityToCast;
    public bool replaceAttack;
    public AbilityDataConfig replacementAbilityToCast;
    [Min(0.1f)] public float triggeredAbilityLifetime = 5f;

    private sealed class AppliedState
    {
        public readonly Dictionary<string, float> additiveDeltas = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, float> originalOverrides = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public Organism organism;
        public PlayerController player;
        public bool isCastingTriggeredAbility;
    }

    private readonly Dictionary<int, AppliedState> appliedByTarget = new Dictionary<int, AppliedState>();

    public override void OnApply(GameObject target, GameObject source)
    {
        StatContainer stats = ResolveStats(target);
        Organism organism = target != null ? target.GetComponentInParent<Organism>() : null;
        if (stats == null && (!grantsShield || organism == null))
            return;

        int targetId = target.GetInstanceID();
        ApplyModifiers(stats, organism, targetId, 1);
        BindAttackTriggers(target, targetId);
    }

    public override void OnStackChanged(GameObject target, int oldStacks, int newStacks)
    {
        if (target == null)
            return;

        int targetId = target.GetInstanceID();
        if (appliedByTarget.TryGetValue(targetId, out AppliedState state))
            RemoveAppliedState(target, targetId, state);

        StatContainer stats = ResolveStats(target);
        Organism organism = target.GetComponentInParent<Organism>();
        if (stats == null && (!grantsShield || organism == null))
            return;

        ApplyModifiers(stats, organism, targetId, Mathf.Max(1, newStacks));
        BindAttackTriggers(target, targetId);
    }

    public override void OnUpdate(GameObject target, float deltaTime)
    {
    }

    public override void OnRemove(GameObject target)
    {
        if (target == null)
            return;

        int targetId = target.GetInstanceID();
        if (appliedByTarget.TryGetValue(targetId, out AppliedState state))
            RemoveAppliedState(target, targetId, state);
    }

    private void BindAttackTriggers(GameObject target, int targetId)
    {
        bool hasAttackTrigger = (nextAttack && abilityToCast != null) || (replaceAttack && replacementAbilityToCast != null);
        if (!grantsShield && !hasAttackTrigger)
            return;
        if (!appliedByTarget.TryGetValue(targetId, out AppliedState state))
            return;

        PlayerController player = target.GetComponent<PlayerController>();
        if (state.organism == null)
            return;

        UnbindAttackTriggers(state);
        state.player = player;
        if (grantsShield)
            state.organism.OnShieldDestroyed += HandleShieldDestroyed;
        if (nextAttack && abilityToCast != null)
            state.player.OnAttack += HandleNextAttack;
        if (replaceAttack && replacementAbilityToCast != null)
            state.player.OnBeforeAttackAbilityUse += HandleReplacementAttack;
    }

    private void UnbindAttackTriggers(AppliedState state)
    {
        if (state.organism != null)
            state.organism.OnShieldDestroyed -= HandleShieldDestroyed;
        if (state.player != null)
        {
            state.player.OnAttack -= HandleNextAttack;
            state.player.OnBeforeAttackAbilityUse -= HandleReplacementAttack;
        }
        state.player = null;
    }

    private void HandleNextAttack(AbilityDataConfig attackConfig)
    {
        foreach (var pair in appliedByTarget)
        {
            AppliedState state = pair.Value;
            if (state.player == null || state.isCastingTriggeredAbility || attackConfig == abilityToCast)
                continue;

            if (CastConfiguredAbility(state, abilityToCast))
                ConsumeStack(state.player);
        }
    }

    private void HandleShieldDestroyed(Organism organism, object shieldSource)
    {
        if (!ReferenceEquals(shieldSource, this))
            return;

        EffectManager effectManager = null;
        foreach (var pair in appliedByTarget)
        {
            AppliedState state = pair.Value;
            if (state.organism == organism)
            {
                effectManager = organism.GetComponent<EffectManager>();
                break;
            }
        }

        effectManager?.RemoveEffect(effectID);
    }

    private bool HandleReplacementAttack(AbilityDataConfig attackConfig)
    {
        foreach (var pair in appliedByTarget)
        {
            AppliedState state = pair.Value;
            if (state.player == null || state.isCastingTriggeredAbility || attackConfig == replacementAbilityToCast)
                continue;

            if (CastConfiguredAbility(state, replacementAbilityToCast))
            {
                ConsumeStack(state.player);
                return true;
            }
        }
        return false;
    }

    private bool CastConfiguredAbility(AppliedState state, AbilityDataConfig ability)
    {
        if (ability == null)
            return false;

        state.isCastingTriggeredAbility = true;
        DataDrivenAbility runtimeAbility = state.player.gameObject.AddComponent<DataDrivenAbility>();
        runtimeAbility.SetAbilityReference(new AbilityReference(ability));
        runtimeAbility.ConfigureAsTriggeredProjectile();
        runtimeAbility.InitializeAbility();
        runtimeAbility.RebuildConfigModifiers();
        bool cast = runtimeAbility.TryUseAbilityManually();
        Destroy(runtimeAbility, triggeredAbilityLifetime);
        state.isCastingTriggeredAbility = false;
        return cast;
    }

    private void ConsumeStack(PlayerController player)
    {
        player.GetComponent<EffectManager>()?.ConsumeEffectStack(effectID);
    }

    private void ApplyModifiers(StatContainer stats, Organism organism, int targetId, int stackCount)
    {
        var state = new AppliedState { organism = organism };
        if (grantsShield && state.organism != null)
            state.organism.SetShield(this, shieldAmount * stackCount);

        foreach (StatModifier modifier in statModifiers)
        {
            if (stats == null || modifier == null || string.IsNullOrEmpty(modifier.statID) || !stats.HasStat(modifier.statID))
                continue;

            switch (modifier.modifierType)
            {
                case ModifierType.Flat:
                case ModifierType.Percentage:
                    float delta = modifier.modifierType == ModifierType.Flat
                        ? modifier.value * stackCount
                        : stats.GetStat(modifier.statID) * (modifier.value / 100f) * stackCount;
                    stats.ModifyStat(modifier.statID, delta);
                    state.additiveDeltas[modifier.statID] = delta;
                    break;
                case ModifierType.Override:
                    state.originalOverrides[modifier.statID] = stats.GetStat(modifier.statID);
                    stats.SetStat(modifier.statID, modifier.value);
                    break;
            }
        }
        appliedByTarget[targetId] = state;
    }

    private void RemoveAppliedState(GameObject target, int targetId, AppliedState state)
    {
        UnbindAttackTriggers(state);
        state.organism?.RemoveShield(this);
        StatContainer stats = ResolveStats(target);
        if (stats != null)
        {
            foreach (var delta in state.additiveDeltas)
                if (stats.HasStat(delta.Key)) stats.ModifyStat(delta.Key, -delta.Value);
            foreach (var original in state.originalOverrides)
                if (stats.HasStat(original.Key)) stats.SetStat(original.Key, original.Value);
        }
        appliedByTarget.Remove(targetId);
    }

    private static StatContainer ResolveStats(GameObject target)
    {
        Organism organism = target != null ? target.GetComponentInParent<Organism>() : null;
        return organism != null ? organism.AllStats : target?.GetAllStats();
    }
}