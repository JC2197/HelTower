using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Base class for all tooltip systems providing common functionality for positioning,
/// showing/hiding, and following the mouse cursor.
/// Inherit from this to create specialized tooltips (items, abilities, stats, etc.)
/// </summary>
public abstract class TooltipBase : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] protected GameObject tooltipPanel;
    
    [Header("Settings")]
    [SerializeField] protected float appearDelay = 0f;
    
    protected RectTransform tooltipRect;
    protected float showTimer;
    protected bool isShowing;
    
    protected virtual void Awake()
    {
        if (tooltipPanel != null)
        {
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            tooltipPanel.SetActive(false);
            
            // Ensure tooltip renders on top by moving to end of hierarchy
            tooltipPanel.transform.SetAsLastSibling();
        }
        
        // Make tooltip ignore raycasts to prevent flickering
        if (tooltipPanel != null)
        {
            CanvasGroup canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = tooltipPanel.AddComponent<CanvasGroup>();
            }
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            canvasGroup.alpha = 1f;
        }
    }
    
    protected virtual void Update()
    {

    }
    
    /// <summary>
    /// Show the tooltip. Override this in derived classes to set specific content.
    /// </summary>
    protected virtual void ShowTooltip()
    {
       
    }
    
    /// <summary>
    /// Hide the tooltip
    /// </summary>
    public virtual void HideTooltip()
    {
        isShowing = false;
        showTimer = 0f;
        
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// Update tooltip position to follow mouse with bounds checking
    /// </summary>
    protected virtual void UpdatePosition()
    {
        
    }
    
    /// <summary>
    /// Helper method to safely set text on a TextMeshProUGUI component
    /// </summary>
    protected void SetText(TextMeshProUGUI textComponent, string text)
    {
    }
    
    /// <summary>
    /// Helper method to show/hide a GameObject based on whether text is provided
    /// </summary>
    protected void SetTextAndVisibility(TextMeshProUGUI textComponent, string text)
    {
       
    }
}

