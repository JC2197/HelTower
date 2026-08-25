using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Handles pan and zoom navigation for the trait tree UI.
/// Attach this to the node container RectTransform.
/// </summary>
public class TraitTreeNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [Header("Zoom Settings")]
    [SerializeField] private float minZoom = 0.2f;
    [SerializeField] private float maxZoom = 8f;
    [SerializeField] private float zoomSpeed = 0.1f;
    
    [Header("Pan Settings")]
    [SerializeField] private bool enablePan = true;
    [SerializeField] private float panSpeed = 1f;
    [SerializeField] private bool enableLeftClickDrag = true; // Allow left-click drag
    
    [Header("Bounds (Optional)")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private Vector2 minBounds = new Vector2(-2000, -2000);
    [SerializeField] private Vector2 maxBounds = new Vector2(2000, 2000);
    
    [Header("References")]
    [SerializeField] private RectTransform contentRect; // The main container (optional if using multiple containers)
    [SerializeField] private RectTransform[] additionalContainers; // Additional containers to move together (e.g., nodeContainer, connectionContainer)
    
    private Vector2 lastMousePosition;
    private bool isDragging = false;
    private float currentZoom = 0.5f;
    private Vector2 panOffset = Vector2.zero;
    
    private void Awake()
    {
        if (contentRect == null)
        {
            contentRect = GetComponent<RectTransform>();
        }
    }
    
    private void Update()
    {
        // Handle keyboard zoom (+ and - keys)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.equalsKey.wasPressedThisFrame || Keyboard.current.numpadPlusKey.wasPressedThisFrame)
            {
                ZoomIn();
            }
            if (Keyboard.current.minusKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame)
            {
                ZoomOut();
            }
        }
        
        // Handle mouse button drag for panning (both middle and left)
        if (Mouse.current != null && enablePan)
        {
            // Middle mouse button (always enabled)
            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                lastMousePosition = Mouse.current.position.ReadValue();
                isDragging = true;
            }
            
            if (Mouse.current.middleButton.isPressed && isDragging)
            {
                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                Vector2 delta = (currentMousePos - lastMousePosition) * panSpeed;
                Pan(delta);
                lastMousePosition = currentMousePos;
            }
            
            if (Mouse.current.middleButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }
        }
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!enablePan) return;
        
        // Allow middle mouse or left mouse (if enabled)
        if (eventData.button == PointerEventData.InputButton.Middle || 
            (enableLeftClickDrag && eventData.button == PointerEventData.InputButton.Left))
        {
            lastMousePosition = eventData.position;
            isDragging = true;
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!enablePan || !isDragging) return;
        
        // Allow middle mouse or left mouse (if enabled)
        if (eventData.button == PointerEventData.InputButton.Middle || 
            (enableLeftClickDrag && eventData.button == PointerEventData.InputButton.Left))
        {
            Vector2 delta = (eventData.position - lastMousePosition) * panSpeed;
            Pan(delta);
            lastMousePosition = eventData.position;
        }
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Middle || 
            (enableLeftClickDrag && eventData.button == PointerEventData.InputButton.Left))
        {
            isDragging = false;
        }
    }
    
    public void OnScroll(PointerEventData eventData)
    {
        // Zoom with mouse wheel
        float scrollDelta = eventData.scrollDelta.y;
        
        if (scrollDelta > 0)
        {
            ZoomIn();
        }
        else if (scrollDelta < 0)
        {
            ZoomOut();
        }
    }
    
    private void ZoomIn()
    {
        SetZoom(currentZoom + zoomSpeed);
    }
    
    private void ZoomOut()
    {
        SetZoom(currentZoom - zoomSpeed);
    }
    
    private void SetZoom(float newZoom)
    {
        currentZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);
        
        if (contentRect != null)
        {
            contentRect.localScale = Vector3.one * currentZoom;
        }
        
        // Apply to additional containers
        if (additionalContainers != null)
        {
            foreach (var container in additionalContainers)
            {
                if (container != null)
                {
                    container.localScale = Vector3.one * currentZoom;
                }
            }
        }
    }
    
    private void Pan(Vector2 delta)
    {
        panOffset += delta;
        
        // Apply bounds if enabled
        if (useBounds)
        {
            panOffset.x = Mathf.Clamp(panOffset.x, minBounds.x, maxBounds.x);
            panOffset.y = Mathf.Clamp(panOffset.y, minBounds.y, maxBounds.y);
        }
        
        if (contentRect != null)
        {
            contentRect.anchoredPosition = panOffset;
        }
        
        // Apply to additional containers
        if (additionalContainers != null)
        {
            foreach (var container in additionalContainers)
            {
                if (container != null)
                {
                    container.anchoredPosition = panOffset;
                }
            }
        }
    }
    
    /// <summary>
    /// Reset zoom and pan to default
    /// </summary>
    public void ResetView()
    {
        currentZoom = 1f;
        panOffset = Vector2.zero;
        
        if (contentRect != null)
        {
            contentRect.localScale = Vector3.one;
            contentRect.anchoredPosition = Vector2.zero;
        }
        
        // Reset additional containers
        if (additionalContainers != null)
        {
            foreach (var container in additionalContainers)
            {
                if (container != null)
                {
                    container.localScale = Vector3.one;
                    container.anchoredPosition = Vector2.zero;
                }
            }
        }
    }
    
    /// <summary>
    /// Center view on a specific position
    /// </summary>
    public void CenterOn(Vector2 position)
    {
        panOffset = -position * currentZoom;
        
        // Apply bounds
        if (useBounds)
        {
            panOffset.x = Mathf.Clamp(panOffset.x, minBounds.x, maxBounds.x);
            panOffset.y = Mathf.Clamp(panOffset.y, minBounds.y, maxBounds.y);
        }
        
        if (contentRect != null)
        {
            contentRect.anchoredPosition = panOffset;
        }
        
        // Apply to additional containers
        if (additionalContainers != null)
        {
            foreach (var container in additionalContainers)
            {
                if (container != null)
                {
                    container.anchoredPosition = panOffset;
                }
            }
        }
    }
}
