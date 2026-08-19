using FishNet.Component.Animating;
using FishNet.Object;
using UnityEngine;

/// <summary>
/// Manages the offhand weapon for dual-wielding characters.
/// Similar to WeaponHolder but for the offhand slot.
/// </summary>
public class OffHandWeaponHolder : MonoBehaviour
{
    private const string GlowZeroMaterialResourcePath = "Materials/GlowZero";

    [Header("Weapon Settings")]
    [SerializeField] private int weaponSortingOrder = 1; // Relative to player sprite
    
    private GameObject currentOffHandWeapon;
    private SpriteRenderer playerSpriteRenderer;
    
    private void Awake()
    {
        playerSpriteRenderer = GetComponent<SpriteRenderer>();
    }
    
    public void EquipWeapon(GameObject weaponPrefab)
    {
        EquipWeapon(weaponPrefab, null);
    }
    
    public void EquipWeapon(GameObject weaponPrefab, RuntimeAnimatorController animatorController)
    {
        // Remove current offhand weapon if exists
        if (currentOffHandWeapon != null)
        {
            Destroy(currentOffHandWeapon);
        }

        // Instantiate new weapon as child of the OffHandWeaponHolder named child
        Transform offHandHolderTransform = EnsureOffHandHolderChildExists();
        currentOffHandWeapon = Instantiate(weaponPrefab, offHandHolderTransform);
        currentOffHandWeapon.name = "OffHandWeapon"; // CRITICAL: Hierarchy is Player > OffHandWeaponHolder > OffHandWeapon
        currentOffHandWeapon.transform.localPosition = Vector3.zero;
        currentOffHandWeapon.transform.localRotation = Quaternion.identity;

        // Apply animator controller if provided
        if (animatorController != null)
        {
            Animator weaponAnimator = currentOffHandWeapon.GetComponentInChildren<Animator>();
            if (weaponAnimator != null)
            {
                weaponAnimator.runtimeAnimatorController = animatorController;
            }
            else
            {
                Debug.LogWarning($"[OffHandWeaponHolder] Animator controller provided but weapon {weaponPrefab.name} has no Animator component");
            }
        }

        // Find the weapon sprite renderer (look for WeaponSprite child, exclude HandHolders)
        SpriteRenderer weaponRenderer = null;
        Transform weaponSpriteChild = currentOffHandWeapon.transform.Find("WeaponSprite");
        if (weaponSpriteChild != null)
        {
            weaponRenderer = weaponSpriteChild.GetComponent<SpriteRenderer>();
        }
        if (weaponRenderer == null)
        {
            // Fallback: find first SpriteRenderer not on a HandHolder
            foreach (SpriteRenderer sr in currentOffHandWeapon.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sr.gameObject.name.Contains("HandHolder"))
                {
                    weaponRenderer = sr;
                    break;
                }
            }
        }

        ApplyGlowZeroMaterial(currentOffHandWeapon);
        
        // Set sorting order relative to player sprite
        string targetSortingLayer = "Default";
        int targetSortingOrder = 0;
        if (weaponRenderer != null && playerSpriteRenderer != null)
        {
            weaponRenderer.sortingLayerName = playerSpriteRenderer.sortingLayerName;
            weaponRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + weaponSortingOrder;
            targetSortingLayer = weaponRenderer.sortingLayerName;
            targetSortingOrder = weaponRenderer.sortingOrder;
        }
        
        Debug.Log($"[OffHandWeaponHolder] Equipped offhand weapon: {weaponPrefab.name}");
    }
    
    public void UnequipWeapon()
    {
        if (currentOffHandWeapon != null)
        {
            // Network weapons are despawned by PlayerController — only locally destroy non-network weapons
            if (currentOffHandWeapon.GetComponent<NetworkObject>() == null)
                Destroy(currentOffHandWeapon);
            currentOffHandWeapon = null;
        }
    }

    /// <summary>
    /// Called by PlayerController.ObserversRpcSetupWeaponVisuals on ALL clients after FishNet
    /// has spawned and parented the offhand weapon NetworkObject.
    /// </summary>
    public void SetupNetworkWeapon(GameObject weapon)
    {
        if (currentOffHandWeapon != null && currentOffHandWeapon != weapon)
        {
            if (currentOffHandWeapon.GetComponent<NetworkObject>() == null)
                Destroy(currentOffHandWeapon);
            else
                Debug.Log($"[OffHandWeaponHolder] Previous weapon '{currentOffHandWeapon.name}' is network-managed; server will despawn it");
        }

        Transform holderChild = EnsureOffHandHolderChildExists();
        weapon.transform.SetParent(holderChild);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.name = "OffHandWeapon";
        currentOffHandWeapon = weapon;

        Animator weaponAnimator = weapon.GetComponentInChildren<Animator>();
        NetworkAnimator netAnimator = weapon.GetComponent<NetworkAnimator>();
        if (netAnimator != null && weaponAnimator != null)
        {
            netAnimator.SetAnimator(weaponAnimator);
            Debug.Log($"[OffHandWeaponHolder] NetworkAnimator configured for '{weapon.name}'");
        }

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

        ApplyGlowZeroMaterial(weapon);

        string targetSortingLayer = "Default";
        int targetSortingOrder = 0;
        if (weaponRenderer != null && playerSpriteRenderer != null)
        {
            weaponRenderer.sortingLayerName = playerSpriteRenderer.sortingLayerName;
            weaponRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + weaponSortingOrder;
            targetSortingLayer = weaponRenderer.sortingLayerName;
            targetSortingOrder = weaponRenderer.sortingOrder;
        }
        
        Debug.Log($"[OffHandWeaponHolder] SetupNetworkWeapon complete for '{weapon.name}'");
    }

    /// <summary>Returns the named 'OffHandWeaponHolder' child transform, creating it if absent.</summary>
    public Transform EnsureOffHandHolderChildExists()
    {
        Transform holderTransform = transform.Find("OffHandWeaponHolder");
        if (holderTransform == null)
        {
            GameObject holderObj = new GameObject("OffHandWeaponHolder");
            holderObj.transform.SetParent(transform);
            holderObj.transform.localPosition = Vector3.zero;
            holderObj.transform.localRotation = Quaternion.identity;
            holderObj.transform.localScale = Vector3.one;
            holderTransform = holderObj.transform;
        }
        return holderTransform;
    }

    public bool HasWeapon() => currentOffHandWeapon != null;

    public GameObject GetCurrentWeapon() => currentOffHandWeapon;

    /// <summary>
    /// Applies a Z-rotation offset to all HandHolder children of the current offhand weapon.
    /// Call once after equipping, not per-frame.
    /// </summary>
    public void ApplyHandRotationOffset(float offset)
    {
        if (currentOffHandWeapon == null) return;
        foreach (Transform child in currentOffHandWeapon.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains("HandHolder"))
                child.localEulerAngles = new Vector3(0f, 0f, offset);
        }
    }

    private void ApplyGlowZeroMaterial(GameObject weapon)
    {
        if (weapon == null) return;

        Material glowZeroMaterial = Resources.Load<Material>(GlowZeroMaterialResourcePath);
        if (glowZeroMaterial == null)
        {
            Debug.LogWarning($"[OffHandWeaponHolder] Could not load GlowZero material at Resources/{GlowZeroMaterialResourcePath}");
            return;
        }

        foreach (SpriteRenderer spriteRenderer in weapon.GetComponentsInChildren<SpriteRenderer>(true))
        {
            spriteRenderer.material = glowZeroMaterial;
        }
    }
    
    /// <summary>
    /// Gets the world position from which projectiles should be spawned for offhand attacks.
    /// Uses LaunchZone child if available, otherwise weapon pivot.
    /// </summary>
    public Vector3 GetLaunchPosition()
    {
        if (currentOffHandWeapon == null)
        {
            return transform.position;
        }
        
        return WeaponLaunchPoint.GetLaunchPosition(currentOffHandWeapon.transform);
    }
    
    /// <summary>
    /// Gets the launch direction based on weapon rotation.
    /// </summary>
    public Vector3 GetLaunchDirection()
    {
        if (currentOffHandWeapon == null)
        {
            return Vector3.right;
        }
        
        return WeaponLaunchPoint.GetLaunchDirection(currentOffHandWeapon.transform);
    }
}
