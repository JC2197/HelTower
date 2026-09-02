using UnityEngine;

/// <summary>
/// Telegraph shown during an ability's pre-cast. Spawned at the character root and oriented
/// toward the aim direction the ability will fire in — lets enemies (and players) signal an
/// incoming attack before it resolves. Mirrors the lightweight <see cref="TimedParticleSpawn"/>
/// pattern rather than the full <see cref="HitboxConfig"/> pipeline.
/// </summary>
[System.Serializable]
public class IndicatorConfig
{
    [Tooltip("Prefab instantiated at the character root during pre-cast, facing the aim direction.")]
    public GameObject prefab;

    [Tooltip("Delay after pre-cast begins before the indicator appears (seconds).")]
    public float spawnDelay = 0f;

    [Tooltip("How long the indicator stays alive (seconds). 0 = destroy automatically when pre-cast ends.")]
    public float duration = 0f;

    [Tooltip("Position offset from the character root.")]
    public Vector3 offset = Vector3.zero;
}
