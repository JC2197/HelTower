using UnityEngine;

/// <summary>
/// Static utility for drawing pixel-based connection paths onto a Texture2D colour array.
///
/// The path interpolates between two extremes controlled by <c>curveAmount</c>:
///   0 = right-angle L-shape (horizontal segment then vertical segment)
///   1 = smooth S-curve (cubic Bezier)
/// Intermediate values produce rounded / chamfered corners.
/// </summary>
public static class PixelConnectionDrawer
{
    // Texture-space exit/entry directions per bubble index (0=N 1=S 2=E 3=W).
    // Screen Y is down; texture Y is up — so screen N(-Y) maps to texture +Y.
    private static readonly Vector2[] s_BubbleTexDir =
    {
        new Vector2( 0,  1),  // 0 = N  (screen up   = texture +Y)
        new Vector2( 0, -1),  // 1 = S  (screen down  = texture -Y)
        new Vector2( 1,  0),  // 2 = E
        new Vector2(-1,  0),  // 3 = W
    };

    /// <summary>
    /// Draw a connection from <paramref name="texA"/> to <paramref name="texB"/> in texture-pixel space.
    /// </summary>
    /// <param name="pixels">Flat row-major colour array (index = y * texWidth + x).</param>
    /// <param name="texWidth">Texture width in pixels.</param>
    /// <param name="texHeight">Texture height in pixels.</param>
    /// <param name="texA">Start point (Y-up, same convention as Texture2D).</param>
    /// <param name="texB">End point.</param>
    /// <param name="curveAmount">0 = right-angle · 1 = smooth S-curve (ignored when bubble dirs are set).</param>
    /// <param name="lineWidth">Path thickness in pixels.</param>
    /// <param name="color">Colour to paint.</param>
    /// <param name="fromBubbleDir">Bubble direction (0-3) the line exits from, or -1 for auto.</param>
    /// <param name="toBubbleDir">Bubble direction (0-3) the line arrives at, or -1 for auto.</param>
    public static void DrawConnection(
        Color32[] pixels, int texWidth, int texHeight,
        Vector2Int texA, Vector2Int texB,
        float curveAmount, int lineWidth, Color32 color,
        int fromBubbleDir = -1, int toBubbleDir = -1)
    {
        int dx = texB.x - texA.x;
        int dy = texB.y - texA.y;

        // Enough steps so no pixel is skipped along the longest dimension.
        int steps = Mathf.Max(Mathf.Abs(dx) + Mathf.Abs(dy), 2) * 2;

        Vector2 p0 = new Vector2(texA.x, texA.y);
        Vector2 p3 = new Vector2(texB.x, texB.y);
        Vector2 p1, p2;

        if (fromBubbleDir >= 0 || toBubbleDir >= 0)
        {
            bool fromIsVert = fromBubbleDir == 0 || fromBubbleDir == 1; // N or S
            bool toIsVert   = toBubbleDir   == 0 || toBubbleDir   == 1;

            Vector2[] pts;

            if ((fromBubbleDir == 0 || fromBubbleDir == 1) &&
                (toBubbleDir   == 0 || toBubbleDir   == 1))
            {
                // V→V: two corners with horizontal bridge at the midpoint Y.
                float midY = (p0.y + p3.y) * 0.5f;
                pts = new Vector2[] { p0, new Vector2(p0.x, midY), new Vector2(p3.x, midY), p3 };
            }
            else if ((fromBubbleDir == 2 || fromBubbleDir == 3) &&
                     (toBubbleDir   == 2 || toBubbleDir   == 3))
            {
                // H→H: two corners with vertical bridge at the midpoint X.
                float midX = (p0.x + p3.x) * 0.5f;
                pts = new Vector2[] { p0, new Vector2(midX, p0.y), new Vector2(midX, p3.y), p3 };
            }
            else
            {
                // V→H or H→V (or one side unset): single corner.
                bool verticalFirst = fromBubbleDir >= 0 ? fromIsVert : !toIsVert;
                Vector2 corner = verticalFirst
                    ? new Vector2(p0.x, p3.y)
                    : new Vector2(p3.x, p0.y);
                pts = new Vector2[] { p0, corner, p3 };
            }

            DrawChamferedPolyline(pixels, texWidth, texHeight, pts, curveAmount, lineWidth, color);
            return;
        }
        else
        {
            // ── Original L-bend logic ──────────────────────────────────────────
            // The L-bend corner (go horizontal first, then vertical).
            Vector2 corner = new Vector2(texB.x, texA.y);

            // P1 blends from the corner toward the horizontal mid-tangent exit.
            p1 = Vector2.Lerp(corner, new Vector2(texA.x + dx * 0.5f, texA.y), curveAmount);

            // P2 blends from the corner toward the vertical mid-tangent entry.
            p2 = Vector2.Lerp(corner, new Vector2(texB.x, texA.y + dy * 0.5f), curveAmount);
        }

        Vector2 prev = new Vector2(texA.x, texA.y);
        for (int i = 1; i <= steps; i++)
        {
            float t  = i / (float)steps;
            Vector2 pt = CubicBezier(new Vector2(texA.x, texA.y), p1, p2, new Vector2(texB.x, texB.y), t);

            DrawThickLine(
                pixels, texWidth, texHeight,
                new Vector2Int(Mathf.RoundToInt(prev.x), Mathf.RoundToInt(prev.y)),
                new Vector2Int(Mathf.RoundToInt(pt.x),   Mathf.RoundToInt(pt.y)),
                color, lineWidth);

            prev = pt;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a polyline through <paramref name="pts"/> with a quadratic-Bezier chamfer
    /// of <paramref name="chamferPx"/> pixels shaved from each end of every interior corner.
    /// </summary>
    private static void DrawChamferedPolyline(
        Color32[] pixels, int texWidth, int texHeight,
        Vector2[] pts, float chamferPx, int lineWidth, Color32 color)
    {
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 from   = pts[i];
            Vector2 to     = pts[i + 1];
            float   segLen = Vector2.Distance(from, to);
            Vector2 dir    = segLen > 0.001f ? (to - from) / segLen : Vector2.zero;

            // First segment: no leading shave. Last segment: no trailing shave.
            float startShave = i == 0               ? 0f : Mathf.Min(chamferPx, segLen);
            float endShave   = i == pts.Length - 2  ? 0f : Mathf.Min(chamferPx, segLen);

            Vector2 segStart = from + dir * startShave;
            Vector2 segEnd   = to   - dir * endShave;

            // Straight portion of this segment.
            if (Vector2.Distance(segStart, segEnd) > 0.5f)
                DrawThickLine(pixels, texWidth, texHeight, ToV2I(segStart), ToV2I(segEnd), color, lineWidth);

            // Quadratic Bezier chamfer at the trailing corner (into the next segment).
            if (i < pts.Length - 2)
            {
                Vector2 nextTo     = pts[i + 2];
                float   nextLen    = Vector2.Distance(to, nextTo);
                Vector2 nextDir    = nextLen > 0.001f ? (nextTo - to) / nextLen : Vector2.zero;
                Vector2 chamferEnd = to + nextDir * Mathf.Min(chamferPx, nextLen);

                int steps = Mathf.Max(Mathf.RoundToInt(Vector2.Distance(segEnd, chamferEnd)) * 2, 2);
                Vector2 qPrev = segEnd;
                for (int j = 1; j <= steps; j++)
                {
                    float   t   = j / (float)steps;
                    Vector2 qPt = QuadraticBezier(segEnd, to, chamferEnd, t);
                    DrawThickLine(pixels, texWidth, texHeight, ToV2I(qPrev), ToV2I(qPt), color, lineWidth);
                    qPrev = qPt;
                }
            }
        }
    }

    private static Vector2Int ToV2I(Vector2 v) =>
        new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));

    private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0
             + 3f * u * u * t * p1
             + 3f * u * t * t * p2
             + t * t * t * p3;
    }

    private static void DrawThickLine(
        Color32[] pixels, int texWidth, int texHeight,
        Vector2Int a, Vector2Int b, Color32 color, int thickness)
    {
        int half     = thickness / 2;
        int threshSq = half * half + half; // slight circular cross-section

        for (int ox = -half; ox <= half; ox++)
        for (int oy = -half; oy <= half; oy++)
        {
            if (ox * ox + oy * oy > threshSq) continue;
            DrawLine(pixels, texWidth, texHeight,
                a + new Vector2Int(ox, oy),
                b + new Vector2Int(ox, oy),
                color);
        }
    }

    private static void DrawLine(
        Color32[] pixels, int texWidth, int texHeight,
        Vector2Int a, Vector2Int b, Color32 color)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            SetPixel(pixels, texWidth, texHeight, x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { if (x0 == x1) break; err += dy; x0 += sx; }
            if (e2 <= dx) { if (y0 == y1) break; err += dx; y0 += sy; }
        }
    }

    private static void SetPixel(Color32[] pixels, int texWidth, int texHeight, int x, int y, Color32 color)
    {
        if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) return;
        pixels[y * texWidth + x] = color;
    }
}
