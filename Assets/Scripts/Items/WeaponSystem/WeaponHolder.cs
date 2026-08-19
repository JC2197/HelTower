using FishNet.Component.Animating;
using FishNet.Object;
using System;
using UnityEngine;

public class WeaponHolder : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private int weaponSortingOrder = 1; // Relative to player sprite
    
    private GameObject currentWeapon;
    private SpriteRenderer playerSpriteRenderer;
    
    private void Awake()
    {
        playerSpriteRenderer = GetComponent<SpriteRenderer>();
    }
    
    // Weapon is equipped by PlayerController, not auto-loaded here
    
    public void EquipWeapon(GameObject weaponPrefab)
    {
        EquipWeapon(weaponPrefab, null);
    }
    
    public void EquipWeapon(GameObject weaponPrefab, RuntimeAnimatorController animatorController)
    {
        // Remove current weapon if exists
        if (currentWeapon != null)
        {
            Destroy(currentWeapon);
        }

        // Instantiate new weapon as child of the WeaponHolder named child
        Transform weaponHolderTransform = EnsureWeaponHolderChildExists();
        currentWeapon = Instantiate(weaponPrefab, weaponHolderTransform);
        currentWeapon.name = "Weapon"; // CRITICAL: Hierarchy is Player > WeaponHolder > Weapon
        currentWeapon.transform.localPosition = Vector3.zero;
        currentWeapon.transform.localRotation = Quaternion.identity;

        // Apply animator controller if provided (for weapon variants with sprite swaps)
        if (animatorController != null)
        {
            Animator weaponAnimator = currentWeapon.GetComponentInChildren<Animator>();
            if (weaponAnimator != null)
            {
                weaponAnimator.runtimeAnimatorController = animatorController;
                Debug.Log($"[WeaponHolder] Applied animator controller: {animatorController.name} to {weaponAnimator.gameObject.name} (weapon: {weaponPrefab.name})");
                Debug.Log($"[WeaponHolder] Animator controller type: {animatorController.GetType().Name}");
                Debug.Log($"[WeaponHolder] Animator controller has {animatorController.animationClips.Length} clips");
            }
            else
            {
                Debug.LogWarning($"[WeaponHolder] Animator controller provided but weapon {weaponPrefab.name} has no Animator component in hierarchy");
            }
        }
        else
        {
            Animator weaponAnimator = currentWeapon.GetComponentInChildren<Animator>();
            if (weaponAnimator != null)
            {
                Debug.Log($"[WeaponHolder] No override controller provided, weapon using prefab's default controller: {(weaponAnimator.runtimeAnimatorController != null ? weaponAnimator.runtimeAnimatorController.name : "NONE")}");
            }
        }

        // Find the weapon sprite renderer (look for WeaponSprite child, exclude HandHolders)
        SpriteRenderer weaponRenderer = null;
        Transform weaponSpriteChild = currentWeapon.transform.Find("WeaponSprite");
        if (weaponSpriteChild != null)
        {
            weaponRenderer = weaponSpriteChild.GetComponent<SpriteRenderer>();
        }
        if (weaponRenderer == null)
        {
            // Fallback: find first SpriteRenderer not on a HandHolder
            foreach (SpriteRenderer sr in currentWeapon.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sr.gameObject.name.Contains("HandHolder"))
                {
                    weaponRenderer = sr;
                    break;
                }
            }
        }
        
        int targetSortingOrder = 0;
        string targetSortingLayer = "Default";
        
        if (weaponRenderer != null && playerSpriteRenderer != null)
        {
            weaponRenderer.sortingLayerName = playerSpriteRenderer.sortingLayerName;
            weaponRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + weaponSortingOrder;
            targetSortingOrder = weaponRenderer.sortingOrder;
            targetSortingLayer = weaponRenderer.sortingLayerName;
        }
        
        Debug.Log($"Equipped weapon: {weaponPrefab.name}");
    }
    
    public void UnequipWeapon()
    {
        if (currentWeapon != null)
        {
            // Network weapons are despawned by PlayerController — only locally destroy non-network weapons
            if (currentWeapon.GetComponent<NetworkObject>() == null)
                Destroy(currentWeapon);
            currentWeapon = null;
        }
    }

    /// <summary>
    /// Called by PlayerController.ObserversRpcSetupWeaponVisuals on ALL clients after FishNet
    /// has spawned and parented the weapon NetworkObject. Configures visuals (sorting, animator)
    /// and moves the weapon under the WeaponHolder child for clean hierarchy organisation.
    /// </summary>
    public void SetupNetworkWeapon(GameObject weapon)
    {
        // Discard any pre-network Instantiate-created weapon (from the Awake-time SetupCharacter call)
        if (currentWeapon != null && currentWeapon != weapon)
        {
            if (currentWeapon.GetComponent<NetworkObject>() == null)
            {
                Destroy(currentWeapon);
            }
            else
            {
                Debug.Log($"[WeaponHolder] Previous weapon '{currentWeapon.name}' is network-managed; server will despawn it");
            }
        }

        // Parent under the named WeaponHolder child (within the player's FishNet hierarchy)
        Transform holderChild = EnsureWeaponHolderChildExists();
        weapon.transform.SetParent(holderChild);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.name = "Weapon";
        currentWeapon = weapon;

        // NetworkAnimator must reference the Animator after any runtime setup
        Animator weaponAnimator = weapon.GetComponentInChildren<Animator>();
        NetworkAnimator netAnimator = weapon.GetComponentInChildren<NetworkAnimator>();
        if (netAnimator != null && weaponAnimator != null)
        {
            netAnimator.SetAnimator(weaponAnimator);
            Debug.Log($"[WeaponHolder] NetworkAnimator configured for '{weapon.name}'");
        }
        
        // Sorting orders
        SpriteRenderer weaponRenderer = null;
        Transform weaponSpriteChild = weapon.transform.Find("WeaponSprite");
        if (weaponSpriteChild != null)
            weaponRenderer = weaponSpriteChild.GetComponent<SpriteRenderer>();
        if (weaponRenderer == null)
        {
            foreach (SpriteRenderer sr in weapon.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sr.gameObject.name.Contains("HandHolder"))
                {
                    weaponRenderer = sr;
                    break;
                }
            }
        }

        int targetSortingOrder = 0;
        string targetSortingLayer = "Default";
        if (weaponRenderer != null && playerSpriteRenderer != null)
        {
            weaponRenderer.sortingLayerName = playerSpriteRenderer.sortingLayerName;
            weaponRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + weaponSortingOrder;
            targetSortingOrder = weaponRenderer.sortingOrder;
            targetSortingLayer = weaponRenderer.sortingLayerName;
        }

        Debug.Log($"[WeaponHolder] SetupNetworkWeapon complete for '{weapon.name}'");
    }

    /// <summary>Returns the named 'WeaponHolder' child transform, creating it if absent.</summary>
    public Transform EnsureWeaponHolderChildExists()
    {
        if (string.Equals(transform.name, "WeaponHolder", StringComparison.Ordinal))
            return transform;

        Transform weaponHolderTransform = transform.Find("WeaponHolder");
        if (weaponHolderTransform == null)
        {
            GameObject weaponHolderObj = new GameObject("WeaponHolder");
            weaponHolderObj.transform.SetParent(transform);
            weaponHolderObj.transform.localPosition = Vector3.zero;
            weaponHolderObj.transform.localRotation = Quaternion.identity;
            weaponHolderObj.transform.localScale = Vector3.one;
            weaponHolderTransform = weaponHolderObj.transform;
        }
        return weaponHolderTransform;
    }

    public bool HasWeapon() => currentWeapon != null;

    public GameObject GetCurrentWeapon() => currentWeapon;

    /// <summary>
    /// Applies a Z-rotation offset to all HandHolder children of the current weapon.
    /// Call once after equipping a weapon, not per-frame.
    /// </summary>
    public void ApplyHandRotationOffset(float offset)
    {
        if (currentWeapon == null) return;
        foreach (Transform child in currentWeapon.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains("HandHolder"))
                child.localEulerAngles = new Vector3(0f, 0f, offset);
        }
    }
    
    /// <summary>
    /// Gets the world position from which projectiles should be spawned.
    /// Uses LaunchZone child if available, otherwise weapon pivot.
    /// </summary>
    public Vector3 GetLaunchPosition()
    {
        if (currentWeapon == null)
        {
            return transform.position;
        }
        
        return WeaponLaunchPoint.GetLaunchPosition(currentWeapon.transform);
    }
    
    /// <summary>
    /// Gets the launch direction based on weapon rotation.
    /// </summary>
    public Vector3 GetLaunchDirection()
    {
        if (currentWeapon == null)
        {
            return Vector3.right;
        }
        
        return WeaponLaunchPoint.GetLaunchDirection(currentWeapon.transform);
    }
}