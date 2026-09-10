using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TraitTree", menuName = "Trait System/Trait Tree")]
public class TraitTree : ScriptableObject
{
    public string treeName;
    public string description;

    public float nodeSpacing = 100f;
    public int nodeIconSize = 17;

    [Tooltip("Frame sprite drawn behind every node icon in this tree.")]
    public Sprite nodeIconFrame;

    [Tooltip("Static background art shown behind the tree (fixed to the window; does not pan/zoom). Also previewed in the Tree Editor window.")]
    public Sprite editorBackgroundSprite;

    [Tooltip("Pin the bottom of the tree to the bottom of the window on open; players scroll upward to reveal higher nodes and cannot pan past the bottom edge.")]
    public bool anchorTreeToBottom = false;

    [Tooltip("Only used when Anchor To Bottom is on. Draws tinted level bands from the bottom edge upward; a node inside a band requires that many invested levels before nodes in higher bands unlock.")]
    public bool useLevelZones = false;

    [Tooltip("Number of level bands to draw upward from the bottom edge.")]
    [Min(1)]
    public int levelZoneBandCount = 3;

    [Tooltip("Levels represented by each band. Band N unlocks once the tree has N x Levels Per Band invested levels (band 0 requires 0).")]
    [Min(1)]
    public int levelsPerBand = 5;

    [Tooltip("Zoom level applied when the tree opens. 1 = fit-to-window. Clamped to the pan/zoom component's min/max.")]
    public float defaultZoom = 1f;

    [Tooltip("Allow players to zoom with the mouse wheel. When off, the tree stays locked at Default Zoom (panning still works).")]
    public bool allowZoom = true;

    [Tooltip("Editor-only: size of the in-game window frame previewed in the Tree Editor (tree units = screen pixels at 1x zoom). A design aid for seeing what's on-screen vs. scrollable; runtime uses the actual UI viewport.")]
    public Vector2Int previewWindowSize = new Vector2Int(350, 400);

    [Tooltip("Default pixel width for connection lines. Use 'Apply To All Connections' in the Tree Editor to push this onto every existing connection.")]
    [Min(1)]
    public int connectionLineWidth = 1;

    public int canvasWidth = 400;
    public List<TraitNode> nodes = new List<TraitNode>();
    public int canvasHeight = 225;
    [Tooltip("Minimum canvas width used by auto canvas sizing.")]
    public int minCanvasWidth = 400;

    [Tooltip("Minimum canvas height used by auto canvas sizing.")]
    public int minCanvasHeight = 225;
    [Tooltip("Automatically size canvas bounds to node layout in the editor.")]
    public bool autoCanvasSize = true;
    public int autoCanvasPadding = 80;
    public List<TraitTreeConnection> connections = new List<TraitTreeConnection>();

    /// <summary>Height of a single level band in tree units: Node Icon Size / 2 * Levels Per Band.</summary>
    public float LevelBandHeight => Mathf.Max(1, nodeIconSize) * 1f * Mathf.Max(1, levelsPerBand);

    /// <summary>
    /// Band index a node position falls in, measured from the canvas bottom edge upward.
    /// Band 0 is the lowest band; higher bands sit further up the tree.
    /// </summary>
    public int GetBandIndex(Vector2 position)
    {
        // Screen-up is negative position.y, and the bottom edge sits at +canvasHeight/2,
        // so a node's height above the bottom is (canvasHeight/2 - position.y).
        float heightAboveBottom = canvasHeight * 0.5f - position.y;
        return Mathf.Max(0, Mathf.FloorToInt(heightAboveBottom / LevelBandHeight));
    }

    /// <summary>Total invested tree levels required before nodes in the given band unlock.</summary>
    public int GetRequiredLevelForBand(int bandIndex) => Mathf.Max(0, bandIndex) * Mathf.Max(1, levelsPerBand);

    /// <summary>Total invested tree levels required before the given node position unlocks.</summary>
    public int GetRequiredLevelForPosition(Vector2 position) => GetRequiredLevelForBand(GetBandIndex(position));
}

//Trait nodes are the literal on-tree trait representations, containing data, position, cost, and connection info.
[System.Serializable]
public class TraitNode
{
    public string nodeID;
    public TraitData traitData;
    public Vector2 position;
    public List<string> connectedNodeIDs = new List<string>();
}



