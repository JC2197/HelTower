using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Data structure for ability tooltip display.
/// </summary>
[System.Serializable]
public class AbilityTooltipData
{
    public string abilityName;
    public string description;
    public float energyCost;
    public float cooldown;
    
    public AbilityTooltipData()
    {
        abilityName = "";
        description = "";
        energyCost = 0f;
        cooldown = 0f;
    }
}

/// <summary>
/// Displays tooltips for ability slots showing ability details
/// </summary>
public class AbilityTooltip : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI abilityNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI energyCostText;
    [SerializeField] private TextMeshProUGUI cooldownText;
    
    [Header("Settings")]
    [SerializeField] private float appearDelay = 0f;
    
    private RectTransform tooltipRect;
    private float showTimer;
    private bool isShowing;
    
    private static AbilityTooltip instance;
    public static AbilityTooltip Instance => instance;
    
    void Awake()
    {
        if (tooltipPanel != null)
        {
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            tooltipPanel.SetActive(false);
            
            // Ensure tooltip renders on top
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
            }
            
            // Follow mouse
            UpdatePosition();
        }
    }
    
    /// <summary>
    /// Show tooltip with ability data
    /// </summary>
    public void ShowTooltip(AbilityTooltipData data)
    {
        if (tooltipPanel == null || tooltipRect == null || data == null)
            return;
        
        isShowing = true;
        showTimer = 0f;
        
        // Set ability fields
        SetAbilityName(data.abilityName);
        SetDescription(data.description);
        SetEnergyCost(data.energyCost);
        SetCooldown(data.cooldown);
        
        // Force layout rebuild
        Canvas.ForceUpdateCanvases();
        
        // Activate immediately if no delay
        if (appearDelay <= 0f && !tooltipPanel.activeSelf)
        {
            tooltipPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// Show tooltip with ability details (legacy method for backward compatibility)
    /// </summary>
    public void ShowTooltip(string abilityName, string description, float energyCost, float cooldown)
    {
        AbilityTooltipData data = new AbilityTooltipData
        {
            abilityName = abilityName,
            description = description,
            energyCost = energyCost,
            cooldown = cooldown
        };
        ShowTooltip(data);
    }
    
    private void SetAbilityName(string name)
    {
        if (abilityNameText != null)
        {
            abilityNameText.text = name ?? "";
        }
    }
    
    private void SetDescription(string description)
    {
        if (descriptionText != null)
        {
            if (!string.IsNullOrEmpty(description))
            {
                descriptionText.text = description;
                descriptionText.gameObject.SetActive(true);
            }
            else
            {
                descriptionText.gameObject.SetActive(false);
            }
        }
    }
    
    private void SetEnergyCost(float energyCost)
    {
        if (energyCostText != null)
        {
            energyCostText.text = energyCost > 0 ? $"Energy: {energyCost:F0}" : "Energy: None";
        }
    }
    
    private void SetCooldown(float cooldown)
    {
        if (cooldownText != null)
        {
            cooldownText.text = cooldown > 0 ? $"Cooldown: {cooldown:F1}s" : "Cooldown: None";
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
        if (tooltipPanel == null || !tooltipPanel.activeSelf || tooltipRect == null) return;
        
        Vector2 mousePos;
        
        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            return;
        }
        
        // Get canvas for positioning
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;
        
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            tooltipRect.pivot = new Vector2(0, 1);
            tooltipRect.position = mousePos;
            
            // Bounds checking
            Vector3[] corners = new Vector3[4];
            tooltipRect.GetWorldCorners(corners);
            
            if (corners[2].x > Screen.width)
            {
                tooltipRect.pivot = new Vector2(1, 1);
                tooltipRect.position = mousePos;
                tooltipRect.GetWorldCorners(corners);
            }
            
            if (corners[0].y < 0)
            {
                tooltipRect.pivot = new Vector2(tooltipRect.pivot.x, 0);
                tooltipRect.position = mousePos;
            }
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            if (tooltipRect.parent != canvasRect)
            {
                tooltipRect.SetParent(canvasRect, false);
            }
            
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                mousePos,
                canvas.worldCamera,
                out localPoint))
            {
                tooltipRect.pivot = new Vector2(0, 1);
                tooltipRect.anchoredPosition = localPoint;
            }
        }
    }
}
