using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime stat buff/debuff effect that mutates a target StatContainer for the effect duration,
/// then restores values on remove.
/// </summary>
[CreateAssetMenu(fileName = "Stat Buff Effect", menuName = "Effects/Stat Buff Effect")]
public class StatBuffEffect : EffectConfig
{
    [Header("Stat Buff")]
    [Tooltip("Optional explicit icon for HUD buff display. If set, it is mirrored to EffectConfig.icon.")]
    public Sprite buffIcon;

    [Tooltip("Stat modifiers applied while this effect is active.")]
    public List<StatModifier> statModifiers = new List<StatModifier>();

    [Serializable]
    private class AppliedState
    {
        public readonly Dictionary<string, float> additiveDeltas = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, float> originalOverrides = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    }

    // Per-target state for this specific effect asset instance.
    private readonly Dictionary<int, AppliedState> _appliedByTarget = new Dictionary<int, AppliedState>();

    private void OnValidate()
    {
        if (buffIcon != null)
            icon = buffIcon;
    }

    private StatContainer ResolveStatContainer(GameObject target)
    {
        if (target == null)
            return null;

        Organism organism = target.GetComponent<Organism>();
        if (organism == null)
            organism = target.GetComponentInParent<Organism>();

        if (organism != null)
            return organism.AllStats;

        return target.GetAllStats();
    }

    private Organism ResolveOrganism(GameObject target)
    {
        if (target == null)
            return null;

        Organism organism = target.GetComponent<Organism>();
        if (organism == null)
            organism = target.GetComponentInParent<Organism>();
        return organism;
    }

    public override void OnApply(GameObject target, GameObject source)
    {
        StatContainer stats = ResolveStatContainer(target);
        if (stats == null)
        {
            Debug.LogWarning($"[StatBuffEffect] Cannot apply '{effectName}' to {target?.name ?? "null"}: no StatContainer found.");
            return;
        }

        int targetId = target.GetInstanceID();
        var state = new AppliedState();
        bool touchedMoveSpeed = false;

        foreach (StatModifier mod in statModifiers)
        {
            if (mod == null || string.IsNullOrEmpty(mod.statID))
                continue;

            if (!stats.HasStat(mod.statID))
                continue;

            if (string.Equals(mod.statID, "MoveSpeed", StringComparison.OrdinalIgnoreCase))
                touchedMoveSpeed = true;

            switch (mod.modifierType)
            {
                case ModifierType.Flat:
                {
                    stats.ModifyStat(mod.statID, mod.value);
                    if (state.additiveDeltas.TryGetValue(mod.statID, out float existingFlat))
                        state.additiveDeltas[mod.statID] = existingFlat + mod.value;
                    else
                        state.additiveDeltas[mod.statID] = mod.value;
                    break;
                }
                case ModifierType.Percentage:
                {
                    float current = stats.GetStat(mod.statID);
                    float delta = current * (mod.value / 100f);
                    stats.ModifyStat(mod.statID, delta);
                    if (state.additiveDeltas.TryGetValue(mod.statID, out float existingPct))
                        state.additiveDeltas[mod.statID] = existingPct + delta;
                    else
                        state.additiveDeltas[mod.statID] = delta;
                    break;
                }
                case ModifierType.Override:
                {
                    if (!state.originalOverrides.ContainsKey(mod.statID))
                        state.originalOverrides[mod.statID] = stats.GetStat(mod.statID);
                    stats.SetStat(mod.statID, mod.value);
                    break;
                }
            }
        }

        _appliedByTarget[targetId] = state;

        if (touchedMoveSpeed)
        {
            Organism organism = ResolveOrganism(target);
            
        }
    }

    public override void OnUpdate(GameObject target, float deltaTime)
    {
        // No per-frame logic required.
    }

    public override void OnRemove(GameObject target)
    {
        if (target == null)
            return;

        int targetId = target.GetInstanceID();
        if (!_appliedByTarget.TryGetValue(targetId, out AppliedState state))
            return;

        StatContainer stats = ResolveStatContainer(target);
        if (stats == null)
        {
            _appliedByTarget.Remove(targetId);
            return;
        }

        bool touchedMoveSpeed = false;

        foreach (var kvp in state.additiveDeltas)
        {
            if (!stats.HasStat(kvp.Key))
                continue;

            if (string.Equals(kvp.Key, "MoveSpeed", StringComparison.OrdinalIgnoreCase))
                touchedMoveSpeed = true;

            stats.ModifyStat(kvp.Key, -kvp.Value);
        }

        foreach (var kvp in state.originalOverrides)
        {
            if (!stats.HasStat(kvp.Key))
                continue;

            if (string.Equals(kvp.Key, "MoveSpeed", StringComparison.OrdinalIgnoreCase))
                touchedMoveSpeed = true;

            stats.SetStat(kvp.Key, kvp.Value);
        }

        _appliedByTarget.Remove(targetId);

        if (touchedMoveSpeed)
        {
            Organism organism = ResolveOrganism(target);
            
        }
    }
}
