using UnityEngine;

/// <summary>
/// Forwards damage from child colliders to parent Enemy component
/// Used for bounce enemies where the visual child has the trigger collider
/// </summary>
public class EnemyDamageForwarder : MonoBehaviour, IDamageable, IDamageFloaterSource
{
    private Enemy parentEnemy;
    
    private void Awake()
    {
        // Find the Enemy component on parent
        parentEnemy = GetComponentInParent<Enemy>();
        
        if (parentEnemy == null)
        {
            Debug.LogError($"[EnemyDamageForwarder] No Enemy component found in parent of {gameObject.name}");
        }
    }
    
    public void TakeDamage(float damage, float critMultiplier = 1f)
    {
        if (parentEnemy != null)
        {
            parentEnemy.TakeDamage(damage, critMultiplier);
        }
    }
    
    public void TakeDamage(float damage, string damageTypeName, float critMultiplier = 1f)
    {
        if (parentEnemy != null)
        {
            parentEnemy.TakeDamage(damage, damageTypeName, critMultiplier);
        }
    }
    
    public void TakeDamage(float damage, string damageTypeName, Vector3 hitPosition, float critMultiplier = 1f)
    {
        if (parentEnemy != null)
        {
            parentEnemy.TakeDamage(damage, damageTypeName, hitPosition, critMultiplier);
        }
    }
    
    public void TakeDamage(float damage, string damageTypeName, bool suppressFloater, float critMultiplier = 1f)
    {
        if (parentEnemy != null)
        {
            parentEnemy.TakeDamage(damage, damageTypeName, suppressFloater, critMultiplier);
        }
    }
    
    public void TakeDamage(float damage, string damageTypeName, Color flashColor, float critMultiplier = 1f)
    {
        if (parentEnemy != null)
        {
            parentEnemy.TakeDamage(damage, damageTypeName, flashColor, critMultiplier);
        }
    }
    
    public void TakeDamage(float damage, string damageTypeName, Vector3 attackerPosition, Color flashColor, float critMultiplier = 1f)
    {
        if (parentEnemy != null)
        {
            parentEnemy.TakeDamage(damage, damageTypeName, attackerPosition, flashColor, critMultiplier);
        }
    }
    
    public void TakeDamage(float damage, string damageTypeName, Vector3 attackerPosition, Color flashColor, GameObject attacker, float critMultiplier = 1f)
    {
        if (parentEnemy != null)
        {
            // Forward with attacker reference for thorns/reflect
            parentEnemy.TakeDamage(damage, damageTypeName, attackerPosition, flashColor, attacker, critMultiplier);
        }
    }
    
    public void ShowDamageFloater(float damage, string damageType)
    {
        if (parentEnemy != null)
        {
            parentEnemy.ShowDamageFloater(damage, damageType);
        }
    }
    
    public float GetCurrentHealth()
    {
        return parentEnemy != null ? parentEnemy.GetCurrentHealth() : 0f;
    }
    
    public float GetMaxHealth()
    {
        return parentEnemy != null ? parentEnemy.GetMaxHealth() : 0f;
    }
    
    public bool IsAlive
    {
        get { return parentEnemy != null && parentEnemy.IsAlive; }
    }
    
    // Evade system - forward to parent
    public bool IsEvading => parentEnemy != null && parentEnemy.IsEvading;
    
    public void SetEvading(bool evading)
    {
        parentEnemy?.SetEvading(evading);
    }
    
    // Events - forward from parent (enemies typically don't use these directly)
    public event System.Action<IDamageable, float, string, Vector3, GameObject> OnEvade
    {
        add { if (parentEnemy != null) parentEnemy.OnEvade += value; }
        remove { if (parentEnemy != null) parentEnemy.OnEvade -= value; }
    }
    
    public event System.Action<IDamageable, float, string, Vector3, GameObject> OnBlock
    {
        add { if (parentEnemy != null) parentEnemy.OnBlock += value; }
        remove { if (parentEnemy != null) parentEnemy.OnBlock -= value; }
    }
}
