using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;



// ═══════════════════════════════════════════════════════════════════════════════
//  TraitTreeUI  — merged canvas controller, tab manager, and renderer
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Single MonoBehaviour that owns the entire trait tree UI.
///
/// Responsibilities
///   • Open / close the overlay (C key, or call Toggle() / Close()).
///   • Tab bar: switch between different trait tree panels.
///   • Pixel-art rendering: draws connections + node icons at 400×225 into a Sprite on a UI Image.
///   • Node widgets: spawns transparent 20×20 TraitTreeNodeUI objects for hover/click.
///   • Materials + research-points display.
///   • Routes tooltip to TraitTreeTooltip   singleton.
///
/// Minimum prefab hierarchy
///   Root  ← this component + Canvas (Screen Space Overlay, CanvasScaler ref 400×225)
///   ├── Background   Image — background sprite (stretch-fill)
///   ├── TreeImage    Image — receives generated sprite (stretch-fill, no source sprite)
///   ├── NodeLayer    empty RectTransform (stretch-fill) — node widgets spawned here
///   ├── TabRow       RectTransform — tab buttons are spawned here at runtime
///   └── [optional HUD reference]
///
/// Tabs are generated at runtime from the <c>trees[]</c> array.
/// Each tab button is instantiated from <c>tabButtonPrefab</c> and labelled with
/// the corresponding tree's <c>treeName</c>.
/// </summary>
public class TraitTreeUI : MonoBehaviour
{
    private const int MaxConnectionBufferDim = 2048;
    private const int MaxConnectionBufferPixels = 1024 * 1024;
    private const int MaxConnectionRuns = 50000;

    // ── Open / close ──────────────────────────────────────────────────────────

    [Header("Open / Close")]
    [SerializeField] private GameObject hudCanvas;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject nodePrefab;

    // ── Tab bar ───────────────────────────────────────────────────────────────

    [Header("Tabs")]
    [Tooltip("All crafting trees available as tabs. Order determines tab order.")]
    [SerializeField] private TraitTree[] trees;

    /// <summary>All trees registered in the tab bar. Used by external systems to scan across all tabs.</summary>
    public IReadOnlyList<TraitTree> Trees => trees;

    /// <summary>Overrides the tab list at runtime — e.g. from the currently equipped class's trait trees.</summary>
    public void SetAvailableTrees(IReadOnlyList<TraitTree> newTrees)
    {
        var list = new List<TraitTree>();
        if (newTrees != null)
            foreach (var t in newTrees)
                if (t != null) list.Add(t);
        trees = list.ToArray();

    }

    public void SetCurrentTree(TraitTree tree)
    {
        _treeData = tree;
    }

    [Tooltip("Prefab instantiated for each tab. Must have a Button component and a TMP_Text child for the label.")]
    [SerializeField] private GameObject tabButtonPrefab;
    [Tooltip("RectTransform under which tab buttons are spawned.")]
    [SerializeField] private RectTransform tabContainer;
    [Tooltip("Index into trees[] to display on open.")]
    [SerializeField] private int defaultTabIndex = 0;

    // ── Renderer ──────────────────────────────────────────────────────────────

    [Header("Renderer")]
    [Tooltip("Image that will display the generated pixel-art sprite (stretch-fill, no source sprite).")]
    [SerializeField] private Image treeImage;
    [Tooltip("Optional: also write to a RawImage if present.")]
    [SerializeField] private RawImage treeRawImage;
    [Tooltip("Optional background Image — receives treeData.editorBackgroundSprite.")]
    [SerializeField] private Image backgroundImage;
    [Tooltip("Pixel size of each rendered node icon. 0 = use treeData.nodeIconSize.")]
    [SerializeField] private int nodeIconSizeOverride = 20;

    // ── Node widgets ──────────────────────────────────────────────────────────

    [Header("Node Widgets")]
    [Tooltip("Parent RectTransform for interactive node widgets (stretch-fill, on top of TreeImage).")]
    [SerializeField] private RectTransform nodeContainer;
    [Tooltip("Should match nodeIconSizeOverride.")]
    [SerializeField] private int nodePixelSize = 20;

    // ── Currency ────────────────────────────────────────────────────────

    [Header("Currency")]
    [Tooltip("Displays the save file's gold balance — the only currency trait nodes cost.")]
    [SerializeField] private TMP_Text goldText;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private TraitTree _treeData;
    private CharacterTraitManager _traitTreeManager;
    private int _activeTabIndex = 0;
    private List<Button> _tabButtons = new();
    private Dictionary<string, TraitNodeUI> _nodeUILookup = new();

    private RectTransform _zoomContent;
    private RectTransform _connectionContainer;
    private readonly List<TraitConnectionUI> _connectionUIs = new();

    private static readonly Vector2[] s_BubbleDirOffsets =
    {
        new Vector2( 0, -1), // 0 = North
        new Vector2( 0,  1), // 1 = South
        new Vector2( 1,  0), // 2 = East
        new Vector2(-1,  0), // 3 = West
    };

    /// <summary>Fired when the player clicks an affordable, available node. TraitSystemManager charges the gold.</summary>
    public Action<TraitNode> OnTraitUnlockRequested;
    public Action<TraitNode> OnTraitLevelRequested;

    /// <summary>Fired when the player clicks an available node. Passes the source tree, nodeID, and weaponConfig (may be null for armor nodes).</summary>
    public Action<TraitTree, string, WeaponConfig> OnWeaponCraftRequested;

    // ═════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ═════════════════════════════════════════════════════════════════════════

    private void OnEnable()
    {
        //BuildTabButtons();
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        // Tree content is loaded via Initialize(); tabs just switch data when clicked.
    }

    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    private void OnDestroy()
    {
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Open / close
    // ═════════════════════════════════════════════════════════════════════════

    public void Toggle()
    {
        bool willOpen = !gameObject.activeSelf;
        gameObject.SetActive(willOpen);
        if (hudCanvas != null) hudCanvas.SetActive(!willOpen);
    }

    public void Close()
    {
        // When managed by TraitTreeSceneManager, delegate full teardown to it
        // so that all pushed panels (crafting tree + auto-opened inventory) are
        // properly popped and UI mode is fully exited.
        if (TraitTreeSceneManager.Instance != null)
        {
            this.HideTooltip();
            TraitTreeSceneManager.Instance.CloseTraitTree();
            return;
        }

        // Standalone fallback (not managed by TraitTreeSceneManager).
        this.HideTooltip();
        gameObject.SetActive(false);
        if (hudCanvas != null) hudCanvas.SetActive(true);
        PlayerController.InputEnabled = true;
        if (CursorManager.Instance != null) CursorManager.Instance.PopPanel();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Initialise (called by TraitTreeSceneManager)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wire up the crafting manager and render the default tab's tree.
    /// Called by TraitTreeSceneManager.OpenTraitTree().
    /// The 'data' parameter is accepted for backwards compatibility but the tab-driven
    /// categoryTrees[] bindings are used preferentially; 'data' is the fallback.
    /// </summary>
    public void Initialize(TraitTree data, CharacterTraitManager manager)
    {
        _traitTreeManager = manager;


        if (_traitTreeManager != null)
        {
            _traitTreeManager.OnTraitsChanged -= UpdateAllNodeStates;
            _traitTreeManager.OnTraitsChanged += UpdateAllNodeStates;
        }

        // Load the default tab's tree; fall back to the supplied data if no entry exists.a
        // int startIndex = (trees != null && trees.Length > 0)
        //     ? Mathf.Clamp(defaultTabIndex, 0, trees.Length - 1)
        //     : -1;
        TraitTree startData = data;
        _treeData = startData;
        _activeTabIndex = 0;
        //RefreshTabVisuals();

        EnsureZoomContent();

        if (_treeData != null && _treeData.nodes != null && _treeData.nodes.Count > 0)
        {
            //ApplyBackground();
            CreateConnectionWidgets();
            CreateNodeWidgets();
            UpdateAllNodeStates();
        }
    }

    /// <summary>
    /// Group the dynamic connection layer and the interactive node layer under a single
    /// content wrapper, then point CraftingTreePanZoom at that wrapper so the whole tree
    /// pans/zooms as one unit while the chrome (tabs, materials, close button, background)
    /// stays fixed. The connection layer is created here with the SAME center anchor as the
    /// node layer so a line point at (x, -y) lands exactly on the node drawn at (x, -y).
    /// The legacy baked treeImage is disabled — the tree is rendered entirely from live
    /// UI widgets. Runs once; safe to call on every Initialize.
    /// </summary>
    private void EnsureZoomContent()
    {
        if (_zoomContent != null)
        {
            Debug.Log("[ConnDbg] EnsureZoomContent: already initialized, skipping.");
            return;
        }
        if (nodeContainer == null)
        {
            Debug.LogError("[ConnDbg] EnsureZoomContent: nodeContainer is NULL — connection layer cannot be created!");
            return;
        }

        Transform parent = nodeContainer.parent;
        if (parent == null)
        {
            Debug.LogError("[ConnDbg] EnsureZoomContent: nodeContainer.parent is NULL — connection layer cannot be created!");
            return;
        }

        Debug.Log($"[ConnDbg] EnsureZoomContent: building content wrapper. nodeContainer='{nodeContainer.name}', parent='{parent.name}', nodeRect={nodeContainer.rect}, nodeAnchors=({nodeContainer.anchorMin}->{nodeContainer.anchorMax}), nodePivot={nodeContainer.pivot}");

        // Stretch-fill wrapper occupying the same rect as the canvas.
        var go = new GameObject("TreeContent", typeof(RectTransform));
        go.layer = nodeContainer.gameObject.layer;
        var wrapper = go.GetComponent<RectTransform>();
        wrapper.SetParent(parent, false);
        wrapper.anchorMin = Vector2.zero;
        wrapper.anchorMax = Vector2.one;
        wrapper.pivot = new Vector2(0.5f, 0.5f);
        wrapper.offsetMin = Vector2.zero;
        wrapper.offsetMax = Vector2.zero;
        wrapper.localScale = Vector3.one;

        // Render the tree where treeImage used to sit (below the chrome).
        if (treeImage != null)
        {
            wrapper.SetSiblingIndex(treeImage.rectTransform.GetSiblingIndex());
            // No baked picture — the tree is drawn dynamically.
            treeImage.sprite = null;
            treeImage.enabled = false;
        }
        else
        {
            wrapper.SetSiblingIndex(nodeContainer.GetSiblingIndex());
        }

        // Ensure live tree content always stays above the decorative background.
        if (backgroundImage != null && backgroundImage.transform.parent == parent)
        {
            int bgIndex = backgroundImage.rectTransform.GetSiblingIndex();
            if (wrapper.GetSiblingIndex() <= bgIndex)
                wrapper.SetSiblingIndex(bgIndex + 1);
        }

        // Connection layer beneath the nodes, sharing the node layer's coordinate frame.
        var connGo = new GameObject("ConnectionLayer", typeof(RectTransform));
        connGo.layer = nodeContainer.gameObject.layer;
        _connectionContainer = connGo.GetComponent<RectTransform>();
        _connectionContainer.SetParent(wrapper, false);
        _connectionContainer.anchorMin = nodeContainer.anchorMin;
        _connectionContainer.anchorMax = nodeContainer.anchorMax;
        _connectionContainer.pivot = nodeContainer.pivot;
        _connectionContainer.anchoredPosition = nodeContainer.anchoredPosition;
        _connectionContainer.sizeDelta = nodeContainer.sizeDelta;
        _connectionContainer.localScale = Vector3.one;
        _connectionContainer.SetAsFirstSibling();

        Debug.Log($"[ConnDbg] EnsureZoomContent: created ConnectionLayer. layer={_connectionContainer.gameObject.layer}, rect={_connectionContainer.rect}, anchors=({_connectionContainer.anchorMin}->{_connectionContainer.anchorMax}), pivot={_connectionContainer.pivot}, sizeDelta={_connectionContainer.sizeDelta}, siblingIndex={_connectionContainer.GetSiblingIndex()}, activeInHierarchy={_connectionContainer.gameObject.activeInHierarchy}");

        // Interactive nodes on top.
        nodeContainer.SetParent(wrapper, false);
        nodeContainer.SetAsLastSibling();

        _zoomContent = wrapper;

        var panZoom = GetComponent<CraftingTreePanZoom>();
        if (panZoom != null) panZoom.SetContentPanel(wrapper);
        else Debug.LogWarning("[ConnDbg] EnsureZoomContent: no CraftingTreePanZoom component found on this GameObject.");

        Debug.Log($"[ConnDbg] EnsureZoomContent: DONE. wrapper='{wrapper.name}', wrapperRect={wrapper.rect}, wrapperSibling={wrapper.GetSiblingIndex()}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Tab bar
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Destroy any existing tab buttons and instantiate one from tabButtonPrefab
    /// for each entry in trees[], labelling it with treeName.
    /// </summary>
    private void BuildTabButtons()
    {
        // Clear old buttons
        foreach (var btn in _tabButtons)
            if (btn != null) Destroy(btn.gameObject);
        _tabButtons.Clear();

        if (tabButtonPrefab == null || tabContainer == null || trees == null) return;

        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] == null) continue;

            var go = Instantiate(tabButtonPrefab, tabContainer);
            var btn = go.GetComponent<Button>();
            if (btn == null) continue;

            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = trees[i].treeName;

            int captured = i;
            btn.onClick.AddListener(() => SelectTab(captured));
            _tabButtons.Add(btn);
        }

        RefreshTabVisuals();
    }

    private void SelectTab(int index)
    {
        if (index == _activeTabIndex) return;
        _activeTabIndex = index;
        RefreshTabVisuals();
        if (trees != null && index >= 0 && index < trees.Length && trees[index] != null)
            SwitchTree(trees[index]);
    }

    private void RefreshTabVisuals()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            if (_tabButtons[i] == null) continue;
            bool active = (i == _activeTabIndex);
            // Tint the button graphic to give active/inactive feedback.
            var img = _tabButtons[i].targetGraphic as Image;
            if (img != null) img.color = active ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
        }
    }

    /// <summary>
    /// Swap to a different TraitTree without re-wiring the crafting manager.
    /// Called when the player selects a different tab.
    /// </summary>
    private void SwitchTree(TraitTree newData)
    {
        _treeData = newData;
        if (_treeData == null || _treeData.nodes == null || _treeData.nodes.Count == 0)
            return;
        // ApplyBackground();
        CreateConnectionWidgets();
        CreateNodeWidgets();
        UpdateAllNodeStates();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Renderer (dynamic — no baked texture)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Push the tree's editor background art to the background Image.</summary>
    // private void ApplyBackground()
    // {
    //     if (backgroundImage != null && _treeData != null)
    //         backgroundImage.sprite = _treeData.editorBackgroundSprite;
    // }

    /// <summary>
    /// (Re)build the live connection lines from _treeData into the connection layer.
    /// Each connection's path is rasterized at runtime by PixelConnectionDrawer (the same
    /// pixel logic the tree always used) into a transient buffer, then handed to a
    /// TraitConnectionUI which emits one crisp UI quad per lit pixel — keeping the
    /// pixel-art look without baking anything into a texture asset. Pixels are computed in
    /// the same center-origin UI space as the node widgets, so lines link node centers.
    /// </summary>
    private void CreateConnectionWidgets()
    {

        if (_connectionContainer == null)
        {
            Debug.LogWarning("[ConnDbg] _connectionContainer is NULL at start — calling EnsureZoomContent().");
            EnsureZoomContent();
        }
        if (_connectionContainer == null || _treeData == null)
        {
            Debug.LogError($"[ConnDbg] ABORT: _connectionContainer={(_connectionContainer != null)}, _treeData={(_treeData != null)}. Cannot build connections.");
            return;
        }

        int destroyed = 0;
        foreach (Transform child in _connectionContainer)
        {
            Destroy(child.gameObject);
            destroyed++;
        }
        _connectionUIs.Clear();
        Debug.Log($"[ConnDbg] Cleared {destroyed} existing connection child object(s).");

        if (_treeData.connections == null || _treeData.nodes == null)
        {
            Debug.LogError($"[ConnDbg] ABORT: connections={(_treeData.connections != null ? _treeData.connections.Count.ToString() : "NULL")}, nodes={(_treeData.nodes != null ? _treeData.nodes.Count.ToString() : "NULL")}.");
            return;
        }

        int layer = _connectionContainer.gameObject.layer;
        float iconSize = nodeIconSizeOverride > 0 ? nodeIconSizeOverride : Mathf.Max(_treeData.nodeIconSize, 4);
        float iconHalf = iconSize / 2f + 1f;

        Debug.Log($"[ConnDbg] Building {_treeData.connections.Count} connection(s). nodeCount={_treeData.nodes.Count}, layer={layer}, iconSize={iconSize}, iconHalf={iconHalf}, containerRect={_connectionContainer.rect}");

        int builtCount = 0;
        int skippedCount = 0;
        int fallbackCount = 0;
        int connIndex = -1;

        foreach (var conn in _treeData.connections)
        {
            connIndex++;
            try
            {
                if (conn.fromNodeIDs == null || conn.fromNodeIDs.Length == 0 ||
                    conn.toNodeIDs == null || conn.toNodeIDs.Length == 0)
                {
                    skippedCount++;
                    Debug.LogWarning($"[ConnDbg] Conn[{connIndex}] '{conn.connectionID}' SKIP: missing from/to IDs (from={(conn.fromNodeIDs != null ? conn.fromNodeIDs.Length : 0)}, to={(conn.toNodeIDs != null ? conn.toNodeIDs.Length : 0)}).");
                    continue;
                }

                TraitNode fromNode = null, toNode = null;
                foreach (var n in _treeData.nodes)
                {
                    if (n.nodeID == conn.fromNodeIDs[0]) fromNode = n;
                    if (n.nodeID == conn.toNodeIDs[0]) toNode = n;
                }
                if (fromNode == null || toNode == null)
                {
                    skippedCount++;
                    Debug.LogWarning($"[ConnDbg] Conn[{connIndex}] '{conn.connectionID}' SKIP: node lookup failed (from='{conn.fromNodeIDs[0]}'->{(fromNode != null)}, to='{conn.toNodeIDs[0]}'->{(toNode != null)}).");
                    continue;
                }

                Debug.Log($"[ConnDbg] Conn[{connIndex}] '{conn.connectionID}' from='{fromNode.nodeID}'@{fromNode.position} to='{toNode.nodeID}'@{toNode.position}, lineWidth={conn.lineWidth}, curve={conn.curveAmount}, fromDir={conn.fromBubbleDir}, toDir={conn.toBubbleDir}, useDrawnPath={conn.useDrawnPath}, paintedPixels={(conn.paintedPixels != null ? conn.paintedPixels.Count : 0)}, color={conn.lineColor}");

                // Endpoints in node-layer space (Y-up anchoredPosition convention).
                Vector2 a = new Vector2(fromNode.position.x, -fromNode.position.y);
                Vector2 b = new Vector2(toNode.position.x, -toNode.position.y);

                if (conn.fromBubbleDir >= 0 && conn.fromBubbleDir < 4)
                {
                    Vector2 sd = s_BubbleDirOffsets[conn.fromBubbleDir];
                    a += new Vector2(sd.x * iconHalf, -sd.y * iconHalf);
                }
                if (conn.toBubbleDir >= 0 && conn.toBubbleDir < 4)
                {
                    Vector2 sd = s_BubbleDirOffsets[conn.toBubbleDir];
                    b += new Vector2(sd.x * iconHalf, -sd.y * iconHalf);
                }

                // Rasterize the path into a transient pixel buffer using the shared pixel drawer,
                // working in a local Y-up frame so bubble directions match its texture convention.
                int lineW = Mathf.Max(conn.lineWidth, 1);
                float pad = lineW + Mathf.Max(conn.curveAmount, 0f) + 2f;

                // Guard against non-finite endpoints (a bad node position must never reach the GPU).
                if (float.IsNaN(a.x) || float.IsNaN(a.y) || float.IsNaN(b.x) || float.IsNaN(b.y) ||
                    float.IsInfinity(a.x) || float.IsInfinity(a.y) || float.IsInfinity(b.x) || float.IsInfinity(b.y))
                    continue;

                float minX = Mathf.Min(a.x, b.x) - pad;
                float minY = Mathf.Min(a.y, b.y) - pad;
                int bufW = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + pad - minX) + 1;
                int bufH = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + pad - minY) + 1;
                Debug.Log($"[ConnDbg] Conn[{connIndex}] '{conn.connectionID}' endpoints a={a} b={b}, pad={pad}, minX={minX}, minY={minY}, bufW={bufW}, bufH={bufH}");
                if (bufW <= 0 || bufH <= 0)
                {
                    skippedCount++;
                    Debug.LogWarning($"[ConnDbg] Conn[{connIndex}] '{conn.connectionID}' SKIP: non-positive buffer dims (bufW={bufW}, bufH={bufH}).");
                    continue;
                }

                // Scale oversized bounds down to a safe working buffer instead of skipping the
                // connection. This keeps very large trees stable and still renders connectors.
                float scale = 1f;
                if (bufW > MaxConnectionBufferDim || bufH > MaxConnectionBufferDim)
                {
                    float sx = MaxConnectionBufferDim / (float)bufW;
                    float sy = MaxConnectionBufferDim / (float)bufH;
                    scale = Mathf.Clamp(Mathf.Min(sx, sy), 0.01f, 1f);
                }

                int workW = Mathf.Max(1, Mathf.CeilToInt(bufW * scale));
                int workH = Mathf.Max(1, Mathf.CeilToInt(bufH * scale));

                long workPixels = (long)workW * workH;
                if (workPixels > MaxConnectionBufferPixels)
                {
                    float capScale = Mathf.Sqrt(MaxConnectionBufferPixels / (float)workPixels);
                    capScale = Mathf.Clamp(capScale, 0.01f, 1f);
                    scale *= capScale;
                    workW = Mathf.Max(1, Mathf.CeilToInt(bufW * scale));
                    workH = Mathf.Max(1, Mathf.CeilToInt(bufH * scale));
                }

                var buffer = new Color32[workW * workH];
                var texA = new Vector2Int(
                    Mathf.RoundToInt((a.x - minX) * scale),
                    Mathf.RoundToInt((a.y - minY) * scale));
                var texB = new Vector2Int(
                    Mathf.RoundToInt((b.x - minX) * scale),
                    Mathf.RoundToInt((b.y - minY) * scale));

                texA.x = Mathf.Clamp(texA.x, 0, workW - 1);
                texA.y = Mathf.Clamp(texA.y, 0, workH - 1);
                texB.x = Mathf.Clamp(texB.x, 0, workW - 1);
                texB.y = Mathf.Clamp(texB.y, 0, workH - 1);

                if (conn.useDrawnPath && conn.paintedPixels != null && conn.paintedPixels.Count > 0)
                {
                    int sourceW = Mathf.Max(_treeData.canvasWidth, 1);
                    int sourceH = Mathf.Max(_treeData.canvasHeight, 1);

                    foreach (var p in conn.paintedPixels)
                    {
                        if (p == null) continue;
                        if (p.x < 0 || p.x >= sourceW || p.y < 0 || p.y >= sourceH) continue;

                        int sx = Mathf.RoundToInt((p.x / (float)Mathf.Max(sourceW - 1, 1)) * (workW - 1));
                        int sy = Mathf.RoundToInt((p.y / (float)Mathf.Max(sourceH - 1, 1)) * (workH - 1));
                        if (sx < 0 || sx >= workW || sy < 0 || sy >= workH) continue;
                        buffer[sy * workW + sx] = (Color32)p.color;
                    }
                }
                else
                {
                    PixelConnectionDrawer.DrawConnection(
                        buffer, workW, workH, texA, texB,
                        conn.curveAmount * scale, Mathf.Max(1, Mathf.RoundToInt(lineW * scale)), (Color32)conn.lineColor,
                        conn.fromBubbleDir, conn.toBubbleDir);
                }

                // Count lit pixels; if the rasterizer produced nothing, draw a straight
                // fallback line into the buffer so a connection is always visible.
                int litCount = 0;
                for (int i = 0; i < buffer.Length; i++) if (buffer[i].a > 0) litCount++;
                if (litCount == 0)
                {
                    PixelConnectionDrawer.DrawConnection(
                        buffer, workW, workH, texA, texB,
                        0f, Mathf.Max(1, Mathf.RoundToInt(lineW * scale)), (Color32)conn.lineColor, -1, -1);
                    litCount = 0;
                    for (int i = 0; i < buffer.Length; i++) if (buffer[i].a > 0) litCount++;
                    fallbackCount++;
                    Debug.LogWarning($"[ConnDbg] Conn[{connIndex}] '{conn.connectionID}' had 0 lit pixels — drew straight fallback (now {litCount}).");
                }
                if (litCount == 0)
                {
                    skippedCount++;
                    Debug.LogError($"[ConnDbg] Conn[{connIndex}] '{conn.connectionID}' SKIP: still 0 lit pixels after fallback.");
                    continue;
                }

                // Build a Sprite from the pixel buffer. Texture2D row 0 = bottom, matching the
                // Y-up node-space frame used for the endpoints, so the sprite lines up with nodes.
                var tex = new Texture2D(workW, workH, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                tex.SetPixels32(buffer);
                tex.Apply(false, false);
                var sprite = Sprite.Create(
                    tex, new Rect(0, 0, workW, workH), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

                float uiPixelSize = 1f / scale;
                // Node-space size and center of this connection's pixel region.
                float sizeXn = workW * uiPixelSize;
                float sizeYn = workH * uiPixelSize;
                float centerX = minX + sizeXn * 0.5f;
                float centerY = minY + sizeYn * 0.5f;

                // Standard Image component — always renders and is visible in the inspector.
                var go = new GameObject($"Conn_{conn.connectionID}",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TraitConnectionUI));
                go.layer = layer;
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(_connectionContainer, false);
                // Center anchors + tight size + node-space position — matches how node widgets are placed.
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(sizeXn, sizeYn);
                rt.anchoredPosition = new Vector2(centerX, centerY);
                rt.localScale = Vector3.one;

                var connUI = go.GetComponent<TraitConnectionUI>();
                connUI.Initialize(sprite, conn.lineColor, conn.fromNodeIDs);
                _connectionUIs.Add(connUI);
                builtCount++;

                Debug.Log($"[ConnDbg] Conn[{connIndex}] '{conn.connectionID}' IMAGE CREATED: size=({sizeXn},{sizeYn}), center=({centerX},{centerY}), tex={workW}x{workH}, litPixels={litCount}, activeInHierarchy={go.activeInHierarchy}");
            }
            catch (Exception ex)
            {
                skippedCount++;
                Debug.LogError($"[ConnDbg] Conn[{connIndex}] '{conn.connectionID}' EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            }
        }

        Debug.Log($"[ConnDbg] === CreateConnectionWidgets END === built={builtCount}, skipped={skippedCount}, fallback={fallbackCount}, totalConnectionUIs={_connectionUIs.Count}, containerChildCount={_connectionContainer.childCount}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Node widgets
    // ═════════════════════════════════════════════════════════════════════════

    private void CreateNodeWidgets()
    {
        if (nodeContainer == null) return;

        foreach (Transform child in nodeContainer)
            Destroy(child.gameObject);
        _nodeUILookup.Clear();

        int layer = nodeContainer.gameObject.layer;
        Sprite frame = _treeData.nodeIconFrame;

        foreach (var node in _treeData.nodes)
        {
            var go = Instantiate(nodePrefab, nodeContainer, false);
            go.name = $"Node_{node.nodeID}";
            go.layer = layer;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(nodePixelSize, nodePixelSize);
            rt.anchoredPosition = new Vector2(node.position.x, -node.position.y);
            go.AddComponent<Image>();
            var nodeUI = go.GetComponent<TraitNodeUI>();
            if (nodeUI == null)
                nodeUI = go.AddComponent<TraitNodeUI>();
            nodeUI.Initialize(node, this, frame);
            _nodeUILookup[node.nodeID] = nodeUI;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Node states
    // ═════════════════════════════════════════════════════════════════════════

    public void UpdateAllNodeStates()
    {
        if (_traitTreeManager == null)
            return;

        HashSet<string> unlockedIDs =
            _traitTreeManager.GetUnlockedNodeIDs();

        foreach (var kvp in _nodeUILookup)
        {
            var nodeUI = kvp.Value;
            var nodeData = nodeUI.NodeData;

            if (nodeData == null || nodeData.traitData == null)
                continue;

            int currentLevel =
                _traitTreeManager.GetTraitLevel(nodeData.nodeID);

            int maxLevel =
                nodeData.traitData.maxLevel;

            // Fully upgraded.
            if (currentLevel >= maxLevel)
            {
                nodeUI.UpdateVisualState(
                    TraitNodeState.Unlocked,
                    currentLevel
                );
            }
            // Already purchased, but can be upgraded.
            else if (currentLevel > 0)
            {
                bool canAfford =
                    _traitTreeManager.CanAffordNode(nodeData);

                nodeUI.UpdateVisualState(
                    canAfford
                        ? TraitNodeState.Upgradeable
                        : TraitNodeState.CannotAfford,
                    currentLevel
                );
            }
            // First level — prerequisites still matter.
            else if (IsNodeAvailable(nodeData, unlockedIDs))
            {
                bool canAfford =
                    _traitTreeManager.CanAffordNode(nodeData);

                nodeUI.UpdateVisualState(
                    canAfford
                        ? TraitNodeState.Available
                        : TraitNodeState.CannotAfford,
                    currentLevel
                );
            }
            // Prerequisites aren't met.
            else
            {
                nodeUI.UpdateVisualState(
                    TraitNodeState.Locked,
                    currentLevel
                );
            }
        }

        foreach (var connUI in _connectionUIs)
        {
            if (connUI != null)
                connUI.UpdateState(unlockedIDs);
        }

        UpdateGoldDisplay();
    }

    /// <summary>
    /// Refresh the gold readout from the authoritative TraitSystemManager balance.
    /// </summary>
    public void UpdateGoldDisplay()
    {
        if (goldText == null) return;

        TraitSystemManager manager = TraitSystemManager.Instance;
        goldText.text = manager != null ? $"Gold: {manager.GetAvailableGold()}" : "Gold: ?";
    }

    private bool IsNodeAvailable(TraitNode nodeData, HashSet<string> nodeIDs)
    {
        bool hasPrereqs = false, anyMet = false;

        foreach (var conn in _treeData.connections)
        {
            if (conn.toNodeIDs == null || conn.toNodeIDs.Length == 0) continue;

            bool isTarget = false;
            foreach (string id in conn.toNodeIDs)
                if (id == nodeData.nodeID) { isTarget = true; break; }
            if (!isTarget) continue;

            if (conn.fromNodeIDs != null && conn.fromNodeIDs.Length > 0)
            {
                bool hasValid = false;
                foreach (string fromID in conn.fromNodeIDs)
                {
                    if (string.IsNullOrEmpty(fromID) || !_nodeUILookup.ContainsKey(fromID)) continue;
                    hasValid = true;
                    if (nodeIDs.Contains(fromID)) { anyMet = true; break; }
                }
                if (hasValid) hasPrereqs = true;
                if (anyMet) break;
            }
        }

        return !hasPrereqs || anyMet;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Interaction callbacks  (called by TraitNodeUI)
    // ═════════════════════════════════════════════════════════════════════════

    public void OnNodeClicked(TraitNodeUI nodeUI)
    {
        var node = nodeUI.NodeData;
        if (node == null || node.traitData == null) return;

        if (_traitTreeManager == null) return;

        int currentLevel = _traitTreeManager.GetTraitLevel(node.nodeID);
        int maxLevel = node.traitData.maxLevel;

        if (currentLevel >= maxLevel)
        {
            Debug.Log(
                $"[TraitTreeUI] Node '{node.nodeID}' is already at " +
                $"max level ({maxLevel})."
            );
            return;
        }

        if (!_traitTreeManager.CanAffordNode(node)) return;

        OnTraitUnlockRequested?.Invoke(node);

        UpdateAllNodeStates();
        // Refresh tooltip using the newly updated runtime level/cost.
        if (nodeUI != null)
        {
            ShowTooltip(node.traitData, nodeUI);
        }
    }

    public void ShowTooltip(TraitData traitData, TraitNodeUI nodeUI)
    {
        if (traitData == null || TraitTooltip.Instance == null)
            return;

        string description = traitData.description ?? string.Empty;

        TraitNode node = nodeUI != null
            ? nodeUI.NodeData
            : null;

        if (node != null && _traitTreeManager != null)
        {
            int currentLevel =
                _traitTreeManager.GetTraitLevel(node.nodeID);

            int maxLevel =
                traitData.maxLevel;

            if (currentLevel < maxLevel)
            {
                int goldCost =
                    _traitTreeManager.GetTraitGoldCost(node);

                string colour =
                    _traitTreeManager.CanAffordNode(node)
                        ? "#00ff00"
                        : "#ff4444";

                description +=
                    $"\n\n<b>Level:</b> {currentLevel}/{maxLevel}" +
                    $"\n<b>Cost:</b> <color={colour}>{goldCost} gold</color>";
            }
            else
            {
                description +=
                    $"\n\n<b>Level:</b> {currentLevel}/{maxLevel}" +
                    "\n<b>MAX LEVEL</b>";
            }
        }

        TraitTooltip.Instance.ShowTooltip(
            traitData.displayName,
            description
        );
    }

    public void HideTooltip() => TraitTooltip.Instance?.HideTooltip();


    private void AppendCostAndRequirements(TraitNode nodeData, System.Text.StringBuilder sb)
    {


        // if (nodeData.goldCost > 0)
        // {
        //     PlayerController lp = PlayerController.GetLocalPlayer();
        //     var charData = lp != null ? lp.GetCurrentCharacterData() : CharacterSelectionManager.SelectedCharacter;
        //     int rp = charData != null ? charData.totalGold : 0;
        //     string col = rp >= nodeData.goldCost ? "#00ff00" : "#ff4444";
        //     sb.AppendLine($"\n<b>Gold:</b> <color={col}>{nodeData.goldCost}</color>");
        // }

        // var prereqs = GetNodePrerequisites(nodeData);
        // if (prereqs.Count > 0)
        // {
        //     sb.AppendLine("\n<b>Requirements:</b>");
        //     foreach (var p in prereqs)
        //         sb.AppendLine($"  • {p}");
        // }
    }

    private static string GetNodeDisplayName(TraitNode nodeData)
    {
        return nodeData?.traitData?.displayName
            ?? string.Empty;
    }

    private List<string> GetNodePrerequisites(TraitNode nodeData)
    {
        var result = new List<string>();
        foreach (var conn in _treeData.connections)
        {
            if (conn.toNodeIDs == null || conn.toNodeIDs.Length == 0) continue;

            bool isTarget = false;
            foreach (string id in conn.toNodeIDs)
                if (id == nodeData.nodeID) { isTarget = true; break; }
            if (!isTarget) continue;

            if (conn.fromNodeIDs != null)
                foreach (string fromID in conn.fromNodeIDs)
                {
                    if (string.IsNullOrEmpty(fromID)) continue;
                    var fromNode = _treeData.nodes.Find(n => n.nodeID == fromID);
                    string fromName = fromNode?.traitData?.displayName;
                    if (!string.IsNullOrEmpty(fromName))
                        result.Add(fromName);
                }
        }
        return result;
    }
}
