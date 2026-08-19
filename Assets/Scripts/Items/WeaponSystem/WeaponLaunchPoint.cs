using UnityEngine;

/// <summary>
/// Utility for getting projectile spawn positions from weapons.
/// Looks for a "LaunchZone" child transform, otherwise uses weapon pivot.
/// </summary>
public static class WeaponLaunchPoint
{
    /// <summary>
    /// Gets the world position from which to spawn projectiles.
    /// Searches for a "LaunchZone" child, falls back to weapon transform position.
    /// </summary>
    /// <param name="weaponTransform">The weapon transform (typically the Weapon or OffHand GameObject)</param>
    /// <returns>World position to spawn projectiles from</returns>
    public static Vector3 GetLaunchPosition(Transform weaponTransform)
    {
        if (weaponTransform == null)
        {
            Debug.LogWarning("[WeaponLaunchPoint] Weapon transform is null! Using Vector3.zero.");
            return Vector3.zero;
        }

        // Look for LaunchZone child
        Transform launchZone = FindLaunchZone(weaponTransform);
        
        if (launchZone != null)
        {
            return launchZone.position;
        }
        
        // Fallback to weapon pivot
        return weaponTransform.position;
    }
    
    /// <summary>
    /// Gets the launch direction from the weapon's rotation.
    /// Uses LaunchZone rotation if available, otherwise weapon rotation.
    /// </summary>
    /// <param name="weaponTransform">The weapon transform</param>
    /// <returns>Forward direction vector from the launch point</returns>
    public static Vector3 GetLaunchDirection(Transform weaponTransform)
    {
        if (weaponTransform == null)
        {
            return Vector3.right; // Default direction
        }

        // Look for LaunchZone child
        Transform launchZone = FindLaunchZone(weaponTransform);
        
        if (launchZone != null)
        {
            return launchZone.right; // 2D games typically use "right" as forward
        }
        
        // Fallback to weapon rotation
        return weaponTransform.right;
    }
    
    /// <summary>
    /// Recursively searches for a child named "LaunchZone" in the weapon hierarchy.
    /// </summary>
    public static Transform FindLaunchZone(Transform parent)
    {
        // Direct child search
        Transform launchZone = parent.Find("LaunchZone");
        if (launchZone != null)
        {
            return launchZone;
        }
        
        // Recursive search through all children
        foreach (Transform child in parent)
        {
            Transform found = FindLaunchZone(child);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets all launch zones from a weapon (useful for multi-barrel weapons).
    /// </summary>
    /// <param name="weaponTransform">The weapon transform</param>
    /// <returns>Array of launch zone transforms, or single weapon transform if none found</returns>
    public static Transform[] GetAllLaunchZones(Transform weaponTransform)
    {
        if (weaponTransform == null)
        {
            return new Transform[0];
        }

        // Find all children named "LaunchZone" or with "LaunchZone" prefix
        System.Collections.Generic.List<Transform> launchZones = new System.Collections.Generic.List<Transform>();
        FindAllLaunchZones(weaponTransform, launchZones);
        
        if (launchZones.Count > 0)
        {
            return launchZones.ToArray();
        }
        
        // Fallback to weapon itself
        return new Transform[] { weaponTransform };
    }
    
    /// <summary>
    /// Recursively finds all LaunchZone children.
    /// </summary>
    private static void FindAllLaunchZones(Transform parent, System.Collections.Generic.List<Transform> results)
    {
        foreach (Transform child in parent)
        {
            if (child.name == "LaunchZone" || child.name.StartsWith("LaunchZone"))
            {
                results.Add(child);
            }
            
            // Continue searching deeper
            FindAllLaunchZones(child, results);
        }
    }
    
    /// <summary>
    /// Gets projectile prefab override from weapon's LaunchZone.
    /// Returns null if no LaunchZone or no override specified.
    /// </summary>
    /// <param name="weaponTransform">The weapon transform</param>
    /// <returns>Override projectile prefab or null</returns>
    public static GameObject GetProjectilePrefabOverride(Transform weaponTransform)
    {
        if (weaponTransform == null)
        {
            return null;
        }
        
        // Look for LaunchZone child
        Transform launchZone = FindLaunchZone(weaponTransform);
        
        if (launchZone != null)
        {
            LaunchZone launchZoneComponent = launchZone.GetComponent<LaunchZone>();
            if (launchZoneComponent != null && launchZoneComponent.ProjectilePrefabOverride != null)
            {
                return launchZoneComponent.ProjectilePrefabOverride;
            }
        }
        
        return null;
    }
}
