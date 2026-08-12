using UnityEngine;

/// <summary>
/// Reusable obstacle-separation steering component.
/// Add to any GameObject that needs to navigate around obstacles.
/// Call Initialize() once with the layer mask and avoidance strength from your config,
/// then call GetSteeringDirection(target) each frame (or from FixedUpdate) to obtain
/// the corrected movement direction.
/// </summary>
public class AIPathfinding : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Configuration
    // -------------------------------------------------------------------------

    [Tooltip("Layers the sensor treats as obstacles. Set to walls/terrain only — exclude enemies and players.")]
    public LayerMask obstacleLayers = -1;

    [Tooltip("How strongly obstacles deflect movement (5 = gentle, 50 = aggressive avoidance).")]
    [Range(5f, 50f)]
    public float avoidanceStrength = 25f;

    [Tooltip("Radius of the obstacle sensor circle in world units.")]
    [Min(0.1f)]
    public float sensorRadius = 4.5f;

    [Tooltip("Recalculate steering every N frames. Higher = cheaper, slightly less reactive.")]
    [Range(1, 6)]
    public int updateInterval = 3;

    [Tooltip("Draw debug rays in the Scene view (preferred = yellow, steered = green).")]
    public bool debugDraw = false;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    private readonly Collider2D[] _buffer = new Collider2D[16];
    private ContactFilter2D _filter;
    private Vector2 _cached;
    private int _frameCounter;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call once after the host object is created to configure the sensor.
    /// </summary>
    public void Initialize(LayerMask layers, float strength, float radius = 4.5f, bool debug = false)
    {
        obstacleLayers = layers;
        avoidanceStrength = strength;
        sensorRadius = radius;
        debugDraw = debug;

        _filter.SetLayerMask(layers);
        _filter.useTriggers = false;

        // Stagger recalculation across instances so no single frame spikes.
        _frameCounter = Mathf.Abs(GetInstanceID()) % updateInterval;
    }

    /// <summary>
    /// Returns the steered movement direction toward <paramref name="targetPosition"/>,
    /// deflected away from nearby obstacles. Result is normalised and cached between
    /// recalculation intervals.
    /// </summary>
    public Vector2 GetSteeringDirection(Vector3 targetPosition)
    {
        Vector2 preferred = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        return Steer(preferred);
    }

    /// <summary>
    /// Returns the steered direction given an already-computed preferred direction
    /// (e.g. "away from target" for retreat / kite behaviour).
    /// </summary>
    public Vector2 GetSteeringDirectionFromPreferred(Vector2 preferredDirection)
    {
        return Steer(preferredDirection);
    }

    // -------------------------------------------------------------------------
    // Core steering
    // -------------------------------------------------------------------------

    private Vector2 Steer(Vector2 preferredDirection)
    {
        if (preferredDirection == Vector2.zero) return Vector2.zero;

        _frameCounter++;
        if (_frameCounter < updateInterval)
            return _cached.sqrMagnitude > 0f ? _cached : preferredDirection;
        _frameCounter = 0;

        int count = Physics2D.OverlapCircle(transform.position, sensorRadius, _filter, _buffer);

        Vector2 separation = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Collider2D col = _buffer[i];
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            Vector2 closest = col.ClosestPoint(transform.position);
            Vector2 away = (Vector2)transform.position - closest;
            float dist = away.magnitude;
            if (dist < 0.001f) { away = -preferredDirection; dist = 0.001f; }

            float strength = avoidanceStrength * 0.04f * Mathf.Max(0f, 1f - dist / sensorRadius);
            separation += away.normalized * strength;
        }

        Vector2 desired = preferredDirection + separation;
        _cached = desired.sqrMagnitude > 0.0001f ? desired.normalized : preferredDirection;

        if (debugDraw)
        {
            Debug.DrawRay(transform.position, preferredDirection, Color.yellow, 0.1f);
            Debug.DrawRay(transform.position, _cached, Color.green, 0.1f);
        }

        return _cached;
    }

    private void Awake()
    {
        // Ensure filter is set even when Initialize() hasn't been called yet
        // (e.g. if placed directly in the Inspector and not code-initialized).
        _filter.SetLayerMask(obstacleLayers);
        _filter.useTriggers = false;
        _frameCounter = Mathf.Abs(GetInstanceID()) % updateInterval;
    }
}
