using System;
using UnityEngine;

namespace JoeConticello.ModularCombatCore
{
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }

        DamageResult ApplyDamage(DamageRequest request);
    }

    [Serializable]
    public readonly struct DamageRequest
    {
        public float Amount { get; }
        public string DamageType { get; }
        public float CriticalMultiplier { get; }
        public Vector3 SourcePosition { get; }
        public GameObject Source { get; }

        public DamageRequest(
            float amount,
            string damageType = "Physical",
            float criticalMultiplier = 1f,
            Vector3 sourcePosition = default,
            GameObject source = null)
        {
            Amount = amount;
            DamageType = string.IsNullOrWhiteSpace(damageType) ? "Physical" : damageType;
            CriticalMultiplier = Mathf.Max(1f, criticalMultiplier);
            SourcePosition = sourcePosition;
            Source = source;
        }
    }

    [Serializable]
    public readonly struct DamageResult
    {
        public float RequestedDamage { get; }
        public float AppliedDamage { get; }
        public float RemainingHealth { get; }
        public bool WasCritical { get; }
        public bool WasBlocked { get; }
        public bool WasEvaded { get; }

        public DamageResult(
            float requestedDamage,
            float appliedDamage,
            float remainingHealth,
            bool wasCritical = false,
            bool wasBlocked = false,
            bool wasEvaded = false)
        {
            RequestedDamage = requestedDamage;
            AppliedDamage = appliedDamage;
            RemainingHealth = remainingHealth;
            WasCritical = wasCritical;
            WasBlocked = wasBlocked;
            WasEvaded = wasEvaded;
        }
    }
}