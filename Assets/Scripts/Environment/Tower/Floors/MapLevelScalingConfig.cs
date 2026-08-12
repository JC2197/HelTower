using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Configures how enemy power scales with map level.
/// </summary>
[CreateAssetMenu(fileName = "MapLevelScalingConfig", menuName = "Arenas/Map Level Scaling Config")]
public class MapLevelScalingConfig : ScriptableObject
{
    [Header("Fallback Multipliers")]
    [Tooltip("Applied when a map level has no explicit entry. Example: 0.5 means +50% health per level.")]
    [Min(0f)] public float fallbackHealthIncreasePerLevel = 0.5f;

    [Tooltip("Applied when a map level has no explicit entry. Example: 0.25 means +25% damage per level.")]
    [Min(0f)] public float fallbackDamageIncreasePerLevel = 0.25f;

    [Header("Per-Level Overrides")]
    [Tooltip("Optional explicit per-level scaling entries.")]
    public List<MapEnemyLevelScalingData> enemyLevelScaling = new List<MapEnemyLevelScalingData>();

    public float GetHealthMultiplier(int mapLevel)
    {
        int clampedLevel = Mathf.Max(1, mapLevel);
        return 1f + fallbackHealthIncreasePerLevel * (clampedLevel - 1);
    }

    public float GetDamageMultiplier(int mapLevel)
    {
        int clampedLevel = Mathf.Max(1, mapLevel);
        MapEnemyLevelScalingData entry = GetLevelEntry(clampedLevel);
        if (entry != null)
        {
            return Mathf.Max(0f, entry.enemyDamageMultiplier);
        }

        return 1f + fallbackDamageIncreasePerLevel * (clampedLevel - 1);
    }

    public MapEnemyLevelScalingData GetLevelEntry(int mapLevel)
    {
        int clampedLevel = Mathf.Max(1, mapLevel);
        for (int i = 0; i < enemyLevelScaling.Count; i++)
        {
            MapEnemyLevelScalingData entry = enemyLevelScaling[i];
            if (entry != null && entry.mapLevel == clampedLevel)
            {
                return entry;
            }
        }

        return null;
    }
}

[Serializable]
public class MapEnemyLevelScalingData
{
    [Tooltip("Map level this entry applies to.")]
    [Min(1)] public int mapLevel = 1;

    [Tooltip("Final multiplier to enemy outgoing damage at this level.")]
    [Min(0f)] public float enemyDamageMultiplier = 1f;

    [Tooltip("Stat modifiers applied to enemy runtime StatContainer at this level (MoveSpeed, MaxHealth, etc.).")]
    public List<StatModifier> enemyStatModifiers = new List<StatModifier>();

    [Tooltip("Future modifier hooks to toggle map-specific enemy mechanics (IDs resolved by a modifier system).")]
    public string[] enemyModifierIDs = new string[0];
}