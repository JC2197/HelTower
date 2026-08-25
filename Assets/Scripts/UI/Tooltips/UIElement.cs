using System;
using UnityEngine;

/// <summary>
/// Token component placed on any hoverable UI element (InventoryItemUI, GearItemUI, etc.).
/// GlobalTooltipCanvas detects this via EventSystem raycast each frame and fires the
/// appropriate callbacks, keeping tooltip logic out of each individual item UI class.
///
/// Usage:
///   1. The owning MonoBehaviour calls GetOrAdd<UIElement>() in Awake.
///   2. In Initialize() it assigns OnTooltipShow / OnTooltipHide.
///   3. Set showTooltip = false on any element where tooltips should be suppressed.
/// </summary>
public class UIElement : MonoBehaviour
{
    [Tooltip("Uncheck to suppress tooltip display for this element.")]
    public bool showTooltip = true;

    /// <summary>Invoked by GlobalTooltipCanvas when pointer enters this element.</summary>
    public Action OnTooltipShow;

    /// <summary>Invoked by GlobalTooltipCanvas when pointer leaves this element.</summary>
    public Action OnTooltipHide;
}