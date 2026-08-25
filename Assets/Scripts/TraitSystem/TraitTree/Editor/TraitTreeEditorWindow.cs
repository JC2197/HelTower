using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Visual editor for designing trait trees with drag-and-drop nodes and connections.
/// Open via: Window → Trait System → Tree Editor
/// </summary>
public class TraitTreeEditorWindow : EditorWindow
{
    private const int MaxPreviewDimension = 4096;
    private const int MaxPreviewPixels = 16 * 1024 * 1024;
    private const int MaxCanvasDimension = 100000;
    private TraitTree currentTree;
    private Vector2 scrollPosition;
    private Vector2 canvasOffset = Vector2.zero;
    private float zoomLevel = 1f;

    // Node editing
    private int selectedNodeIndex = -1;
    private int selectedConnectionIndex = -1;
    private bool isDraggingNode = false;
    private Vector2 dragStartPosition;
    private bool isConnectingNodes = false;
    private int connectSourceIndex = -1;

    // Box selection
    private bool isBoxSelecting = false;
    private Vector2 boxSelectionStart;
    private Vector2 boxSelectionEnd;
    private List<int> selectedNodeIndices = new List<int>();


    // Directional bubble connection drag
    private bool _isDraggingBubble = false;
    private int _bubbleDragNodeIndex = -1;
    private int _bubbleDragDir = -1; // 0=N, 1=S, 2=E, 3=W
    // Direction offsets (screen space: N=up=-y, S=down=+y, E=right=+x, W=left=-x)
    private static readonly Vector2[] s_BubbleDirOffsets =
    {
        new Vector2( 0, -1), // 0 = North
        new Vector2( 0,  1), // 1 = South
        new Vector2( 1,  0), // 2 = East
        new Vector2(-1,  0), // 3 = West
    };

    // Clipboard
    private List<TraitNode> copiedNodes = new List<TraitNode>();
    private Vector2 copyOrigin;

    // Editor tabs
    private int currentTab = 0;
    private string[] tabNames = new string[] { "nodes", "Connections" };

    // UI Layout
    private Rect canvasRect;
    private Rect inspectorRect;
    private float inspectorWidth = 300f;

    // Visual settings
    private float nodeSize = 50f;
    private float gridSize = 16f;
    private bool showGrid = true;
    private bool snapToGrid = true;
    private GridType gridType = GridType.Square;

    // Grid type enum
    private enum GridType
    {
        Square,
        Hexagonal,
        Triangular
    }

    // Colors
    private Color nodeColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    private Color selectedNodeColor = Color.yellow;
    private Color connectionColor = Color.white;

    // Live pixel preview — rasterized with the exact same logic TraitTreeUI uses at runtime.
    private Texture2D _previewTex;
    private int _previewSourceWidth;
    private int _previewSourceHeight;
    private bool _previewDirty = true;

    [MenuItem("Window/Trait System/Tree Editor")]
    public static void ShowWindow()
    {
        TraitTreeEditorWindow window = GetWindow<TraitTreeEditorWindow>("Tree Editor");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }

    private void OnEnable()
    {
        // Try to load the last opened tree
        string lastTreePath = EditorPrefs.GetString("TraitTreeEditor_LastTree", "");
        if (!string.IsNullOrEmpty(lastTreePath))
        {
            currentTree = AssetDatabase.LoadAssetAtPath<TraitTree>(lastTreePath);
        }
    }

    private void OnDisable()
    {
        if (_previewTex != null)
        {
            DestroyImmediate(_previewTex);
            _previewTex = null;
        }
    }

    // Rebuilding uses RenderTexture ops for non-readable sprites, so it must not run inside OnGUI.
    private void Update()
    {
        if (currentTree != null)
        {
            RebuildPreview();
            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (currentTree == null)
        {
            DrawNoTreeSelected();
            return;
        }

        // Layout
        canvasRect = new Rect(0, 25, position.width - inspectorWidth, position.height - 25);
        inspectorRect = new Rect(position.width - inspectorWidth, 25, inspectorWidth, position.height - 25);

        DrawCanvas();
        DrawInspector();

        // Handle events
        HandleEvents();

        // Mark dirty if changed
        if (GUI.changed)
        {
            EditorUtility.SetDirty(currentTree);
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Tree selector
        EditorGUI.BeginChangeCheck();
        TraitTree newTree = (TraitTree)EditorGUILayout.ObjectField(currentTree, typeof(TraitTree), false, GUILayout.Width(200));
        if (EditorGUI.EndChangeCheck() && newTree != currentTree)
        {
            currentTree = newTree;
            selectedNodeIndex = -1;
            if (currentTree != null)
            {
                string path = AssetDatabase.GetAssetPath(currentTree);
                EditorPrefs.SetString("TraitTreeEditor_LastTree", path);
            }
        }

        GUILayout.FlexibleSpace();

        // Tools
        if (GUILayout.Button("Add Node", EditorStyles.toolbarButton))
        {
            AddNewNode();
        }

        if (GUILayout.Button("Add Connection", EditorStyles.toolbarButton))
        {
            AddNewConnection();
        }

        GUILayout.Space(10);

        showGrid = GUILayout.Toggle(showGrid, "Grid", EditorStyles.toolbarButton);
        snapToGrid = GUILayout.Toggle(snapToGrid, "Snap", EditorStyles.toolbarButton);

        // Grid type selector
        if (showGrid)
        {
            gridType = (GridType)EditorGUILayout.EnumPopup(gridType, EditorStyles.toolbarDropDown, GUILayout.Width(100));

            // Grid size control
            GUILayout.Label("Size:", EditorStyles.toolbarButton, GUILayout.Width(35));
            float newGridSize = EditorGUILayout.FloatField(gridSize, EditorStyles.toolbarTextField, GUILayout.Width(40));
            if (newGridSize != gridSize && newGridSize > 0)
            {
                gridSize = Mathf.Clamp(newGridSize, 4f, 100f);
                Repaint();
            }
        }

        GUILayout.Space(10);

        // Zoom controls
        GUILayout.Label($"Zoom: {(zoomLevel * 100):F0}%", EditorStyles.toolbarButton, GUILayout.Width(80));
        if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            zoomLevel = Mathf.Clamp(zoomLevel - 0.1f, 0.3f, 3f);
            Repaint();
        }
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            zoomLevel = Mathf.Clamp(zoomLevel + 0.1f, 0.3f, 3f);
            Repaint();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Center View", EditorStyles.toolbarButton))
        {
            CenterView();
        }

        if (GUILayout.Button("Save", EditorStyles.toolbarButton))
        {
            SaveTree();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawNoTreeSelected()
    {
        GUILayout.BeginArea(new Rect(0, 25, position.width, position.height - 25));
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();
        GUILayout.Label("No Trait Tree Selected", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Create New Tree", GUILayout.Width(150)))
        {
            CreateNewTree();
        }

        if (GUILayout.Button("Open Existing Tree", GUILayout.Width(150)))
        {
            OpenExistingTree();
        }

        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }

    private void DrawCanvas()
    {
        // Draw canvas background
        EditorGUI.DrawRect(canvasRect, new Color(0.2f, 0.2f, 0.2f, 1f));
        GUILayout.BeginArea(canvasRect);
        UpdateCanvasSizeFromContent();

        Vector2 canvasCenter = canvasRect.size / 2 + canvasOffset * zoomLevel;
        Rect workspaceRect = GetWorkspaceRect(canvasCenter);
        Color bgColor = new Color(0.12f, 0.15f, 0.2f, 1f);
        EditorGUI.DrawRect(workspaceRect, bgColor);
        // Draw grid
        if (showGrid)
        {
            DrawGrid();
        }
        DrawCenterIndicator();

        // Live pixel preview — exact match of in-game node icons + connection rendering
        if (currentTree.canvasWidth > 0 && currentTree.canvasHeight > 0 && _previewTex != null)
        {
            Vector2 previewCenter = canvasRect.size / 2 + canvasOffset * zoomLevel;
            GUI.DrawTexture(workspaceRect, _previewTex, ScaleMode.StretchToFill, true);
        }

        // Draw connections (selection highlight only — the preview above already shows every line)
        DrawConnections();

        // Draw nodes (selection border + label only — the preview above already shows every icon)
        DrawNodes();
        DrawSelectionHighlights(canvasCenter);
        DrawSelectedConnectionHighlight(canvasCenter);
        DrawNodeBubbles(canvasCenter);
        // Draw connection line preview
        if (isConnectingNodes && connectSourceIndex >= 0)
        {
            DrawConnectionPreview();
        }

        // Draw box selection rectangle
        if (isBoxSelecting)
        {
            DrawBoxSelection();
        }

        GUILayout.EndArea();
    }

    private Rect GetWorkspaceRect(Vector2 center)
    {
        if (currentTree == null)
            return new Rect(0, 0, canvasRect.width, canvasRect.height);

        float w = Mathf.Max(1, currentTree.canvasWidth) * zoomLevel;
        float h = Mathf.Max(1, currentTree.canvasHeight) * zoomLevel;
        return new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);
    }

    private void DrawNodeBubbles(Vector2 canvasCenter)
    {
        if (currentTree?.nodes == null) return;

        // Use EditorGUI.DrawRect (no GPU geometry) to avoid D3D12 command-queue overload.
        for (int i = 0; i < currentTree.nodes.Count; i++)
        {
            for (int d = 0; d < 4; d++)
            {
                Vector2 bp = GetBubbleScreenPos(i, d, canvasCenter);
                bool isActiveSrc = _isDraggingBubble && _bubbleDragNodeIndex == i && _bubbleDragDir == d;
                float sz = isActiveSrc ? 16f : 12f;
                Color fill = isActiveSrc ? Color.yellow : new Color(0.2f, 0.8f, 1f, 0.85f);
                Color border = new Color(0f, 0f, 0f, 0.5f);
                // Fill
                EditorGUI.DrawRect(new Rect(bp.x - sz / 2, bp.y - sz / 2, sz, sz), fill);
                // 1-pixel border
                EditorGUI.DrawRect(new Rect(bp.x - sz / 2, bp.y - sz / 2, sz, 1), border);
                EditorGUI.DrawRect(new Rect(bp.x - sz / 2, bp.y + sz / 2 - 1, sz, 1), border);
                EditorGUI.DrawRect(new Rect(bp.x - sz / 2, bp.y - sz / 2, 1, sz), border);
                EditorGUI.DrawRect(new Rect(bp.x + sz / 2 - 1, bp.y - sz / 2, 1, sz), border);
            }
        }

        // Preview line while bubble-dragging (Handles only for the single line, not per-node)
        if (_isDraggingBubble && _bubbleDragNodeIndex >= 0 && _bubbleDragNodeIndex < currentTree.nodes.Count)
        {
            Vector2 from = GetBubbleScreenPos(_bubbleDragNodeIndex, _bubbleDragDir, canvasCenter);
            // Inside GUILayout.BeginArea, Event.current.mousePosition is already canvas-local.
            Vector2 toLocal = Event.current.mousePosition;
            Handles.BeginGUI();
            Handles.color = Color.yellow;
            Handles.DrawDottedLine(from, toLocal, 4f);
            Handles.EndGUI();
        }
    }

    private Vector2 GetBubbleScreenPos(int nodeIdx, int dir, Vector2 canvasCenter)
    {
        float halfPx = (currentTree.nodeIconSize > 0 ? currentTree.nodeIconSize : nodeSize) / 2f;
        float screenOffset = (halfPx + 1f) * zoomLevel;
        Vector2 nodeScreen = canvasCenter + currentTree.nodes[nodeIdx].position * zoomLevel;
        return nodeScreen + s_BubbleDirOffsets[dir] * screenOffset;
    }

    private bool GetBubbleAtPosition(Vector2 mousePos, Vector2 canvasCenter, out int nodeIdx, out int dir)
    {
        nodeIdx = -1;
        dir = -1;
        if (currentTree?.nodes == null) return false;
        float hitRadius = Mathf.Max(6f, 3f * zoomLevel);
        for (int i = 0; i < currentTree.nodes.Count; i++)
            for (int d = 0; d < 4; d++)
            {
                if (Vector2.Distance(mousePos, GetBubbleScreenPos(i, d, canvasCenter)) <= hitRadius)
                {
                    nodeIdx = i;
                    dir = d;
                    return true;
                }
            }
        return false;
    }
    private void UpdateCanvasSizeFromContent()
    {
        if (currentTree == null || !currentTree.autoCanvasSize)
            return;

        int minW = Mathf.Clamp(currentTree.minCanvasWidth, 1, MaxCanvasDimension);
        int minH = Mathf.Clamp(currentTree.minCanvasHeight, 1, MaxCanvasDimension);
        int padding = Mathf.Max(0, currentTree.autoCanvasPadding);

        float halfNode = Mathf.Max(currentTree.nodeIconSize, 4) * 0.5f;
        float maxAbsX = halfNode + padding;
        float maxAbsY = halfNode + padding;

        if (currentTree.nodes != null)
        {
            foreach (var node in currentTree.nodes)
            {
                maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(node.position.x) + halfNode + padding);
                maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(node.position.y) + halfNode + padding);
            }
        }

        int targetW = Mathf.Clamp(Mathf.CeilToInt(maxAbsX * 2f), minW, MaxCanvasDimension);
        int targetH = Mathf.Clamp(Mathf.CeilToInt(maxAbsY * 2f), minH, MaxCanvasDimension);

        if (currentTree.canvasWidth != targetW || currentTree.canvasHeight != targetH)
        {
            currentTree.canvasWidth = targetW;
            currentTree.canvasHeight = targetH;
            _previewDirty = true;
            EditorUtility.SetDirty(currentTree);
        }
    }

    private void DrawGrid()
    {
        Handles.BeginGUI();
        Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        Vector2 center = canvasRect.size / 2 + canvasOffset * zoomLevel;
        float scaledGridSize = gridSize * zoomLevel;
        DrawSquareGrid(center, scaledGridSize);


        Handles.EndGUI();
    }

    private void DrawSquareGrid(Vector2 center, float scaledGridSize)
    {
        // Vertical lines
        for (float x = center.x % scaledGridSize; x < canvasRect.width; x += scaledGridSize)
        {
            Handles.DrawLine(new Vector3(x, 0), new Vector3(x, canvasRect.height));
        }

        // Horizontal lines
        for (float y = center.y % scaledGridSize; y < canvasRect.height; y += scaledGridSize)
        {
            Handles.DrawLine(new Vector3(0, y), new Vector3(canvasRect.width, y));
        }
    }


    private void DrawTriangle(Vector2 center, float size, bool pointUp)
    {
        float height = size * 0.866f; // sqrt(3)/2
        Vector3[] points = new Vector3[4];

        if (pointUp)
        {
            points[0] = new Vector3(center.x, center.y - height / 2, 0); // Top
            points[1] = new Vector3(center.x - size / 2, center.y + height / 2, 0); // Bottom left
            points[2] = new Vector3(center.x + size / 2, center.y + height / 2, 0); // Bottom right
        }
        else
        {
            points[0] = new Vector3(center.x, center.y + height / 2, 0); // Bottom
            points[1] = new Vector3(center.x - size / 2, center.y - height / 2, 0); // Top left
            points[2] = new Vector3(center.x + size / 2, center.y - height / 2, 0); // Top right
        }
        points[3] = points[0]; // Close the triangle

        Handles.DrawPolyLine(points);
    }

    private void DrawFilledHexagon(Vector2 center, float radius, Color fillColor)
    {
        Vector3[] points = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = 60f * i * Mathf.Deg2Rad;
            points[i] = new Vector3(
                center.x + radius * Mathf.Cos(angle),
                center.y + radius * Mathf.Sin(angle),
                0
            );
        }

        Handles.DrawSolidDisc(center, Vector3.forward, radius);

        // Draw hexagon outline for cleaner edges
        Vector3[] outline = new Vector3[7];
        for (int i = 0; i < 6; i++)
        {
            outline[i] = points[i];
        }
        outline[6] = points[0];

        Color oldColor = Handles.color;
        Handles.color = fillColor;

        // Draw filled triangles to form hexagon
        for (int i = 0; i < 6; i++)
        {
            Vector3[] triangle = new Vector3[3] { center, points[i], points[(i + 1) % 6] };
            Handles.DrawAAConvexPolygon(triangle);
        }

        Handles.color = oldColor;
    }

    private void DrawCenterIndicator()
    {
        Handles.BeginGUI();

        Vector2 center = canvasRect.size / 2 + canvasOffset * zoomLevel;

        // Draw crosshair at center (0,0 world position)
        float crosshairSize = 20f;
        Handles.color = new Color(1f, 0.5f, 0f, 0.8f); // Orange color

        // Vertical line
        Handles.DrawLine(
            new Vector3(center.x, center.y - crosshairSize),
            new Vector3(center.x, center.y + crosshairSize)
        );

        // Horizontal line
        Handles.DrawLine(
            new Vector3(center.x - crosshairSize, center.y),
            new Vector3(center.x + crosshairSize, center.y)
        );

        // Draw a small circle at the exact center
        Handles.color = new Color(1f, 0.5f, 0f, 0.5f);
        Handles.DrawSolidDisc(center, Vector3.forward, 3f);

        Handles.EndGUI();
    }

    private void DrawConnections()
    {
        if (currentTree == null || currentTree.nodes == null || currentTree.connections == null) return;
        if (selectedConnectionIndex < 0 || selectedConnectionIndex >= currentTree.connections.Count) return;

        var connection = currentTree.connections[selectedConnectionIndex];
        if (connection.fromNodeIDs == null || connection.fromNodeIDs.Length == 0 ||
            connection.toNodeIDs == null || connection.toNodeIDs.Length == 0) return;

        var fromNode = currentTree.nodes.FirstOrDefault(n => n.nodeID == connection.fromNodeIDs[0]);
        var toNode = currentTree.nodes.FirstOrDefault(n => n.nodeID == connection.toNodeIDs[0]);
        if (fromNode == null || toNode == null) return;

        Vector2 center = canvasRect.size / 2 + canvasOffset * zoomLevel;
        Vector2 fromPos = center + fromNode.position * zoomLevel;
        Vector2 toPos = center + toNode.position * zoomLevel;

        Handles.BeginGUI();
        Handles.color = Color.yellow;
        Handles.DrawAAPolyLine(5f, fromPos, toPos);
        Handles.EndGUI();
    }


    private void DrawSelectionHighlights(Vector2 canvasCenter)
    {
        if (currentTree?.nodes == null) return;
        float rawSize = currentTree.nodeIconSize > 0 ? currentTree.nodeIconSize : nodeSize;
        float half = rawSize * zoomLevel / 2f;

        Handles.BeginGUI();
        Handles.color = Color.green;
        for (int i = 0; i < currentTree.nodes.Count; i++)
        {
            if (i != selectedNodeIndex && !selectedNodeIndices.Contains(i)) continue;
            Vector2 screenPos = canvasCenter + currentTree.nodes[i].position * zoomLevel;
            Vector3[] corners = new Vector3[4]
            {
                new Vector3(screenPos.x - half, screenPos.y - half, 0f),
                new Vector3(screenPos.x + half, screenPos.y - half, 0f),
                new Vector3(screenPos.x + half, screenPos.y + half, 0f),
                new Vector3(screenPos.x - half, screenPos.y + half, 0f),
            };
            Handles.DrawSolidRectangleWithOutline(corners,
                new Color(0f, 1f, 0f, 0.08f),
                Color.green);
        }
        Handles.EndGUI();
    }

    private void DrawSelectedConnectionHighlight(Vector2 canvasCenter)
    {
        if (currentTree?.connections == null || selectedConnectionIndex < 0 ||
            selectedConnectionIndex >= currentTree.connections.Count) return;

        var conn = currentTree.connections[selectedConnectionIndex];
        if (conn.fromNodeIDs == null || conn.fromNodeIDs.Length == 0 ||
            conn.toNodeIDs == null || conn.toNodeIDs.Length == 0) return;

        var fromNode = currentTree.nodes?.FirstOrDefault(n => n.nodeID == conn.fromNodeIDs[0]);
        var toNode = currentTree.nodes?.FirstOrDefault(n => n.nodeID == conn.toNodeIDs[0]);
        if (fromNode == null || toNode == null) return;

        Handles.BeginGUI();
        Handles.color = Color.green;
        Handles.DrawAAPolyLine(3f,
            canvasCenter + fromNode.position * zoomLevel,
            canvasCenter + toNode.position * zoomLevel);
        Handles.EndGUI();
    }
    private void DrawNodes()
    {
        if (currentTree == null || currentTree.nodes == null) return;

        Handles.BeginGUI();

        Vector2 center = canvasRect.size / 2 + canvasOffset * zoomLevel;
        float scalednodesize = nodeSize * zoomLevel;

        for (int i = 0; i < currentTree.nodes.Count; i++)
        {
            var node = currentTree.nodes[i];
            Vector2 nodePos = center + node.position * zoomLevel;
            Rect nodeRect = new Rect(nodePos.x - scalednodesize / 2, nodePos.y - scalednodesize / 2, scalednodesize, scalednodesize);

            // Highlight if selected (either primary selection or in multi-selection) — the icon
            // itself is drawn by the pixel preview above, so only a selection border is needed here.            

            // Draw node label
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.alignment = TextAnchor.UpperCenter;
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(10 * zoomLevel));

            string labelText = node.traitData != null ? node.traitData.displayName : node.nodeID;
            GUI.Label(new Rect(nodeRect.x, nodeRect.yMax + 2, nodeRect.width, 20), labelText, labelStyle);
        }

        Handles.EndGUI();
    }

    private void DrawBoxSelection()
    {
        Rect selectionRect = GetBoxSelectionRect();

        Handles.BeginGUI();

        // Draw filled rectangle
        Handles.DrawSolidRectangleWithOutline(
            selectionRect,
            new Color(0.3f, 0.6f, 1f, 0.1f),
            new Color(0.3f, 0.6f, 1f, 0.8f)
        );

        Handles.EndGUI();
    }

    private Rect GetBoxSelectionRect()
    {
        float minX = Mathf.Min(boxSelectionStart.x, boxSelectionEnd.x);
        float minY = Mathf.Min(boxSelectionStart.y, boxSelectionEnd.y);
        float width = Mathf.Abs(boxSelectionEnd.x - boxSelectionStart.x);
        float height = Mathf.Abs(boxSelectionEnd.y - boxSelectionStart.y);

        return new Rect(minX, minY, width, height);
    }

    private void FinalizeBoxSelection(Vector2 center)
    {
        if (currentTree == null || currentTree.nodes == null) return;

        Rect selectionRect = GetBoxSelectionRect();
        float scalednodesize = nodeSize * zoomLevel;

        // Find all nodes within selection rectangle
        for (int i = 0; i < currentTree.nodes.Count; i++)
        {
            var node = currentTree.nodes[i];
            Vector2 nodePos = center + node.position * zoomLevel;
            Rect nodeRect = new Rect(
                nodePos.x - scalednodesize / 2,
                nodePos.y - scalednodesize / 2,
                scalednodesize,
                scalednodesize
            );

            // Check if node overlaps with selection box
            if (selectionRect.Overlaps(nodeRect))
            {
                if (!selectedNodeIndices.Contains(i))
                {
                    selectedNodeIndices.Add(i);
                }

                // Set primary selection to the last selected node
                selectedNodeIndex = i;
            }
        }
    }

    private void DrawConnectionPreview()
    {
        if (connectSourceIndex < 0 || connectSourceIndex >= currentTree.nodes.Count) return;

        Vector2 center = canvasRect.size / 2 + canvasOffset * zoomLevel;
        Vector2 startPos = center + currentTree.nodes[connectSourceIndex].position * zoomLevel;
        Vector2 mousePos = Event.current.mousePosition;

        Handles.BeginGUI();
        Handles.color = Color.yellow;
        Handles.DrawDottedLine(startPos, mousePos, 4f);
        Handles.EndGUI();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pixel preview — mirrors TraitTreeUI's runtime CreateConnectionWidgets/CreateNodeWidgets
    // ─────────────────────────────────────────────────────────────────────────

    private static void GetSafePreviewSize(int sourceW, int sourceH, out int safeW, out int safeH, out bool downscaled)
    {
        sourceW = Mathf.Max(sourceW, 1);
        sourceH = Mathf.Max(sourceH, 1);

        float scaleDim = Mathf.Min(MaxPreviewDimension / (float)sourceW, MaxPreviewDimension / (float)sourceH);
        float pixelRatio = MaxPreviewPixels / (float)(sourceW * (double)sourceH);
        float scaleArea = pixelRatio >= 1f ? 1f : Mathf.Sqrt(pixelRatio);
        float scale = Mathf.Min(1f, scaleDim, scaleArea);

        safeW = Mathf.Max(1, Mathf.RoundToInt(sourceW * scale));
        safeH = Mathf.Max(1, Mathf.RoundToInt(sourceH * scale));
        downscaled = safeW != sourceW || safeH != sourceH;
    }

    private void RebuildPreview()
    {
        _previewSourceWidth = Mathf.Max(currentTree.canvasWidth, 1);
        _previewSourceHeight = Mathf.Max(currentTree.canvasHeight, 1);

        GetSafePreviewSize(_previewSourceWidth, _previewSourceHeight, out int w, out int h, out _);

        if (_previewTex == null || _previewTex.width != w || _previewTex.height != h)
        {
            if (_previewTex != null) DestroyImmediate(_previewTex);
            _previewTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _previewTex.filterMode = FilterMode.Point;
        }

        Color32[] pixels = new Color32[w * h]; // zero-initialised = transparent

        if (currentTree.connections != null && currentTree.nodes != null)
        {
            foreach (var conn in currentTree.connections)
            {
                if (conn.fromNodeIDs == null || conn.fromNodeIDs.Length == 0 ||
                    conn.toNodeIDs == null || conn.toNodeIDs.Length == 0) continue;

                if (conn.useDrawnPath)
                {
                    if (conn.paintedPixels != null)
                    {
                        foreach (var p in conn.paintedPixels)
                        {
                            if (p.x < 0 || p.x >= _previewSourceWidth || p.y < 0 || p.y >= _previewSourceHeight) continue;

                            int sx = Mathf.RoundToInt((p.x / (float)Mathf.Max(_previewSourceWidth - 1, 1)) * (w - 1));
                            int sy = Mathf.RoundToInt((p.y / (float)Mathf.Max(_previewSourceHeight - 1, 1)) * (h - 1));
                            if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                                pixels[sy * w + sx] = (Color32)p.color;
                        }
                    }
                }
                else
                {
                    var fromNode = currentTree.nodes.FirstOrDefault(n => n.nodeID == conn.fromNodeIDs[0]);
                    var toNode = currentTree.nodes.FirstOrDefault(n => n.nodeID == conn.toNodeIDs[0]);
                    if (fromNode == null || toNode == null) continue;

                    Vector2Int texA = NodeToTexCoord(fromNode.position, w, h);
                    Vector2Int texB = NodeToTexCoord(toNode.position, w, h);

                    int iconHalf = Mathf.Max(currentTree.nodeIconSize, 4) / 2 + 1;
                    if (conn.fromBubbleDir >= 0 && conn.fromBubbleDir < 4)
                    {
                        Vector2 sd = s_BubbleDirOffsets[conn.fromBubbleDir];
                        texA += new Vector2Int(Mathf.RoundToInt(sd.x * iconHalf), Mathf.RoundToInt(-sd.y * iconHalf));
                    }
                    if (conn.toBubbleDir >= 0 && conn.toBubbleDir < 4)
                    {
                        Vector2 sd = s_BubbleDirOffsets[conn.toBubbleDir];
                        texB += new Vector2Int(Mathf.RoundToInt(sd.x * iconHalf), Mathf.RoundToInt(-sd.y * iconHalf));
                    }

                    PixelConnectionDrawer.DrawConnection(pixels, w, h, texA, texB,
                        conn.curveAmount, Mathf.Max(1, conn.lineWidth), (Color32)conn.lineColor,
                        conn.fromBubbleDir, conn.toBubbleDir);
                }
            }
        }

        if (currentTree.nodes != null)
        {
            int iconSize = Mathf.Max(currentTree.nodeIconSize, 4);
            Sprite iconFrame = currentTree.nodeIconFrame;

            foreach (var node in currentTree.nodes)
            {
                Vector2Int center = NodeToTexCoord(node.position, w, h);
                Sprite icon = node.traitData != null ? node.traitData.traitIcon : null;

                if (icon != null)
                    DrawSpriteToPixels(pixels, w, h, center, iconSize, icon);
                else
                    FillPixelRect(pixels, w, h, center.x - iconSize / 2, center.y - iconSize / 2, iconSize, iconSize, new Color32(102, 102, 102, 255));

                if (iconFrame != null)
                    DrawSpriteToPixels(pixels, w, h, center, iconSize, iconFrame);
            }
        }

        _previewTex.SetPixels32(pixels);
        _previewTex.Apply();
    }

    private static Vector2Int NodeToTexCoord(Vector2 nodePos, int texW, int texH) =>
        new Vector2Int(
            Mathf.RoundToInt(texW / 2f + nodePos.x),
            Mathf.RoundToInt(texH / 2f - nodePos.y));

    private static void DrawSpriteToPixels(Color32[] pixels, int texW, int texH, Vector2Int center, int size, Sprite sprite)
    {
        Texture2D src = sprite.texture.isReadable ? sprite.texture : GetReadableCopy(sprite.texture);
        bool isTempCopy = !sprite.texture.isReadable;

        Rect r = sprite.rect;
        int half = size / 2;
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                int srcX = Mathf.Clamp(Mathf.RoundToInt(r.x + (float)px / size * r.width), (int)r.x, Mathf.Max((int)r.x, (int)(r.x + r.width) - 1));
                int srcY = Mathf.Clamp(Mathf.RoundToInt(r.y + (float)py / size * r.height), (int)r.y, Mathf.Max((int)r.y, (int)(r.y + r.height) - 1));
                Color32 col = src.GetPixel(srcX, srcY);
                if (col.a < 10) continue;

                int dstX = center.x - half + px;
                int dstY = center.y - half + py;
                if (dstX >= 0 && dstX < texW && dstY >= 0 && dstY < texH)
                    pixels[dstY * texW + dstX] = col;
            }

        if (isTempCopy) Object.DestroyImmediate(src);
    }

    /// <summary>Blits a non-readable texture into a temporary readable Texture2D.</summary>
    private static Texture2D GetReadableCopy(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(
            source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    private static void FillPixelRect(Color32[] pixels, int texW, int texH, int x, int y, int w, int h, Color32 color)
    {
        for (int py = y; py < y + h; py++)
            for (int px = x; px < x + w; px++)
                if (px >= 0 && px < texW && py >= 0 && py < texH)
                    pixels[py * texW + px] = color;
    }

    private void DrawInspector()
    {
        GUILayout.BeginArea(inspectorRect);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

        // Tab selector
        currentTab = GUILayout.Toolbar(currentTab, tabNames);
        EditorGUILayout.Space();

        switch (currentTab)
        {
            case 0: // nodes
                if (selectedNodeIndex >= 0 && selectedNodeIndex < currentTree.nodes.Count)
                {
                    DrawNodeInspector();
                }
                else
                {
                    DrawTreeInspector();
                }
                break;
            case 1: // Connections
                DrawConnectionsInspector();
                break;
        }

        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawTreeInspector()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Total nodes: {currentTree.nodes.Count}");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "• Click a node to select it\n" +
            "• Drag a node to move it\n" +
            "• Shift+Click a node to start connection\n" +
            "• Shift+Click another node to finish connection\n" +
            "• Add Node to create new nodes\n" +
            "• Press X to remove selected nodes or connections",
            MessageType.Info
        );
    }

    private void DrawNodeInspector()
    {
        var node = currentTree.nodes[selectedNodeIndex];

        EditorGUILayout.LabelField("Node Properties", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        node.nodeID = EditorGUILayout.TextField("Node ID", node.nodeID);
        node.traitData = (TraitData)EditorGUILayout.ObjectField("Trait Data", node.traitData, typeof(TraitData), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Position", EditorStyles.boldLabel);
        node.position = EditorGUILayout.Vector2Field("", node.position);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(currentTree, "Modify Node Properties");
            EditorUtility.SetDirty(currentTree);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);

        if (node.connectedNodeIDs != null && node.connectedNodeIDs.Count > 0)
        {
            for (int i = node.connectedNodeIDs.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"→ {node.connectedNodeIDs[i]}", GUILayout.Width(150));
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    Undo.RecordObject(currentTree, "Remove Connection");
                    node.connectedNodeIDs.RemoveAt(i);
                    EditorUtility.SetDirty(currentTree);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("No connections");
        }

        if (GUILayout.Button(isConnectingNodes ? "Cancel Connection" : "Add Connection"))
        {
            if (isConnectingNodes)
            {
                isConnectingNodes = false;
                connectSourceIndex = -1;
            }
            else
            {
                isConnectingNodes = true;
                connectSourceIndex = selectedNodeIndex;
            }
        }
    }

    private void HandleEvents()
    {
        Event e = Event.current;
        Vector2 mousePos = e.mousePosition;

        // Handle X key to delete (works globally, not just over canvas)
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.X)
        {
            // Delete all selected nodes
            if (selectedNodeIndices.Count > 0)
            {
                // Sort in descending order to avoid index shifting issues
                var sortedIndices = selectedNodeIndices.OrderByDescending(x => x).ToList();
                foreach (int index in sortedIndices)
                {
                    if (index >= 0 && index < currentTree.nodes.Count)
                    {
                        DeleteNode(index);
                    }
                }
                selectedNodeIndices.Clear();
                selectedNodeIndex = -1;
                e.Use();
                Repaint();
                return;
            }
            else if (selectedNodeIndex >= 0)
            {
                DeleteNode(selectedNodeIndex);
                e.Use();
                Repaint();
                return;
            }
            else if (selectedConnectionIndex >= 0)
            {
                DeleteConnection(selectedConnectionIndex);
                e.Use();
                Repaint();
                return;
            }
        }

        // Handle Ctrl+C / Cmd+C (Copy)
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.C && (e.control || e.command))
        {
            if (selectedNodeIndices.Count > 0)
            {
                CopySelectedNodes();
                e.Use();
                return;
            }
        }

        // Handle Ctrl+V / Cmd+V (Paste)
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.V && (e.control || e.command))
        {
            if (copiedNodes.Count > 0)
            {
                PasteNodes();
                e.Use();
                Repaint();
                return;
            }
        }

        if (!canvasRect.Contains(mousePos)) return;
        Vector2 localMouse = mousePos - new Vector2(canvasRect.x, canvasRect.y);
        Vector2 center = canvasRect.size / 2 + canvasOffset * zoomLevel;
        
        if (!_isDraggingBubble && e.type == EventType.MouseDown && e.button == 0)
        {
            if (GetBubbleAtPosition(localMouse, center, out int bni, out int bd))
            {
                _isDraggingBubble = true;
                _bubbleDragNodeIndex = bni;
                _bubbleDragDir = bd;
                e.Use();
                Repaint();
                return;
            }
        }
        if (_isDraggingBubble)
        {
            if (e.type == EventType.MouseDrag)
            {
                e.Use();
                Repaint();
                return;
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (GetBubbleAtPosition(localMouse, center, out int bni, out int bd) && bni != _bubbleDragNodeIndex)
                    ConnectNodes(_bubbleDragNodeIndex, bni, _bubbleDragDir, bd);
                _isDraggingBubble = false;
                _bubbleDragNodeIndex = -1;
                _bubbleDragDir = -1;
                e.Use();
                Repaint();
                return;
            }
        }
        // Handle drag and drop of TraitData objects
        HandleDragAndDrop(mousePos, center, e);

        // Handle mouse wheel zoom
        if (e.type == EventType.ScrollWheel && canvasRect.Contains(mousePos))
        {
            float oldZoom = zoomLevel;
            zoomLevel = Mathf.Clamp(zoomLevel - e.delta.y * 0.05f, 0.3f, 3f);

            // Zoom towards mouse position
            Vector2 mouseOffset = mousePos - canvasRect.size / 2;
            canvasOffset += mouseOffset * (1f / oldZoom - 1f / zoomLevel);

            e.Use();
            Repaint();
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            // Check if clicking on a node FIRST (highest priority for dragging)
            int clickedNode = GetNodeAtPosition(mousePos, center);

            if (e.shift && isConnectingNodes && clickedNode >= 0 && clickedNode != connectSourceIndex)
            {
                // Finish connection
                Connectnodes(connectSourceIndex, clickedNode);
                isConnectingNodes = false;
                connectSourceIndex = -1;
                e.Use();
                Repaint();
            }
            else if (clickedNode >= 0)
            {
                // Node was clicked - prepare to drag
                // Check if clicking on an already selected node
                if (!selectedNodeIndices.Contains(clickedNode))
                {
                    // Clear multi-selection if not holding Ctrl/Cmd
                    if (!e.control && !e.command)
                    {
                        selectedNodeIndices.Clear();
                    }
                    selectedNodeIndices.Add(clickedNode);
                }

                selectedNodeIndex = clickedNode;
                selectedConnectionIndex = -1;
                isDraggingNode = true;
                dragStartPosition = mousePos;
                e.Use();
                Repaint();
            }
            else
            {
                // No node clicked - check if clicking on a connection
                int clickedConnection = -1;
                if (!e.shift)
                {
                    clickedConnection = GetConnectionAtPosition(mousePos, center);
                }

                if (clickedConnection >= 0)
                {
                    // Select the connection and switch to Connections tab
                    selectedConnectionIndex = clickedConnection;
                    selectedNodeIndex = -1;
                    currentTab = 1; // Switch to Connections tab
                    e.Use();
                    Repaint();
                    return;
                }
                else
                {
                    // Start box selection on empty space
                    if (!e.control && !e.command)
                    {
                        selectedNodeIndices.Clear();
                    }
                    selectedNodeIndex = -1;
                    selectedConnectionIndex = -1;
                    isBoxSelecting = true;
                    boxSelectionStart = mousePos;
                    boxSelectionEnd = mousePos;
                    e.Use();
                    Repaint();
                }
            }
        }

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            if (isBoxSelecting)
            {
                // Update box selection rectangle
                boxSelectionEnd = mousePos;
                e.Use();
                Repaint();
            }
            else if (isDraggingNode && selectedNodeIndices.Count > 0)
            {
                // Record undo on first drag movement
                if (dragStartPosition == mousePos)
                {
                    Undo.RecordObject(currentTree, "Move nodes");
                }

                // Drag all selected nodes
                Vector2 delta = e.delta / zoomLevel;
                foreach (int nodeIndex in selectedNodeIndices)
                {
                    if (nodeIndex >= 0 && nodeIndex < currentTree.nodes.Count)
                    {
                        currentTree.nodes[nodeIndex].position += delta;
                    }
                }
                EditorUtility.SetDirty(currentTree);
                e.Use();
                Repaint();
            }
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (isBoxSelecting)
            {
                // Finalize box selection
                FinalizeBoxSelection(center);
                isBoxSelecting = false;
                e.Use();
                Repaint();
            }
            else if (isDraggingNode && snapToGrid && selectedNodeIndices.Count > 0)
            {
                // Snap all selected nodes to grid on release
                foreach (int nodeIndex in selectedNodeIndices)
                {
                    if (nodeIndex >= 0 && nodeIndex < currentTree.nodes.Count)
                    {
                        Vector2 pos = currentTree.nodes[nodeIndex].position;
                        pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
                        pos.y = Mathf.Round(pos.y / gridSize) * gridSize;
                        currentTree.nodes[nodeIndex].position = pos;
                    }
                }
                EditorUtility.SetDirty(currentTree);
            }
            isDraggingNode = false;
        }

        if (e.type == EventType.MouseDrag && e.button == 2) // Middle mouse drag to pan
        {
            canvasOffset += e.delta / zoomLevel;
            e.Use();
            Repaint();
        }

        if (isConnectingNodes)
        {
            Repaint();
        }
    }
    private void ConnectNodes(int sourceIndex, int targetIndex, int fromDir = -1, int toDir = -1)
    {
        if (currentTree == null || sourceIndex < 0 || targetIndex < 0) return;
        if (sourceIndex >= currentTree.nodes.Count || targetIndex >= currentTree.nodes.Count) return;

        Undo.RecordObject(currentTree, "Connect Nodes");

        var sourceNode = currentTree.nodes[sourceIndex];
        var targetNode = currentTree.nodes[targetIndex];
        string targetID = targetNode.nodeID;

        if (sourceNode.connectedNodeIDs == null)
        {
            sourceNode.connectedNodeIDs = new List<string>();
        }

        if (!sourceNode.connectedNodeIDs.Contains(targetID))
        {
            sourceNode.connectedNodeIDs.Add(targetID);
        }

        CreateConnectionBetweenNodes(sourceNode, targetNode, fromDir, toDir);

        EditorUtility.SetDirty(currentTree);
    }

    private void CreateConnectionBetweenNodes(TraitNode sourceNode, TraitNode targetNode, int fromDir = -1, int toDir = -1)
    {
        if (currentTree.connections == null)
        {
            currentTree.connections = new List<TraitTreeConnection>();
        }

        string fromNodeID = sourceNode.nodeID;
        string toNodeID = targetNode.nodeID;

        bool exists = currentTree.connections.Any(c =>
            (c.fromNodeIDs != null && c.toNodeIDs != null &&
             c.fromNodeIDs.Contains(fromNodeID) && c.toNodeIDs.Contains(toNodeID)) ||
            (c.fromNodeIDs != null && c.toNodeIDs != null &&
             c.fromNodeIDs.Contains(toNodeID) && c.toNodeIDs.Contains(fromNodeID))
        );

        if (!exists)
        {
            TraitTreeConnection newConnection = new TraitTreeConnection
            {
                connectionID = $"conn_{System.Guid.NewGuid().ToString().Substring(0, 8)}",
                scale = 1f,
                fromNodeIDs = new string[] { fromNodeID },
                toNodeIDs = new string[] { toNodeID },
                fromBubbleDir = fromDir,
                toBubbleDir = toDir
            };

            currentTree.connections.Add(newConnection);
        }
    }

    private void HandleDragAndDrop(Vector2 mousePos, Vector2 center, Event e)
    {
        if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
        {
            // Check if we're dragging a TraitData object
            bool validDrag = false;
            TraitData draggedTrait = null;

            if (DragAndDrop.objectReferences.Length > 0)
            {
                draggedTrait = DragAndDrop.objectReferences[0] as TraitData;
                validDrag = draggedTrait != null;
            }

            if (validDrag)
            {
                // Show visual feedback
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    // Convert mouse position to world position (accounting for zoom and offset)
                    Vector2 worldPos = (mousePos - center) / zoomLevel;

                    // Snap to grid if enabled
                    if (snapToGrid)
                    {
                        worldPos.x = Mathf.Round(worldPos.x / gridSize) * gridSize;
                        worldPos.y = Mathf.Round(worldPos.y / gridSize) * gridSize;
                    }

                    // Create new node at this position
                    CreateNodeFromTrait(draggedTrait, worldPos);

                    e.Use();
                }
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }
        }

        if (e.type == EventType.DragExited)
        {
            Repaint();
        }
    }

    private void CreateNodeFromTrait(TraitData traitData, Vector2 position)
    {
        if (currentTree == null) return;

        Undo.RecordObject(currentTree, "Create Node from Trait");

        if (currentTree.nodes == null)
        {
            currentTree.nodes = new List<TraitNode>();
        }

        // Generate unique node ID from trait ID
        string nodeID = $"{traitData.traitID}_{System.Guid.NewGuid().ToString().Substring(0, 4)}";

        TraitNode newNode = new TraitNode
        {
            nodeID = nodeID,
            traitData = traitData,
            position = position,
            connectedNodeIDs = new List<string>()
        };

        currentTree.nodes.Add(newNode);
        selectedNodeIndex = currentTree.nodes.Count - 1;
        currentTab = 0; // Switch to nodes tab

        EditorUtility.SetDirty(currentTree);
        Repaint();
    }

    private void CopySelectedNodes()
    {
        if (currentTree == null || selectedNodeIndices.Count == 0) return;

        copiedNodes.Clear();

        // Calculate center of selected nodes for relative positioning
        Vector2 center = Vector2.zero;
        foreach (int index in selectedNodeIndices)
        {
            if (index >= 0 && index < currentTree.nodes.Count)
            {
                center += currentTree.nodes[index].position;
            }
        }
        center /= selectedNodeIndices.Count;
        copyOrigin = center;

        // Deep copy selected nodes
        foreach (int index in selectedNodeIndices)
        {
            if (index >= 0 && index < currentTree.nodes.Count)
            {
                var original = currentTree.nodes[index];
                var copy = new TraitNode
                {
                    nodeID = original.nodeID,
                    traitData = original.traitData,
                    position = original.position,
                    connectedNodeIDs = new List<string>(original.connectedNodeIDs ?? new List<string>())
                };
                copiedNodes.Add(copy);
            }
        }
    }

    private void PasteNodes()
    {
        if (currentTree == null || copiedNodes.Count == 0) return;

        Undo.RecordObject(currentTree, "Paste nodes");

        if (currentTree.nodes == null)
        {
            currentTree.nodes = new List<TraitNode>();
        }

        // Clear current selection
        selectedNodeIndices.Clear();

        // Paste offset (slightly offset from original position)
        Vector2 pasteOffset = new Vector2(gridSize * 2, gridSize * 2);

        // Map old node IDs to new node IDs for connection remapping
        Dictionary<string, string> nodeIDMap = new Dictionary<string, string>();

        // Create new nodes
        foreach (var copiedNode in copiedNodes)
        {
            // Generate new unique ID
            string newNodeID = $"{copiedNode.nodeID}_copy_{System.Guid.NewGuid().ToString().Substring(0, 4)}";
            nodeIDMap[copiedNode.nodeID] = newNodeID;

            // Calculate new position relative to copy origin
            Vector2 relativePos = copiedNode.position - copyOrigin;
            Vector2 newPosition = copyOrigin + relativePos + pasteOffset;

            // Snap to grid if enabled
            if (snapToGrid)
            {
                newPosition.x = Mathf.Round(newPosition.x / gridSize) * gridSize;
                newPosition.y = Mathf.Round(newPosition.y / gridSize) * gridSize;
            }

            TraitNode newNode = new TraitNode
            {
                nodeID = newNodeID,
                traitData = copiedNode.traitData,
                position = newPosition,
                connectedNodeIDs = new List<string>()
            };

            currentTree.nodes.Add(newNode);
            selectedNodeIndices.Add(currentTree.nodes.Count - 1);
        }

        // Remap connections (only preserve connections between pasted nodes)
        int startIndex = currentTree.nodes.Count - copiedNodes.Count;
        for (int i = 0; i < copiedNodes.Count; i++)
        {
            var copiedNode = copiedNodes[i];
            var newNode = currentTree.nodes[startIndex + i];

            if (copiedNode.connectedNodeIDs != null)
            {
                foreach (string oldConnectedID in copiedNode.connectedNodeIDs)
                {
                    // Only add connection if the connected node was also copied
                    if (nodeIDMap.ContainsKey(oldConnectedID))
                    {
                        newNode.connectedNodeIDs.Add(nodeIDMap[oldConnectedID]);
                    }
                }
            }
        }

        // Update copy origin for next paste
        copyOrigin += pasteOffset;

        // Select the last pasted node
        if (selectedNodeIndices.Count > 0)
        {
            selectedNodeIndex = selectedNodeIndices[selectedNodeIndices.Count - 1];
        }

        EditorUtility.SetDirty(currentTree);
    }

    private int GetNodeAtPosition(Vector2 mousePos, Vector2 center)
    {
        if (currentTree == null || currentTree.nodes == null) return -1;

        // Use 90% of node size for more precise clicking, but enforce a minimum size
        float scalednodesize = nodeSize * zoomLevel * 0.9f;
        // Ensure minimum clickable area of 40 pixels for usability at low zoom levels
        float clickableSize = Mathf.Max(scalednodesize, 40f);

        // Check nodes in reverse order (top-most first)
        for (int i = currentTree.nodes.Count - 1; i >= 0; i--)
        {
            Vector2 nodePos = center + currentTree.nodes[i].position * zoomLevel;
            Rect nodeRect = new Rect(nodePos.x - clickableSize / 2, nodePos.y - clickableSize / 2, clickableSize, clickableSize);

            if (nodeRect.Contains(mousePos))
            {
                return i;
            }
        }

        return -1;
    }

    private int GetConnectionAtPosition(Vector2 mousePos, Vector2 center)
    {
        if (currentTree == null || currentTree.connections == null) return -1;

        float clickThreshold = 10f; // Distance threshold for clicking a line

        // Check connections in reverse order (so top-most are checked first)
        for (int i = currentTree.connections.Count - 1; i >= 0; i--)
        {
            var connection = currentTree.connections[i];

            if (connection.fromNodeIDs != null && connection.fromNodeIDs.Length > 0 &&
                connection.toNodeIDs != null && connection.toNodeIDs.Length > 0)
            {
                var fromNode = currentTree.nodes.FirstOrDefault(n => n.nodeID == connection.fromNodeIDs[0]);
                var toNode = currentTree.nodes.FirstOrDefault(n => n.nodeID == connection.toNodeIDs[0]);

                if (fromNode != null && toNode != null)
                {
                    Vector2 fromPos = center + fromNode.position * zoomLevel;
                    Vector2 toPos = center + toNode.position * zoomLevel;

                    // Calculate distance from mouse to line segment
                    float distance = DistanceToLineSegment(mousePos, fromPos, toPos);

                    if (distance < clickThreshold)
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    private float DistanceToLineSegment(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        Vector2 line = lineEnd - lineStart;
        float lineLength = line.magnitude;

        if (lineLength < 0.001f)
            return Vector2.Distance(point, lineStart);

        float t = Mathf.Clamp01(Vector2.Dot(point - lineStart, line) / (lineLength * lineLength));
        Vector2 projection = lineStart + t * line;

        return Vector2.Distance(point, projection);
    }

    private void AddNewNode()
    {
        if (currentTree == null) return;

        Undo.RecordObject(currentTree, "Add Node");

        if (currentTree.nodes == null)
        {
            currentTree.nodes = new List<TraitNode>();
        }

        TraitNode newNode = new TraitNode
        {
            nodeID = $"node_{System.Guid.NewGuid().ToString().Substring(0, 8)}",
            position = Vector2.zero,
            connectedNodeIDs = new List<string>()
        };

        currentTree.nodes.Add(newNode);
        selectedNodeIndex = currentTree.nodes.Count - 1;

        EditorUtility.SetDirty(currentTree);
    }

    private void DeleteNode(int index)
    {
        if (currentTree == null || index < 0 || index >= currentTree.nodes.Count) return;

        Undo.RecordObject(currentTree, "Delete Node");

        string nodeID = currentTree.nodes[index].nodeID;

        // Remove connections to this node from connectedNodeIDs (legacy)
        foreach (var node in currentTree.nodes)
        {
            if (node.connectedNodeIDs != null)
            {
                node.connectedNodeIDs.RemoveAll(id => id == nodeID);
            }
        }

        // Remove connections from the connections list
        if (currentTree.connections != null)
        {
            currentTree.connections.RemoveAll(conn =>
            {
                // Remove if this node is in fromNodeIDs or toNodeIDs
                bool hasNodeInFrom = conn.fromNodeIDs != null && conn.fromNodeIDs.Contains(nodeID);
                bool hasNodeInTo = conn.toNodeIDs != null && conn.toNodeIDs.Contains(nodeID);
                return hasNodeInFrom || hasNodeInTo;
            });
        }

        currentTree.nodes.RemoveAt(index);
        selectedNodeIndex = -1;
        selectedNodeIndices.Clear();

        EditorUtility.SetDirty(currentTree);
    }

    private void Connectnodes(int sourceIndex, int targetIndex)
    {
        if (currentTree == null || sourceIndex < 0 || targetIndex < 0) return;
        if (sourceIndex >= currentTree.nodes.Count || targetIndex >= currentTree.nodes.Count) return;

        Undo.RecordObject(currentTree, "Connect nodes");

        var sourceNode = currentTree.nodes[sourceIndex];
        var targetNode = currentTree.nodes[targetIndex];
        string targetID = targetNode.nodeID;

        // Add to old system (for backward compatibility)
        if (sourceNode.connectedNodeIDs == null)
        {
            sourceNode.connectedNodeIDs = new List<string>();
        }

        if (!sourceNode.connectedNodeIDs.Contains(targetID))
        {
            sourceNode.connectedNodeIDs.Add(targetID);
        }

        // Create sprite connection
        CreateSpriteConnectionBetweennodes(sourceNode, targetNode);

        EditorUtility.SetDirty(currentTree);
    }

    private void CreateSpriteConnectionBetweennodes(TraitNode sourceNode, TraitNode targetNode)
    {
        if (currentTree.connections == null)
        {
            currentTree.connections = new List<TraitTreeConnection>();
        }

        // Determine direction based on click order
        // First clicked node (source) = FROM (prerequisite)
        // Second clicked node (target) = TO (dependent)
        string fromNodeID = sourceNode.nodeID;
        string toNodeID = targetNode.nodeID;

        Debug.Log($"[CreateConnection] FROM: {sourceNode.nodeID} (clicked first) -> TO: {targetNode.nodeID} (clicked second)");

        // Check if connection already exists
        bool exists = currentTree.connections.Any(c =>
            (c.fromNodeIDs != null && c.toNodeIDs != null &&
             c.fromNodeIDs.Contains(fromNodeID) && c.toNodeIDs.Contains(toNodeID)) ||
            (c.fromNodeIDs != null && c.toNodeIDs != null &&
             c.fromNodeIDs.Contains(toNodeID) && c.toNodeIDs.Contains(fromNodeID))
        );

        if (!exists)
        {
            TraitTreeConnection newConnection = new TraitTreeConnection
            {
                connectionID = $"conn_{System.Guid.NewGuid().ToString().Substring(0, 8)}",
                fromNodeIDs = new string[] { fromNodeID },
                toNodeIDs = new string[] { toNodeID } // Legacy support
            };

            currentTree.connections.Add(newConnection);
        }
    }

    private void CenterView()
    {
        canvasOffset = Vector2.zero;
        Repaint();
    }

    private void SaveTree()
    {
        if (currentTree != null)
        {
            EditorUtility.SetDirty(currentTree);
            AssetDatabase.SaveAssets();

            // Print all connections with their from/to relationships
            if (currentTree.connections != null && currentTree.connections.Count > 0)
            {
                Debug.Log("\n--- Connections ---");
                for (int i = 0; i < currentTree.connections.Count; i++)
                {
                    var conn = currentTree.connections[i];
                    string fromnodes = conn.fromNodeIDs != null && conn.fromNodeIDs.Length > 0
                        ? string.Join(", ", conn.fromNodeIDs)
                        : "none";
                    string tonodes = conn.toNodeIDs != null && conn.toNodeIDs.Length > 0
                        ? string.Join(", ", conn.toNodeIDs)
                        : "none";

                    // Get node names
                    string fromNames = "";
                    if (conn.fromNodeIDs != null)
                    {
                        foreach (var id in conn.fromNodeIDs)
                        {
                            var node = currentTree.nodes.FirstOrDefault(n => n.nodeID == id);
                            if (node?.traitData != null)
                                fromNames += node.traitData.displayName + ", ";
                        }
                        fromNames = fromNames.TrimEnd(',', ' ');
                    }

                    string toNames = "";
                    if (conn.toNodeIDs != null)
                    {
                        foreach (var id in conn.toNodeIDs)
                        {
                            var node = currentTree.nodes.FirstOrDefault(n => n.nodeID == id);
                            if (node?.traitData != null)
                                toNames += node.traitData.displayName + ", ";
                        }
                        toNames = toNames.TrimEnd(',', ' ');
                    }
                }
            }

            Debug.Log("=== Save Complete ===");
        }
    }

    private void CreateNewTree()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Trait Tree",
            "NewTraitTree",
            "asset",
            "Create a new trait tree data asset"
        );

        if (!string.IsNullOrEmpty(path))
        {
            TraitTree newTree = CreateInstance<TraitTree>();
            newTree.nodes = new List<TraitNode>();

            AssetDatabase.CreateAsset(newTree, path);
            AssetDatabase.SaveAssets();

            currentTree = newTree;
            EditorPrefs.SetString("TraitTreeEditor_LastTree", path);
        }
    }

    private void OpenExistingTree()
    {
        string path = EditorUtility.OpenFilePanel("Open Trait Tree", "Assets", "asset");
        if (!string.IsNullOrEmpty(path))
        {
            path = "Assets" + path.Substring(Application.dataPath.Length);
            currentTree = AssetDatabase.LoadAssetAtPath<TraitTree>(path);
            if (currentTree != null)
            {
                EditorPrefs.SetString("TraitTreeEditor_LastTree", path);
            }
        }
    }

    private void DrawConnectionsInspector()
    {
        EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);

        if (currentTree.connections == null)
        {
            currentTree.connections = new List<TraitTreeConnection>();
        }

        EditorGUILayout.LabelField($"Total Connections: {currentTree.connections.Count}");
        EditorGUILayout.Space();

        if (selectedConnectionIndex >= 0 && selectedConnectionIndex < currentTree.connections.Count)
        {
            DrawSelectedConnectionInspector();
        }
        else
        {
            EditorGUILayout.HelpBox("Select a connection from the list below or Shift+Click nodes to create connections", MessageType.Info);
            EditorGUILayout.Space();

            // List all connections
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < currentTree.connections.Count; i++)
            {
                var conn = currentTree.connections[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Show warning icon for orphaned connections
                bool hasEmptynodes = (conn.fromNodeIDs == null || conn.fromNodeIDs.Length == 0 ||
                                     conn.toNodeIDs == null || conn.toNodeIDs.Length == 0);
                string label = hasEmptynodes ? $"⚠ {i}: {conn.connectionID}" : $"{i}: {conn.connectionID}";

                if (GUILayout.Button(label, GUILayout.Height(25)))
                {
                    selectedConnectionIndex = i;
                    selectedNodeIndex = -1;
                }

                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Delete Connection",
                        $"Delete connection '{conn.connectionID}'?",
                        "Delete", "Cancel"))
                    {
                        DeleteConnection(i);
                        break; // Exit loop after deletion
                    }
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawSelectedConnectionInspector()
    {
        var connection = currentTree.connections[selectedConnectionIndex];

        EditorGUILayout.LabelField($"Connection {selectedConnectionIndex}", EditorStyles.boldLabel);

        connection.connectionID = EditorGUILayout.TextField("ID", connection.connectionID);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Directional nodes (From → To)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("From nodes = prerequisites (must be active first)\nTo nodes = dependents (become available when any from is active)", MessageType.Info);

        // From nodes
        EditorGUILayout.LabelField("From nodes (Prerequisites)", EditorStyles.boldLabel);
        if (connection.fromNodeIDs == null)
        {
            connection.fromNodeIDs = new string[0];
        }

        int newFromCount = EditorGUILayout.IntField("Count", connection.fromNodeIDs.Length);
        if (newFromCount != connection.fromNodeIDs.Length)
        {
            System.Array.Resize(ref connection.fromNodeIDs, Mathf.Max(0, newFromCount));
        }

        for (int i = 0; i < connection.fromNodeIDs.Length; i++)
        {
            connection.fromNodeIDs[i] = EditorGUILayout.TextField($"  From [{i}]", connection.fromNodeIDs[i] ?? "");
        }

        EditorGUILayout.Space();

        // To nodes
        EditorGUILayout.LabelField("To nodes (Dependents)", EditorStyles.boldLabel);
        if (connection.toNodeIDs == null)
        {
            connection.toNodeIDs = new string[0];
        }

        int newToCount = EditorGUILayout.IntField("Count", connection.toNodeIDs.Length);
        if (newToCount != connection.toNodeIDs.Length)
        {
            System.Array.Resize(ref connection.toNodeIDs, Mathf.Max(0, newToCount));
        }

        for (int i = 0; i < connection.toNodeIDs.Length; i++)
        {
            connection.toNodeIDs[i] = EditorGUILayout.TextField($"  To [{i}]", connection.toNodeIDs[i] ?? "");
        }

        EditorGUILayout.Space();

        // Warning for orphaned connections
        bool hasEmptyFromTo = (connection.fromNodeIDs == null || connection.fromNodeIDs.Length == 0 ||
                               connection.toNodeIDs == null || connection.toNodeIDs.Length == 0);
        if (hasEmptyFromTo)
        {
            EditorGUILayout.HelpBox("Warning: This connection is not linked to any nodes! Add From/To nodes.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Back to Connection List"))
        {
            selectedConnectionIndex = -1;
        }

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Delete Connection", GUILayout.Width(120)))
        {
            if (EditorUtility.DisplayDialog("Delete Connection",
                $"Are you sure you want to delete connection '{connection.connectionID}'?",
                "Delete", "Cancel"))
            {
                DeleteConnection(selectedConnectionIndex);
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }



    private void AddNewConnection()
    {
        if (currentTree == null) return;

        Undo.RecordObject(currentTree, "Add Connection");

        if (currentTree.connections == null)
        {
            currentTree.connections = new List<TraitTreeConnection>();
        }

        TraitTreeConnection newConnection = new TraitTreeConnection
        {
            connectionID = $"conn_{System.Guid.NewGuid().ToString().Substring(0, 8)}",
            fromNodeIDs = new string[0],
            toNodeIDs = new string[0]
        };

        currentTree.connections.Add(newConnection);
        selectedConnectionIndex = currentTree.connections.Count - 1;
        selectedNodeIndex = -1;
        currentTab = 1; // Switch to Connections tab

        EditorUtility.SetDirty(currentTree);
    }

    private void DeleteConnection(int index)
    {
        if (currentTree == null || index < 0 || index >= currentTree.connections.Count) return;

        Undo.RecordObject(currentTree, "Delete Connection");

        currentTree.connections.RemoveAt(index);
        selectedConnectionIndex = -1;

        EditorUtility.SetDirty(currentTree);
    }


}
