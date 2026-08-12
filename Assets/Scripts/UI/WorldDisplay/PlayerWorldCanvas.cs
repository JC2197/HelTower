using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unified world-space canvas that lives on the player character.
/// Replaces the individual WorldHealthBar canvas and ReloadBar canvas
/// with a single Canvas that both share.
///
/// Hierarchy example:
///   PlayerCharacter
///   └── WorldCanvas           ← this component + Canvas + CanvasScaler
///       ├── HealthSection     ← WorldHealthBar content lives here
///       ├── ReloadSection     ← ReloadBar content lives here
///       └── ChargeSection     ← ChargeBar content lives here
///
/// Setup:
///   1. Add a child GameObject "WorldCanvas" to PlayerCharacter.
///   2. Add Canvas + CanvasScaler + PlayerWorldCanvas to it.
///   3. Assign the two Section RectTransforms in the inspector.
///   4. Remove the old per-bar Canvas objects (or keep and disable them).
/// </summary>
[RequireComponent(typeof(Canvas))]
public class PlayerWorldCanvas : MonoBehaviour
{
    [Header("Sections — child RectTransforms for each indicator")]
    [Tooltip("Parent of the health bar fill image and force-field image.")]
    [SerializeField] private RectTransform healthSection;

    [Tooltip("Parent of the reload bar fill image.")]
    [SerializeField] private RectTransform reloadSection;

    [Tooltip("Parent of the charge bar fill image.")]
    [SerializeField] private RectTransform chargeSection;

    // ── Public accessors ──────────────────────────────────────────────────

    public Canvas        SharedCanvas    { get; private set; }
    public RectTransform HealthSection   => healthSection;
    public RectTransform ReloadSection   => reloadSection;
    public RectTransform ChargeSection   => chargeSection;

    // ── Internal ──────────────────────────────────────────────────────────────

    private Camera mainCamera;

    private void Awake()
    {
        SharedCanvas = GetComponent<Canvas>();
        // Do NOT force renderMode, sortingOrder, or localScale here.
        // Configure the Canvas component and RectTransform directly on the prefab.
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();
    }

    // Billboard is intentionally removed — the canvas is a child of the player
    // and moves with them automatically. Forced rotation fights 2D layout.

    // ── Utility ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by child sections (WorldHealthBar, ReloadBar, StaminaHUDController) whenever
    /// they show or hide their section. Disables the whole canvas when every section is hidden
    /// so it renders nothing, and re-enables it as soon as any section becomes visible.
    /// </summary>
    public void NotifySectionChanged()
    {
        if (SharedCanvas == null) SharedCanvas = GetComponent<Canvas>();
        bool healthActive  = healthSection  != null && healthSection.gameObject.activeSelf;
        bool reloadActive  = reloadSection  != null && reloadSection.gameObject.activeSelf;
        bool chargeActive  = chargeSection  != null && chargeSection.gameObject.activeSelf;
        bool anyActive = healthActive || reloadActive || chargeActive;
        
        Debug.Log($"[PlayerWorldCanvas] NotifySectionChanged: health={healthActive}, reload={reloadActive}, charge={chargeActive} => canvas.enabled={anyActive} (caller: {new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod()?.DeclaringType?.Name})");
        
        SharedCanvas.enabled = anyActive;
    }

    /// <summary>
    /// Returns the reload section RectTransform, creating a default one if none is assigned.
    /// </summary>
    public RectTransform GetOrCreateReloadSection()
    {
        if (reloadSection != null) return reloadSection;

        var go     = new GameObject("ReloadSection", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        reloadSection = go.GetComponent<RectTransform>();
        reloadSection.anchorMin        = new Vector2(0.5f, 0f);
        reloadSection.anchorMax        = new Vector2(0.5f, 0f);
        reloadSection.pivot            = new Vector2(0.5f, 1f);
        reloadSection.anchoredPosition = new Vector2(0f, -28f);
        reloadSection.sizeDelta        = new Vector2(100f, 10f);
        return reloadSection;
    }
}
