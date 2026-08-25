using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Displays tooltips for trait nodes showing the trait name and description
/// </summary>
public class TraitTooltip : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI traitNameText;
    [SerializeField] private TextMeshProUGUI traitDescriptionText;
    
    [Header("Settings")]
    [SerializeField] private float appearDelay = 0f;
    
    private RectTransform tooltipRect;
    private float showTimer;
    private bool isShowing;
    
    private static TraitTooltip instance;
    public static TraitTooltip Instance => instance;
    
    void Awake()
    {
        Debug.Log("[TraitTooltip] Awake called");
        
        if (tooltipPanel == null)
        {
            Debug.LogError("[TraitTooltip] tooltipPanel is not assigned in inspector!");
            return;
        }
        
        tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        if (tooltipRect == null)
        {
            Debug.LogError("[TraitTooltip] tooltipPanel doesn't have a RectTransform!");
        }
        
        tooltipPanel.SetActive(false);
        Debug.Log("[TraitTooltip] Tooltip panel initialized and hidden");
        
        // Ensure tooltip renders on top by moving to end of hierarchy
        tooltipPanel.transform.SetAsLastSibling();
        
        // Log hierarchy
        Transform current = transform;
        string hierarchy = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            hierarchy = current.name + "/" + hierarchy;
        }
        Debug.Log($"[TraitTooltip] Hierarchy: {hierarchy}");
        Debug.Log($"[TraitTooltip] Local scale: {transform.localScale}, World scale: {transform.lossyScale}");
        
        // Check canvas setup
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"[TraitTooltip] Found parent canvas: {canvas.name}, render mode: {canvas.renderMode}, sorting order: {canvas.sortingOrder}");
        }
        else
        {
            Debug.LogError("[TraitTooltip] No parent Canvas found!");
        }
        
        // Check for nested canvas
        Canvas[] childCanvases = GetComponentsInChildren<Canvas>();
        foreach (var childCanvas in childCanvases)
        {
            Debug.Log($"[TraitTooltip] Found child canvas: {childCanvas.name}, render mode: {childCanvas.renderMode}, sorting order: {childCanvas.sortingOrder}");
            
            // Force to ScreenSpaceOverlay if not already
            if (childCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Debug.LogWarning($"[TraitTooltip] Canvas {childCanvas.name} is {childCanvas.renderMode}, forcing to ScreenSpaceOverlay!");
                childCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                childCanvas.sortingOrder = 100;
            }
        }
        
        // Make tooltip ignore raycasts to prevent flickering
        CanvasGroup canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = tooltipPanel.AddComponent<CanvasGroup>();
            Debug.Log("[TraitTooltip] Added CanvasGroup to tooltip panel");
        }
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 1f;
        Debug.Log($"[TraitTooltip] CanvasGroup alpha set to: {canvasGroup.alpha}");
    }
    
    void OnEnable()
    {
        instance = this;
    }

    void OnDisable()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (isShowing)
        {
            showTimer += Time.deltaTime;
            
            if (showTimer >= appearDelay && tooltipPanel != null && !tooltipPanel.activeSelf)
            {
                tooltipPanel.SetActive(true);
                // Check visibility factors
                Image img = tooltipPanel.GetComponent<Image>();
                if (img != null)
                {
                    Debug.Log($"[TraitTooltip] Image enabled: {img.enabled}, color: {img.color}, sprite: {(img.sprite != null ? img.sprite.name : "null")}");
                }
                
                if (tooltipRect != null)
                {
                    Vector3[] corners = new Vector3[4];
                    tooltipRect.GetWorldCorners(corners);
                }
            }
            
            // Follow mouse
            UpdatePosition();
        }
    }
    
    /// <summary>
    /// Show tooltip with trait name and description
    /// </summary>
    public void ShowTooltip(string traitName, string traitDescription)
    {
        Debug.Log($"[TraitTooltip] ShowTooltip called with: '{traitName}'");
        
        if (tooltipPanel == null)
        {
            Debug.LogWarning("[TraitTooltip] ShowTooltip called but tooltipPanel is null!");
            return;
        }
        
        isShowing = true;
        showTimer = 0f;
        
        // Set text content
        if (traitNameText != null)
        {
            traitNameText.text = traitName;
        }
        else
        {
            Debug.LogWarning("[TraitTooltip] traitNameText is null!");
        }
        
        if (traitDescriptionText != null)
        {
            // Only show description if it's not empty
            if (!string.IsNullOrEmpty(traitDescription))
            {
                traitDescriptionText.gameObject.SetActive(true);
                traitDescriptionText.text = traitDescription;
            }
            else
            {
                traitDescriptionText.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning("[TraitTooltip] traitDescriptionText is null!");
        }
    }
    
    /// <summary>
    /// Hide the tooltip
    /// </summary>
    public void HideTooltip()
    {
        isShowing = false;
        showTimer = 0f;
        
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// Update tooltip position to follow mouse
    /// </summary>
    void UpdatePosition()
    {
        if (tooltipPanel == null || !tooltipPanel.activeSelf || tooltipRect == null) 
            return;
        
        Vector2 mousePos;
        
        // Get mouse position
        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            Debug.LogWarning("[TraitTooltip] Mouse.current is null in UpdatePosition!");
            return;
        }
        
        // Get the nested canvas (child of this GameObject, not parent)
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[TraitTooltip] Could not find child canvas in UpdatePosition!");
            return;
        }
        
        
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;
        
        // For ScreenSpaceOverlay, we position the Canvas itself, not the panel inside it
        RectTransform transformToPosition = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? canvasRect : tooltipRect;
        
        // Handle different canvas render modes
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // For overlay mode, use direct screen position on the CANVAS
            transformToPosition.pivot = new Vector2(0, 1);
            
            transformToPosition.position = mousePos;
            
            
            // Keep within canvas bounds
            Vector3[] corners = new Vector3[4];
            tooltipRect.GetWorldCorners(corners);
            
            // If tooltip goes off right edge, flip to left of cursor
            if (corners[2].x > Screen.width)
            {
                transformToPosition.pivot = new Vector2(1, 1); // Top-right pivot
                transformToPosition.position = mousePos;
                tooltipRect.GetWorldCorners(corners);
            }
            
            // If tooltip goes off bottom edge, flip to above cursor
            if (corners[0].y < 0)
            {
                transformToPosition.pivot = new Vector2(transformToPosition.pivot.x, 0); // Keep horizontal pivot, switch to bottom
                transformToPosition.position = mousePos;
            }
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {

            // Convert screen to local canvas space
            Vector2 localPoint;
            bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                mousePos,
                canvas.worldCamera,
                out localPoint);
            
            
            if (converted)
            {
                // Set pivot and position in local space
                tooltipRect.pivot = new Vector2(0, 1);
                tooltipRect.anchoredPosition = localPoint;
                
            }
            else
            {
                Debug.LogWarning($"[TraitTooltip] Failed to convert screen point to local point! Camera: {canvas.worldCamera}");
            }
            
            // Keep within canvas bounds in local space
            Rect canvasRect_local = canvasRect.rect;
            Vector2 tooltipSize = tooltipRect.rect.size;
            Vector2 currentPos = tooltipRect.anchoredPosition;
            
            // Check right edge
            if (currentPos.x + tooltipSize.x > canvasRect_local.xMax)
            {
                tooltipRect.pivot = new Vector2(1, 1);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    mousePos,
                    canvas.worldCamera,
                    out localPoint))
                {
                    tooltipRect.anchoredPosition = localPoint;
                }
            }
            
            // Check bottom edge
            currentPos = tooltipRect.anchoredPosition;
            if (currentPos.y - tooltipSize.y < canvasRect_local.yMin)
            {
                tooltipRect.pivot = new Vector2(tooltipRect.pivot.x, 0);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    mousePos,
                    canvas.worldCamera,
                    out localPoint))
                {
                    tooltipRect.anchoredPosition = localPoint;
                }
            }
        }
    }
}
