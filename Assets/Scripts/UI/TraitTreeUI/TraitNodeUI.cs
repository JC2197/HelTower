using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI component for a single trait node in the tree.
/// Handles visual state, tooltips, and click interactions.
/// </summary>
public class TraitNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject lockedOverlay;

    [Header("Node Backgrounds by Type")]
    [SerializeField] private Sprite minorNodeBackground;
    [SerializeField] private Sprite majorNodeBackground;
    [SerializeField] private Sprite keystoneNodeBackground;

    // Runtime data
    private TraitNode nodeData;
    private TraitTreeUI treeUI;
    private TraitNodeState currentState = TraitNodeState.Locked;
    private bool isHovered = false;
    private Image lockedOverlayImage; // Cached reference to overlay image
    public TraitNode NodeData => nodeData;
    public TraitNodeState State => currentState;

    /// <summary>
    /// Initialize the node with data. Node widgets are spawned bare by TraitTreeUI, so the
    /// background/icon/overlay images are created here when they are not prefab-assigned.
    /// </summary>
    public void Initialize(TraitNode node, TraitTreeUI parentTreeUI, Sprite iconFrame = null)
    {
        nodeData = node;
        treeUI = parentTreeUI;

        EnsureRuntimeVisuals(iconFrame);

        if (lockedOverlay != null)
        {
            lockedOverlayImage = lockedOverlay.GetComponent<Image>();
        }

        if (iconImage != null)
        {
            iconImage.enabled = true;
        }

        // Set initial state
        UpdateVisualState(TraitNodeState.Locked);
    }

    private void OnDisable()
    {
        isHovered = false;

        // Force the tooltip to hide if this specific node was the one showing it
        if (treeUI != null)
        {
            treeUI.HideTooltip();
        }
    }

    private void EnsureRuntimeVisuals(Sprite iconFrame)
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (backgroundImage != null)
        {
            backgroundImage.sprite = iconFrame;
            // A frameless node stays invisible but must still receive raycasts to be clickable.
            backgroundImage.color = iconFrame != null ? Color.white : new Color(0f, 0f, 0f, 0f);
            backgroundImage.raycastTarget = true;
        }

        if (iconImage == null)
            iconImage = CreateChildImage("Icon", Color.white);

        if (lockedOverlay == null)
            lockedOverlay = CreateChildImage("LockedOverlay", Color.black).gameObject;
    }

    private Image CreateChildImage(string childName, Color color)
    {
        var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;

        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    /// <summary>
    /// Update the visual state of the node
    /// </summary>
    public void UpdateVisualState(TraitNodeState newState)
    {
        currentState = newState;

        // Adjust locked overlay alpha based on state
        if (lockedOverlayImage != null)
        {
            Color overlayColor = lockedOverlayImage.color;

            switch (newState)
            {
                case TraitNodeState.Locked:
                    overlayColor.a = 0.99f; // 99% opacity - almost fully black
                    lockedOverlay.SetActive(true);
                    break;
                case TraitNodeState.CannotAfford:
                    overlayColor.a = 0.90f; // Reachable but unaffordable
                    lockedOverlay.SetActive(true);
                    break;
                case TraitNodeState.Available:
                    overlayColor.a = 0.80f; // 80% opacity - slightly lighter
                    lockedOverlay.SetActive(true);
                    break;
                case TraitNodeState.Unlocked:
                    overlayColor.a = 0f; // Fully transparent
                    lockedOverlay.SetActive(false); // Disable for performance
                    break;
            }

            lockedOverlayImage.color = overlayColor;
        }

        // Set icon
        if (iconImage != null && nodeData?.traitData != null)
        {
            if (nodeData.traitData.traitIcon != null)
                iconImage.sprite = nodeData.traitData.traitIcon;
        }
    }

    /// <summary>
    /// Handle mouse enter
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        UpdateVisualState(currentState);

        // Show tooltip
        if (nodeData?.traitData != null && treeUI != null)
        {
            treeUI.ShowTooltip(nodeData.traitData, this);
        }
    }

    /// <summary>
    /// Handle mouse exit
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        UpdateVisualState(currentState);

        // If the window or tree UI is already disabling, stop here
        if (treeUI == null || !treeUI.gameObject.activeInHierarchy) return;

        treeUI.HideTooltip();
    }

    /// <summary>
    /// Handle click
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (treeUI != null && currentState == TraitNodeState.Available)
        {
            treeUI.OnNodeClicked(this);
        }
    }

    /// <summary>
    /// Get the world position of this node for path drawing
    /// </summary>
    public Vector2 GetWorldPosition()
    {
        return transform.position;
    }
}

/// <summary>
/// Possible states for a trait node
/// </summary>
public enum TraitNodeState
{
    Locked,        // Requirements not met
    Available,     // Requirements met, can be unlocked
    CannotAfford,  // Requirements met, but the node's cost is unaffordable
    Unlocked       // Already unlocked
}
