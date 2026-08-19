using FishNet.Component.Animating;
using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds any number of accessories. Every accessory is instantiated as a child of the same
/// 'AccessoryHolder' transform: Player > AccessoryHolder > Accessory_&lt;prefabName&gt;.
/// </summary>
public class AccessoryHolder : MonoBehaviour
{
    [Header("Accessory Settings")]
    [SerializeField] private int AccessorySortingOrder = 1; // Relative to player sprite

    private readonly List<GameObject> equippedAccessories = new List<GameObject>();
    private SpriteRenderer playerSpriteRenderer;

    private void Awake()
    {
        playerSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Accessories are equipped by PlayerController, not auto-loaded here

    public GameObject EquipAccessory(GameObject AccessoryPrefab)
    {
        return EquipAccessory(AccessoryPrefab, null);
    }

    /// <summary>Adds an accessory alongside any already-equipped ones (never replaces them).</summary>
    public GameObject EquipAccessory(GameObject AccessoryPrefab, RuntimeAnimatorController animatorController)
    {
        if (AccessoryPrefab == null)
            return null;

        // Instantiate the new Accessory as another child of the AccessoryHolder named child
        Transform AccessoryHolderTransform = EnsureAccessoryHolderChildExists();
        GameObject accessory = Instantiate(AccessoryPrefab, AccessoryHolderTransform);
        accessory.name = $"Accessory_{AccessoryPrefab.name}";
        accessory.transform.localPosition = Vector3.zero;
        accessory.transform.localRotation = Quaternion.identity;

        if (animatorController != null)
        {
            Animator AccessoryAnimator = accessory.GetComponentInChildren<Animator>();
            if (AccessoryAnimator != null)
                AccessoryAnimator.runtimeAnimatorController = animatorController;
            else
                Debug.LogWarning($"[AccessoryHolder] Animator controller provided but Accessory {AccessoryPrefab.name} has no Animator component in hierarchy");
        }

        ApplyAccessorySorting(accessory);
        equippedAccessories.Add(accessory);

        Debug.Log($"[AccessoryHolder] Equipped Accessory: {AccessoryPrefab.name}");
        return accessory;
    }

    /// <summary>Removes a single equipped accessory instance.</summary>
    public void UnequipAccessory(GameObject accessory)
    {
        if (accessory == null)
            return;

        equippedAccessories.Remove(accessory);

        // Network accessories are despawned by PlayerController — only locally destroy non-network ones
        if (accessory.GetComponent<NetworkObject>() == null)
            Destroy(accessory);
    }

    /// <summary>Removes every equipped accessory.</summary>
    public void UnequipAllAccessories()
    {
        for (int i = equippedAccessories.Count - 1; i >= 0; i--)
        {
            GameObject accessory = equippedAccessories[i];
            if (accessory != null && accessory.GetComponent<NetworkObject>() == null)
                Destroy(accessory);
        }
        equippedAccessories.Clear();
    }

    /// <summary>
    /// Called by PlayerController.ObserversRpcSetupAccessoryVisuals on ALL clients after FishNet
    /// has spawned and parented the Accessory NetworkObject. Configures visuals (sorting, animator)
    /// and moves the Accessory under the AccessoryHolder child for clean hierarchy organisation.
    /// </summary>
    public void SetupNetworkAccessory(GameObject Accessory)
    {
        if (Accessory == null)
            return;

        // Parent under the named AccessoryHolder child (within the player's FishNet hierarchy)
        Transform holderChild = EnsureAccessoryHolderChildExists();
        Accessory.transform.SetParent(holderChild);
        Accessory.transform.localPosition = Vector3.zero;
        Accessory.transform.localRotation = Quaternion.identity;

        if (!equippedAccessories.Contains(Accessory))
            equippedAccessories.Add(Accessory);

        // NetworkAnimator must reference the Animator after any runtime setup
        Animator AccessoryAnimator = Accessory.GetComponentInChildren<Animator>();
        NetworkAnimator netAnimator = Accessory.GetComponentInChildren<NetworkAnimator>();
        if (netAnimator != null && AccessoryAnimator != null)
        {
            netAnimator.SetAnimator(AccessoryAnimator);
            Debug.Log($"[AccessoryHolder] NetworkAnimator configured for '{Accessory.name}'");
        }

        ApplyAccessorySorting(Accessory);

        Debug.Log($"[AccessoryHolder] SetupNetworkAccessory complete for '{Accessory.name}'");
    }

    private void ApplyAccessorySorting(GameObject accessory)
    {
        SpriteRenderer AccessoryRenderer = FindAccessoryRenderer(accessory);
        if (AccessoryRenderer == null || playerSpriteRenderer == null)
            return;

        AccessoryRenderer.sortingLayerName = playerSpriteRenderer.sortingLayerName;
        AccessoryRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + AccessorySortingOrder;
    }

    /// <summary>Finds the AccessorySprite renderer, falling back to the first non-HandHolder renderer.</summary>
    private static SpriteRenderer FindAccessoryRenderer(GameObject accessory)
    {
        Transform AccessorySpriteChild = accessory.transform.Find("AccessorySprite");
        if (AccessorySpriteChild != null)
        {
            SpriteRenderer spriteChildRenderer = AccessorySpriteChild.GetComponent<SpriteRenderer>();
            if (spriteChildRenderer != null)
                return spriteChildRenderer;
        }

        foreach (SpriteRenderer sr in accessory.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!sr.gameObject.name.Contains("HandHolder"))
                return sr;
        }
        return null;
    }

    /// <summary>Returns the named 'AccessoryHolder' child transform, creating it if absent.</summary>
    public Transform EnsureAccessoryHolderChildExists()
    {
        if (string.Equals(transform.name, "AccessoryHolder", StringComparison.Ordinal))
            return transform;

        Transform AccessoryHolderTransform = transform.Find("AccessoryHolder");
        if (AccessoryHolderTransform == null)
        {
            GameObject AccessoryHolderObj = new GameObject("AccessoryHolder");
            AccessoryHolderObj.transform.SetParent(transform);
            AccessoryHolderObj.transform.localPosition = Vector3.zero;
            AccessoryHolderObj.transform.localRotation = Quaternion.identity;
            AccessoryHolderObj.transform.localScale = Vector3.one;
            AccessoryHolderTransform = AccessoryHolderObj.transform;
        }
        return AccessoryHolderTransform;
    }

    public bool HasAccessories() => equippedAccessories.Count > 0;

    public IReadOnlyList<GameObject> GetEquippedAccessories() => equippedAccessories;

}