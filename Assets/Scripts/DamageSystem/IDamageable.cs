using UnityEngine;
using System;

public interface IDamageable
{
    void TakeDamage(float damage, float critMultiplier = 1f);
    void TakeDamage(float damage, string damageTypeName, float critMultiplier = 1f);
    void TakeDamage(float damage, string damageTypeName, Vector3 attackerPosition, float critMultiplier = 1f);
    void TakeDamage(float damage, string damageTypeName, bool suppressFloater, float critMultiplier = 1f);
    void TakeDamage(float damage, string damageTypeName, Color flashColor, float critMultiplier = 1f);
    void TakeDamage(float damage, string damageTypeName, Vector3 attackerPosition, Color flashColor, float critMultiplier = 1f);
    // Overload with explicit attacker reference for thorns/reflect damage
    void TakeDamage(float damage, string damageTypeName, Vector3 attackerPosition, Color flashColor, GameObject attacker, float critMultiplier = 1f);
    float GetCurrentHealth();
    float GetMaxHealth();
    bool IsAlive { get; }  // Property that implementers must provide
    
    // Evade system - when evading, all incoming damage is negated
    bool IsEvading { get; }
    void SetEvading(bool evading);
    
    /// <summary>Invoked when damage is evaded (dodge/dash i-frames). Passes the evaded damage amount.</summary>
    event Action<IDamageable, float, string, Vector3, GameObject> OnEvade;
    /// <summary>Invoked when damage is blocked (shield/parry). Passes the blocked damage amount.</summary>
    event Action<IDamageable, float, string, Vector3, GameObject> OnBlock;
}

public interface IDamageFloaterSource
{
    void ShowDamageFloater(float damage, string damageTypeName);
}