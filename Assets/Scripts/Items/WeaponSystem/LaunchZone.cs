using UnityEngine;

/// <summary>
/// Component for LaunchZone GameObjects that defines projectile spawn point and optional projectile override.
/// Add this to your LaunchZone child for better visibility when setting up weapons.
/// Can override projectile prefabs from abilities to make weapon-specific projectiles.
/// </summary>
public class LaunchZone : MonoBehaviour
{
    [Header("Projectile Override")]
    [Tooltip("Optional: Override projectile prefab from ability with weapon-specific projectile. Leave empty to use ability's projectile.")]
    [SerializeField] private GameObject projectilePrefabOverride;
    
    [Header("Gizmo Settings")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange
    [SerializeField] private float gizmoSize = 0.2f;
    [SerializeField] private bool drawDirection = true;
    [SerializeField] private float directionLength = 0.5f;
    
    /// <summary>
    /// Get the projectile prefab override (null if no override)
    /// </summary>
    public GameObject ProjectilePrefabOverride => projectilePrefabOverride;
    
    private void OnDrawGizmos()
    {
        // Draw spawn point
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, gizmoSize);
        Gizmos.DrawSphere(transform.position, gizmoSize * 0.5f);
        
        // Draw direction arrow
        if (drawDirection)
        {
            Gizmos.color = Color.red;
            Vector3 direction = transform.right * directionLength;
            Gizmos.DrawRay(transform.position, direction);
            
            // Draw arrow head
            Vector3 arrowEnd = transform.position + direction;
            Vector3 arrowLeft = Quaternion.Euler(0, 0, 150) * direction.normalized * 0.2f;
            Vector3 arrowRight = Quaternion.Euler(0, 0, -150) * direction.normalized * 0.2f;
            Gizmos.DrawLine(arrowEnd, arrowEnd + arrowLeft);
            Gizmos.DrawLine(arrowEnd, arrowEnd + arrowRight);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw brighter when selected
        Gizmos.color = new Color(1f, 0.8f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, gizmoSize * 1.5f);
        
        if (drawDirection)
        {
            Gizmos.color = Color.yellow;
            Vector3 direction = transform.right * directionLength * 1.5f;
            Gizmos.DrawRay(transform.position, direction);
        }
    }
}
