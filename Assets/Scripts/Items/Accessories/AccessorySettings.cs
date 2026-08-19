using UnityEngine;

/// <summary>
/// Runtime settings for a single equipped accessory: the spawn assets plus the shared
/// AccessoryPositioningData authored on AccessoryConfig.
/// </summary>
[System.Serializable]
public class AccessorySettings
{
    public GameObject accessoryPrefab;

    [Tooltip("Optional animator controller override applied to the spawned accessory")]
    public RuntimeAnimatorController animatorController;

    public AccessoryPositioningData positioning = new AccessoryPositioningData();

    // Pass-throughs so callers read settings directly instead of reaching into positioning.
    public float aimingRadius => positioning.aimingRadius;

    public Vector2 northEastOffset => positioning.northEastOffset;
    public Vector2 northWestOffset => positioning.northWestOffset;
    public Vector2 southEastOffset => positioning.southEastOffset;
    public Vector2 southWestOffset => positioning.southWestOffset;

    public bool lockTo2Directions => positioning.lockTo2Directions;
    public bool flipAccessoryOnTurn => positioning.flipAccessoryOnTurn;
    public bool flipAccessoryOnYAxis => positioning.flipAccessoryOnYAxis;
    public bool flipAccessoryOnXAxis => positioning.flipAccessoryOnXAxis;

    public bool AccessoryBehindOnNE => positioning.AccessoryBehindOnNE;
    public bool AccessoryBehindOnNW => positioning.AccessoryBehindOnNW;
    public bool AccessoryBehindOnSE => positioning.AccessoryBehindOnSE;
    public bool AccessoryBehindOnSW => positioning.AccessoryBehindOnSW;

    public bool handBehindOnNE => positioning.handBehindOnNE;
    public bool handBehindOnNW => positioning.handBehindOnNW;
    public bool handBehindOnSE => positioning.handBehindOnSE;
    public bool handBehindOnSW => positioning.handBehindOnSW;
    public float handRotationOffset => positioning.handRotationOffset;
}
