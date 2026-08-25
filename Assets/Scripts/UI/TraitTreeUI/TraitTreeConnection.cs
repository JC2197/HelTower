using UnityEngine;

/// <summary>
/// Represents a visual connection between trait tree nodes.
/// Stores the connection type and placement information for the trait tree.
/// </summary>
[System.Serializable]
public class TraitTreeConnection
{
    [Header("Connection Identity")]
    [Tooltip("Unique ID for this connection")]
    public string connectionID;
    
    [Header("Visual Settings")]
    [Tooltip("Scale multiplier for this connection")]
    public float scale = 1f;
    
    [Header("Connected Nodes")]
    [Tooltip("Node IDs this connection comes FROM (prerequisites)")]
    public string[] fromNodeIDs = new string[0];
    
    [Tooltip("Node IDs this connection goes TO (dependents)")]
    public string[] toNodeIDs = new string[0];

    [Header("Bubble Directions")]
    [Tooltip("Which bubble on the FROM node this connection leaves from. 0=N 1=S 2=E 3=W -1=center")]
    public int fromBubbleDir = -1;
    [Tooltip("Which bubble on the TO node this connection arrives at. 0=N 1=S 2=E 3=W -1=center")]
    public int toBubbleDir = -1;

    [Header("Path Style")]
    [Tooltip("Pixels to shave off each leg at the corner. 0 = sharp right angle, higher values = rounder corner.")]
    [Min(0f)]
    public float curveAmount = 4f;

    [Tooltip("Pixel width of the rendered path.")]
    [Min(1)]
    public int lineWidth = 1;

    [Tooltip("Base colour of the rendered path. Tinted by activation state at runtime.")]
    public Color lineColor = Color.white;

    [Header("Drawn Path")]
    [Tooltip("When true, the pixel-painted path below is used instead of the auto-generated curve.")]
    public bool useDrawnPath = false;

    [Tooltip("Hand-painted pixels for this connection. Edit via 'Draw Connection' in the Tree Editor.")]
    public System.Collections.Generic.List<ConnectionPixel> paintedPixels
        = new System.Collections.Generic.List<ConnectionPixel>();
}

/// <summary>One painted pixel on a connection's hand-drawn path.</summary>
[System.Serializable]
public class ConnectionPixel
{
    public int x;
    public int y;
    public Color color;

    public ConnectionPixel(int x, int y, Color color)
    {
        this.x = x; this.y = y; this.color = color;
    }
}
