using UnityEngine;
using System.Collections.Generic;
using System;
/// <summary>
/// Thorns passive effect - deals damage back to attackers when hit.
/// Triggered by traits that grant thorns damage.
/// Damage is reflected directly to the attacker without range restrictions.
/// </summary>
public class ThornsPassive : PassiveAbility
{
    private ThornsPassiveConfig thornsConfig;
    private Organism organism;
    private StatContainer statContainer;
    public override void Initialize(
    AbilityDataConfig abilityConfig,
    DataDrivenAbility source,
    PassiveConfig runtimePassiveConfig = null,
    PassiveAbilityConfigBase runtimePassiveAsset = null)
    {
        base.Initialize(abilityConfig, source, runtimePassiveConfig, runtimePassiveAsset);

        thornsConfig = runtimePassiveAsset as ThornsPassiveConfig;
        if (thornsConfig == null)
        {
            Debug.LogError("[ThornsPassive] Missing or wrong passive asset. Expected ThornsPassiveConfig.");
            enabled = false;
        }
    }
    private void Awake()
    {
        organism = GetComponent<Organism>();
        if (organism == null)
        {
            Debug.LogError($"[ThornsPassive] No Organism component found on {gameObject.name}! Thorns requires Organism.");
            enabled = false;
            return;
        }
        statContainer = organism.AllStats;
    }

    private void OnEnable()
    {
        if (organism != null)
        {
            organism.OnDamageTaken += HandleDamageTaken;
            Debug.Log($"[ThornsPassive] Thorns effect activated on {gameObject.name}");
        }
    }

    private void OnDisable()
    {
        if (organism != null)
        {
            organism.OnDamageTaken -= HandleDamageTaken;
            Debug.Log($"[ThornsPassive] Thorns effect deactivated on {gameObject.name}");
        }
    }

    /// <summary>
    /// Called when this character takes damage.
    /// Reflects damage directly to the attacker.
    /// </summary>
    private void HandleDamageTaken(Organism victim, float damage, string damageType, Vector3 attackerPosition, GameObject attackerObject)
    {
        // If no attacker reference provided, we can't reflect damage
        if (attackerObject == null)
        {
            Debug.Log($"[ThornsPassive] No attacker reference provided, thorns not triggered");
            return;
        }

        // Get the IDamageable from the attacker
        IDamageable attacker = attackerObject.GetComponent<IDamageable>();
        if (attacker == null)
        {
            Debug.Log($"[ThornsPassive] Attacker {attackerObject.name} is not damageable, thorns not triggered");
            return;
        }

        // Don't reflect damage to ourselves
        if (attackerObject == gameObject)
        {
            return;
        }

        // Calculate thorns damage (can be modified by stats)
        float thornsDamage = CalculateThornsDamage();

        if (thornsDamage <= 0f)
        {
            return;
        }

        Debug.Log($"[ThornsPassive] Dealing {thornsDamage:F1} {thornsConfig.ThornsDamageType} thorns damage to {attackerObject.name}");

        // Deal damage directly back to the attacker
        attacker.TakeDamage(thornsDamage, thornsConfig.ThornsDamageType, transform.position);

        // Spawn visual effect at attacker's position
        if (thornsConfig.ThornsEffectPrefab != null)
        {
            GameObject effect = Instantiate(thornsConfig.ThornsEffectPrefab, attackerObject.transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // Play sound effect
        if (thornsConfig.ThornsSound != null)
        {
            AudioManager.Instance?.PlaySpatialSound(thornsConfig.ThornsSound, attackerObject.transform.position);
        }
    }

    /// <summary>
    /// Calculate final thorns damage based on stats.
    /// Uses the "Thorns" stat directly - no base damage from config.
    /// </summary>
    private float CalculateThornsDamage()
    {
        float damage = thornsConfig.ThornsBaseDamage;
        return damage;
    }
}
