using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Persistent, DontDestroyOnLoad tooltip host canvas.
/// All tooltip prefabs (WeaponTooltip, ArmorTooltip, ItemTooltip, GearTooltip) live as
/// children of this object's Canvas.
/// 
/// Each frame it fires a single EventSystem raycast to find a UIElement component under
/// the pointer. When the hovered element changes it invokes OnTooltipHide on the old
/// element and OnTooltipShow on the new one.
///
/// Set IsDragging = true from InventoryItemUI / GearItemUI OnBeginDrag to suppress
/// tooltips while dragging.
/// 
/// Setup:
///   - Attach to a Canvas with RenderMode = Screen Space Overlay, Sort Order 9999.
///   - Do NOT add a GraphicRaycaster to this GameObject so tooltip panels never
///     eat input.
///   - Place all tooltip child objects under this Canvas.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class GlobalTooltipCanvas : MonoBehaviour
{
    public static GlobalTooltipCanvas Instance { get; private set; }

    /// <summary>
    /// Signal from drag handlers. While true no new tooltip will be shown.
    /// </summary>
    public static bool IsDragging { get; set; }

    private UIElement _currentElement;
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
    }

    private void Start()
    {
        // Ensure any nested Canvases on tooltip children also have a high sort order.
        // Prefabs with their own Canvas component would otherwise render at sort order 0,
        // behind canvases like the inventory (sort order 45).
        Canvas[] childCanvases = GetComponentsInChildren<Canvas>(true);
        foreach (Canvas child in childCanvases)
        {
            if (child.gameObject == gameObject) continue;
            child.overrideSorting = true;
            child.sortingOrder = 9999;
        }
    }

    /// <summary>
    /// Force the tooltip system to re-evaluate the element under the pointer on the next
    /// frame. Call this after any operation that destroys or repositions UIElement objects
    /// (e.g. inventory refresh after equipping an item) so the cached element reference
    /// does not remain stale.
    /// </summary>
    public static void Invalidate()
    {
        if (Instance == null) return;
        Instance._currentElement?.OnTooltipHide?.Invoke();
        Instance._currentElement = null;
    }

    private void Update()
    {
        if (EventSystem.current == null)
            return;

        UIElement hit = IsDragging ? null : GetUIElementUnderPointer();

        if (hit == _currentElement)
            return;

        _currentElement?.OnTooltipHide?.Invoke();
        _currentElement = hit;
        _currentElement?.OnTooltipShow?.Invoke();
    }

    private UIElement GetUIElementUnderPointer()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero
        };

        _raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, _raycastResults);

        foreach (RaycastResult result in _raycastResults)
        {
            UIElement element = result.gameObject.GetComponent<UIElement>();
            if (element == null)
                element = result.gameObject.GetComponentInParent<UIElement>();

            if (element != null && element.showTooltip)
            {
                return element;
            }
        }

        return null;
    }
}
