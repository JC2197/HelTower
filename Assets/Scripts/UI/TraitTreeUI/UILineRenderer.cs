using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UI-compatible line renderer that works with Canvas Screen Space - Overlay mode.
/// Uses Unity's UI system (Graphic/CanvasRenderer) to draw lines.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UILineRenderer : Graphic
{
    [SerializeField] private float lineWidth = 3f;
    [SerializeField] private bool useGradient = true;
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.white;
    
    private Vector2[] points = new Vector2[2];
    
    /// <summary>
    /// Set the start and end points of the line in local space
    /// </summary>
    public void SetPositions(Vector2 start, Vector2 end)
    {
        points = new Vector2[] { start, end };
        SetVerticesDirty();
    }

    /// <summary>
    /// Set an arbitrary multi-point polyline in local space.
    /// </summary>
    public void SetPoints(IList<Vector2> pts)
    {
        if (pts == null || pts.Count < 2) return;
        points = new Vector2[pts.Count];
        for (int i = 0; i < pts.Count; i++) points[i] = pts[i];
        SetVerticesDirty();
    }
    
    /// <summary>
    /// Set line width
    /// </summary>
    public void SetWidth(float width)
    {
        lineWidth = width;
        SetVerticesDirty();
    }
    
    /// <summary>
    /// Set line colors (gradient from start to end)
    /// </summary>
    public void SetColors(Color start, Color end)
    {
        startColor = start;
        endColor = end;
        useGradient = true;
        SetVerticesDirty();
    }
    
    /// <summary>
    /// Set single color for entire line
    /// </summary>
    public void SetColor(Color col)
    {
        startColor = col;
        endColor = col;
        useGradient = false;
        color = col;
        SetVerticesDirty();
    }
    
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points == null || points.Length < 2)
            return;

        float half = lineWidth * 0.5f;
        int segCount = points.Length - 1;
        int vi = 0;

        for (int i = 0; i < segCount; i++)
        {
            Vector2 start = points[i];
            Vector2 end   = points[i + 1];
            Vector2 dir   = end - start;
            if (dir.sqrMagnitude < 1e-6f) continue;
            dir.Normalize();
            Vector2 perpendicular = new Vector2(-dir.y, dir.x) * half;

            Color cStart = useGradient ? Color.Lerp(startColor, endColor, i / (float)segCount) : color;
            Color cEnd   = useGradient ? Color.Lerp(startColor, endColor, (i + 1) / (float)segCount) : color;

            UIVertex vertex = UIVertex.simpleVert;

            vertex.position = start - perpendicular; vertex.color = cStart; vh.AddVert(vertex);
            vertex.position = start + perpendicular; vertex.color = cStart; vh.AddVert(vertex);
            vertex.position = end   + perpendicular; vertex.color = cEnd;   vh.AddVert(vertex);
            vertex.position = end   - perpendicular; vertex.color = cEnd;   vh.AddVert(vertex);

            vh.AddTriangle(vi, vi + 1, vi + 2);
            vh.AddTriangle(vi + 2, vi + 3, vi);
            vi += 4;
        }
    }
    
    /// <summary>
    /// Update the geometry (call after changing positions or colors)
    /// </summary>
    public new void UpdateGeometry()
    {
        SetVerticesDirty();
    }
}
