using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Pan and zoom for the weapon/armor crafting tree panel.
/// Attach to the scroll/viewport root that contains both the pixel-art
/// tree image and the interactive node widgets.
///
/// Default zoom = 1 (background native resolution).
/// Max zoom     = 5 (500 %).
///
/// Controls:
///   Mouse wheel      — zoom towards cursor
///   Right-click drag — pan
/// </summary>
public class CraftingTreePanZoom : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IScrollHandler
{
    [Header("Target")]
    [Tooltip("The RectTransform that will be scaled and translated — should be the direct parent of both the tree image and the node container.")]
    [SerializeField] private RectTransform contentPanel;
    [Header("Viewport Bounds")]
    [Tooltip("The RectTransform that defines the visible area. Usually the parent/viewport containing the content.")]
    [SerializeField] private RectTransform viewport;
    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 5f;

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 1f;
    [SerializeField] private bool clampPanning = true;
    [SerializeField] private float panBoundsPadding = 300f;

    private float currentZoom = 1f;
    private float baseScale = 1f;
    private bool isPanning = false;
    private Vector2 lastMousePosition;
    private Vector2 contentStartPosition;

    private void Start()
    {
        if (contentPanel == null)
        {
            contentPanel = GetComponent<RectTransform>();
            Debug.LogWarning("[CraftingTreePanZoom] contentPanel not assigned — using self.");
        }
        if (viewport == null)
    {
        viewport = contentPanel.parent as RectTransform;

        if (viewport != null)
            Debug.Log($"[CraftingTreePanZoom] Viewport auto-assigned: {viewport.name}");
        else
            Debug.LogWarning("[CraftingTreePanZoom] Could not determine viewport.");
    }

        contentStartPosition = contentPanel.anchoredPosition;
        currentZoom = Mathf.Clamp(1f, minZoom, maxZoom);
        contentPanel.localScale = Vector3.one * currentZoom * baseScale;
    }

    /// <summary>
    /// Reassign the panel that pan/zoom operates on, resetting zoom/pan state.
    /// Used when the tree UI groups the baked picture + interactive node layer under a
    /// single content wrapper at runtime so they zoom/pan together.
    /// </summary>
    /// <param name="fitScale">
    /// Scale that fits the tree's authored canvas size into the panel's actual RectTransform
    /// size, so a smaller/larger root window is always reflected by the content — not just
    /// a fixed 1:1 pixel scale. 1x zoom means "fit to window".
    /// </param>
    public void SetContentPanel(RectTransform panel, float fitScale = 1f)
    {
        if (panel == null) return;
        contentPanel = panel;
        baseScale = Mathf.Max(0.01f, fitScale);
        if (viewport == null)
        viewport = contentPanel.parent as RectTransform;
        contentStartPosition = panel.anchoredPosition;
        currentZoom = Mathf.Clamp(1f, minZoom, maxZoom);
        panel.localScale = Vector3.one * currentZoom * baseScale;
    }

    private void Update()
    {
        HandlePanning();
    }

    // ─── IScrollHandler (EventSystem) ─────────────────────────────────────────
    // Using IScrollHandler means the zoom only fires when the cursor is inside
    // the panel's RectTransform, preventing accidental zoom while scrolling other UI.

    public void OnScroll(PointerEventData eventData)
    {
        float scrollDelta = eventData.scrollDelta.y;

        if (Mathf.Abs(scrollDelta) < 0.01f) return;

        // Mouse position in local content space before zoom.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contentPanel,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localMousePos);

        float newZoom = Mathf.Clamp(currentZoom + scrollDelta * zoomSpeed, minZoom, maxZoom);

        if (Mathf.Abs(newZoom - currentZoom) < 0.001f) return;

        float zoomFactor = newZoom / currentZoom;
        contentPanel.localScale = Vector3.one * newZoom * baseScale;
        currentZoom = newZoom;

        // Shift anchor so we zoom towards the cursor position.
        Vector2 newPos = contentPanel.anchoredPosition - (localMousePos * (zoomFactor - 1f));
        contentPanel.anchoredPosition = ClampPosition(newPos);
    }

    // ─── Panning ──────────────────────────────────────────────────────────────

    private void HandlePanning()
    {
        if (Mouse.current == null) return;

        bool rightDown = Mouse.current.rightButton.isPressed;

        if (rightDown)
        {
            Vector2 curPos = Mouse.current.position.ReadValue();
            if (!isPanning)
            {
                isPanning = true;
                lastMousePosition = curPos;
            }
            else
            {
                Vector2 delta = (curPos - lastMousePosition) * panSpeed;
                contentPanel.anchoredPosition = ClampPosition(contentPanel.anchoredPosition + delta);
                lastMousePosition = curPos;
            }
        }
        else if (isPanning)
        {
            isPanning = false;
        }
    }

    private Vector2 ClampPosition(Vector2 position)
{
    if (!clampPanning || contentPanel == null || viewport == null)
        return position;

    Rect parentRect = viewport.rect;
    Rect contentRect = contentPanel.rect;

    float scale = currentZoom * baseScale;

    // Content dimensions after zoom.
    float contentWidth = contentRect.width * scale;
    float contentHeight = contentRect.height * scale;

    float viewportWidth = parentRect.width;
    float viewportHeight = parentRect.height;

    // Account for the content pivot.
    float pivotOffsetX = contentRect.center.x * scale;
    float pivotOffsetY = contentRect.center.y * scale;

    // Content pivot position relative to the viewport.
    Vector2 pivotPosition = position;

    // ------------------------------------------------------------------
    // X
    // ------------------------------------------------------------------

    if (contentWidth <= viewportWidth)
    {
        // Content is smaller than viewport — keep it centred.
        pivotPosition.x = contentStartPosition.x - pivotOffsetX;
    }
    else
    {
        float halfContentWidth = contentWidth * 0.5f;
        float halfViewportWidth = viewportWidth * 0.5f;

        float minX =
            contentStartPosition.x
            - halfContentWidth
            + halfViewportWidth
            - pivotOffsetX;

        float maxX =
            contentStartPosition.x
            + halfContentWidth
            - halfViewportWidth
            - pivotOffsetX;

        pivotPosition.x = Mathf.Clamp(
            position.x,
            minX - panBoundsPadding,
            maxX + panBoundsPadding
        );
    }

    // ------------------------------------------------------------------
    // Y
    // ------------------------------------------------------------------

    if (contentHeight <= viewportHeight)
    {
        // Content is smaller than viewport — keep it centred.
        pivotPosition.y = contentStartPosition.y - pivotOffsetY;
    }
    else
    {
        float halfContentHeight = contentHeight * 0.5f;
        float halfViewportHeight = viewportHeight * 0.5f;

        float minY =
            contentStartPosition.y
            - halfContentHeight
            + halfViewportHeight
            - pivotOffsetY;

        float maxY =
            contentStartPosition.y
            + halfContentHeight
            - halfViewportHeight
            - pivotOffsetY;

        pivotPosition.y = Mathf.Clamp(
            position.y,
            minY - panBoundsPadding,
            maxY + panBoundsPadding
        );
    }

    return pivotPosition;
}

    // ─── IPointerDownHandler / IPointerUpHandler ──────────────────────────────
    // Required by IScrollHandler to work inside a ScrollRect hierarchy; can also
    // be used to block parent scroll on pointer down.

    public void OnPointerDown(PointerEventData eventData) { }
    public void OnPointerUp(PointerEventData eventData) { }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Reset to default zoom (1x) and centred position.</summary>
    public void ResetView()
    {
        currentZoom = Mathf.Clamp(1f, minZoom, maxZoom);
        contentPanel.localScale = Vector3.one * currentZoom * baseScale;
        contentPanel.anchoredPosition = contentStartPosition;
        isPanning = false;
    }
}
