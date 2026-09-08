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
    [Tooltip("Zoom level the tree opens at (1 = fit-to-window). Driven per-tree by TraitTree.defaultZoom.")]
    [SerializeField] private float defaultZoom = 1f;
    [Tooltip("When false, mouse-wheel zoom is disabled and the tree stays at Default Zoom (panning still works).")]
    [SerializeField] private bool allowZoom = true;

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 1f;
    [SerializeField] private bool clampPanning = true;
    [SerializeField] private float panBoundsPadding = 300f;
    [Tooltip("Vertical pan distance per mouse-wheel notch when zoom is disabled.")]
    [SerializeField] private float scrollPanSpeed = 40f;

    [Header("Bottom Anchor")]
    [Tooltip("When true, the tree opens pinned to the bottom of the viewport and cannot be panned below its bottom edge; players scroll upward to reveal higher nodes. Driven per-tree by TraitTree.anchorTreeToBottom.")]
    [SerializeField] private bool anchorToBottom = false;

    private float currentZoom = 1f;
    private float baseScale = 1f;
    private bool isPanning = false;
    private Vector2 lastMousePosition;
    private Vector2 contentStartPosition;
    private Vector2 contentBounds; // explicit unscaled content size; <=0 falls back to contentPanel.rect
    private float bottomAnchorHalfHeight; // unscaled distance from content centre to the anchor line (canvas bottom); <=0 falls back to content-bounds half height

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
        currentZoom = Mathf.Clamp(defaultZoom, minZoom, maxZoom);
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
        currentZoom = Mathf.Clamp(defaultZoom, minZoom, maxZoom);
        panel.localScale = Vector3.one * currentZoom * baseScale;
        if (anchorToBottom)
            panel.anchoredPosition = ClampPosition(BottomAlignedPosition());
    }

    /// <summary>
    /// Pin the tree to the bottom of the viewport on open (scroll up to reveal higher nodes,
    /// no panning past the bottom edge), or restore centered/free panning. Driven per-tree.
    /// </summary>
    public void SetAnchorToBottom(bool value)
    {
        anchorToBottom = value;
        if (contentPanel == null || viewport == null) return;
        contentPanel.anchoredPosition = anchorToBottom
            ? ClampPosition(BottomAlignedPosition())
            : ClampPosition(contentPanel.anchoredPosition);
    }

    /// <summary>Anchored position whose Y aligns the content's bottom edge with the viewport bottom.</summary>
    private Vector2 BottomAlignedPosition()
    {
        Vector2 pos = contentPanel.anchoredPosition;
        if (viewport == null) return pos;
        float scale = currentZoom * baseScale;
        Rect contentRect = contentPanel.rect;
        float baseH = contentBounds.y > 0f ? contentBounds.y : contentRect.height;
        float anchorOffset = (bottomAnchorHalfHeight > 0f ? bottomAnchorHalfHeight : baseH * 0.5f) * scale;
        float viewportHeight = viewport.rect.height;
        float pivotOffsetY = contentBounds.y > 0f ? 0f : contentRect.center.y * scale;
        pos.y = contentStartPosition.y + anchorOffset - (viewportHeight * 0.5f) - pivotOffsetY;
        return pos;
    }

    /// <summary>
    /// Provide the tree's real content size (node extents) so panning is clamped to the whole
    /// tree, not the stretch-filled wrapper. Enables scrolling from the start when the tree is
    /// larger than the viewport. Re-applies the current clamp/bottom anchor immediately.
    /// </summary>
    public void SetContentBounds(Vector2 size)
    {
        contentBounds = size;
        if (contentPanel == null || viewport == null) return;
        contentPanel.anchoredPosition = anchorToBottom
            ? ClampPosition(BottomAlignedPosition())
            : ClampPosition(contentPanel.anchoredPosition);
    }

    /// <summary>
    /// Set the unscaled authored canvas height so the bottom anchor pins the tree's canvas-bottom
    /// line (the editor's orange origin indicator) to the viewport bottom — matching the editor's
    /// in-game window preview — rather than the outer node-extent bounds. Re-applies immediately.
    /// </summary>
    public void SetBottomAnchorReference(float unscaledCanvasHeight)
    {
        bottomAnchorHalfHeight = Mathf.Max(0f, unscaledCanvasHeight) * 0.5f;
        if (contentPanel == null || viewport == null || !anchorToBottom) return;
        contentPanel.anchoredPosition = ClampPosition(BottomAlignedPosition());
    }

    /// <summary>
    /// Set the opening zoom and whether wheel-zoom is allowed. Applies the default zoom
    /// immediately (re-fitting position, honoring the bottom anchor). Driven per-tree.
    /// </summary>
    public void SetZoomSettings(float newDefaultZoom, bool zoomEnabled)
    {
        allowZoom = zoomEnabled;
        if (newDefaultZoom > 0f) defaultZoom = newDefaultZoom;
        if (contentPanel == null) return;
        currentZoom = Mathf.Clamp(defaultZoom, minZoom, maxZoom);
        contentPanel.localScale = Vector3.one * currentZoom * baseScale;
        contentPanel.anchoredPosition = anchorToBottom
            ? ClampPosition(BottomAlignedPosition())
            : ClampPosition(contentPanel.anchoredPosition);
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

        // When zoom is disabled the wheel scrolls the tree vertically instead.
        if (!allowZoom)
        {
            Vector2 scrolled = contentPanel.anchoredPosition + new Vector2(0f, -scrollDelta * scrollPanSpeed);
            contentPanel.anchoredPosition = ClampPosition(scrolled);
            return;
        }

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

    // Prefer explicit content bounds (the tree's node extents) over the stretch-filled wrapper
    // rect, which always matches the viewport and would otherwise disable panning.
    float baseW = contentBounds.x > 0f ? contentBounds.x : contentRect.width;
    float baseH = contentBounds.y > 0f ? contentBounds.y : contentRect.height;

    // Content dimensions after zoom.
    float contentWidth = baseW * scale;
    float contentHeight = baseH * scale;

    // Distance from content centre to the bottom anchor line (canvas bottom / orange indicator),
    // after zoom. Falls back to the content half-height when no reference has been supplied.
    float anchorOffsetScaled = (bottomAnchorHalfHeight > 0f ? bottomAnchorHalfHeight : baseH * 0.5f) * scale;

    float viewportWidth = parentRect.width;
    float viewportHeight = parentRect.height;

    // Account for the content pivot. Explicit bounds are centered on the wrapper, so no offset.
    float pivotOffsetX = contentBounds.x > 0f ? 0f : contentRect.center.x * scale;
    float pivotOffsetY = contentBounds.y > 0f ? 0f : contentRect.center.y * scale;

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
        // Content is smaller than viewport — pin to bottom when anchored, else keep it centred.
        pivotPosition.y = anchorToBottom
            ? contentStartPosition.y + anchorOffsetScaled - (viewportHeight * 0.5f) - pivotOffsetY
            : contentStartPosition.y - pivotOffsetY;
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

        // When anchored, the floor is the canvas-bottom line (orange indicator) rather than the
        // outer node-extent bottom, so the tree opens with its canvas bottom at the viewport bottom
        // and cannot be scrolled below it.
        float upperBound = anchorToBottom
            ? contentStartPosition.y + anchorOffsetScaled - halfViewportHeight - pivotOffsetY
            : maxY + panBoundsPadding;

        pivotPosition.y = Mathf.Clamp(
            position.y,
            minY - panBoundsPadding,
            upperBound
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
        currentZoom = Mathf.Clamp(defaultZoom, minZoom, maxZoom);
        contentPanel.localScale = Vector3.one * currentZoom * baseScale;
        contentPanel.anchoredPosition = anchorToBottom
            ? ClampPosition(BottomAlignedPosition())
            : contentStartPosition;
        isPanning = false;
    }
}
