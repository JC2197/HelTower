using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Handles panning (right-click drag) and zooming (mouse wheel) for the trait tree panel.
/// </summary>
public class TraitTreePanZoom : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IScrollHandler
{
    [Header("Zoom Settings")]
    [SerializeField] private RectTransform contentPanel; // The panel containing nodes
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float minZoom = 0.5f;
            [SerializeField] private float maxZoom = 5f;
    
    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 1f;
    [SerializeField] private bool clampPanning = true;
    [SerializeField] private float panBoundsPadding = 500f; // How far outside the content you can pan
    
    private float currentZoom = 1f;
    private bool isPanning = false;
    private Vector2 lastMousePosition;
    private Vector2 contentStartPosition;
    private int lastScrollEventFrame = -1;

    private void Start()
    {
        if (contentPanel == null)
        {
            contentPanel = GetComponent<RectTransform>();
            Debug.LogWarning("[TraitTreePanZoom] Content panel not assigned, using self");
        }
        
        contentStartPosition = contentPanel.anchoredPosition;
    }
    
    private void Update()
    {
        HandleZoom();
        HandlePanning();
    }
    
    /// <summary>
    /// EventSystem scroll handler — fires when the cursor is over the tree content
    /// (nodes/connections bubble up to this ContentPanel). This is the reliable path
    /// in projects using the new Input System UI module, matching CraftingTreePanZoom.
    /// </summary>
    public void OnScroll(PointerEventData eventData)
    {
        float scrollDelta = eventData.scrollDelta.y;
        if (Mathf.Abs(scrollDelta) < 0.01f) return;

        lastScrollEventFrame = Time.frameCount;
        ApplyZoom(scrollDelta, eventData.position, eventData.pressEventCamera);
    }

    /// <summary>
    /// Fallback device polling so zoom still works when the cursor is over empty
    /// canvas space (which has no raycast target to deliver an OnScroll event).
    /// Skipped on frames where OnScroll already handled the wheel to avoid double zoom.
    /// </summary>
    private void HandleZoom()
    {
        if (Mouse.current == null) return;
        if (lastScrollEventFrame == Time.frameCount) return; // already handled by OnScroll

        float rawScroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(rawScroll) < 0.01f) return;

        // Device delta is ~120 per notch on Windows; normalise to one step per notch
        // so a single tick doesn't snap straight to the zoom clamp.
        float scrollDelta = Mathf.Sign(rawScroll);
        ApplyZoom(scrollDelta, Mouse.current.position.ReadValue(), null);
    }

    /// <summary>
    /// Apply a zoom step centred on a screen position, scaling the content panel
    /// and shifting its anchor so the zoom focuses towards the cursor.
    /// </summary>
    private void ApplyZoom(float scrollDelta, Vector2 screenPos, Camera cam)
    {
        // Get pointer position in content space before zoom
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contentPanel,
            screenPos,
            cam,
            out Vector2 localMousePos
        );

        float zoomDelta = scrollDelta * zoomSpeed;
        float newZoom = Mathf.Clamp(currentZoom + zoomDelta, minZoom, maxZoom);

        if (Mathf.Abs(newZoom - currentZoom) <= 0.001f) return;

        // Calculate zoom factor
        float zoomFactor = newZoom / currentZoom;

        // Apply zoom
        contentPanel.localScale = Vector3.one * newZoom;
        currentZoom = newZoom;

        // Adjust position to zoom towards the cursor
        Vector2 newPos = contentPanel.anchoredPosition - (localMousePos * (zoomFactor - 1f));
        contentPanel.anchoredPosition = ClampPosition(newPos);
    }
    
    /// <summary>
    /// Handle right-click drag panning
    /// </summary>
    private void HandlePanning()
    {
        if (Mouse.current == null) return;
        
        // Check for right mouse button
        bool rightMouseDown = Mouse.current.rightButton.isPressed;
        
        if (rightMouseDown)
        {
            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            
            if (!isPanning)
            {
                // Start panning
                isPanning = true;
                lastMousePosition = currentMousePos;
            }
            else
            {
                // Continue panning
                Vector2 delta = (currentMousePos - lastMousePosition) * panSpeed;
                Vector2 newPos = contentPanel.anchoredPosition + delta;
                contentPanel.anchoredPosition = ClampPosition(newPos);
                lastMousePosition = currentMousePos;
            }
        }
        else if (isPanning)
        {
            // Stop panning
            isPanning = false;
        }
    }
    
    /// <summary>
    /// Clamp panning to reasonable bounds
    /// </summary>
    private Vector2 ClampPosition(Vector2 position)
    {
        if (!clampPanning)
            return position;
        
        // Get the size of the content
        Vector2 contentSize = contentPanel.sizeDelta * currentZoom;
        
        // Calculate max pan distance from starting position
        float maxX = contentSize.x / 2f + panBoundsPadding;
        float maxY = contentSize.y / 2f + panBoundsPadding;
        
        // Clamp relative to start position
        position.x = Mathf.Clamp(position.x, contentStartPosition.x - maxX, contentStartPosition.x + maxX);
        position.y = Mathf.Clamp(position.y, contentStartPosition.y - maxY, contentStartPosition.y + maxY);
        
        return position;
    }
    
    /// <summary>
    /// Reset zoom and pan to defaults
    /// </summary>
    public void ResetView()
    {
        currentZoom = 1f;
        contentPanel.localScale = Vector3.one;
        contentPanel.anchoredPosition = contentStartPosition;
        isPanning = false;
    }
    
    /// <summary>
    /// Focus on a specific position in the content
    /// </summary>
    public void FocusOnPosition(Vector2 worldPosition, float zoom = 1f)
    {
        currentZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        contentPanel.localScale = Vector3.one * currentZoom;
        
        // Center the position
        contentPanel.anchoredPosition = ClampPosition(-worldPosition * currentZoom);
    }
    
    // IPointerHandler implementation (for additional pointer-based events if needed)
    public void OnPointerDown(PointerEventData eventData)
    {
        // Can be used for additional click detection
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        // Can be used for additional click detection
    }
}
