using UnityEngine;
using System;

/// <summary>
/// All positioning, flipping, and sorting data for a Accessory.
/// Shared via AccessoryTypeConfig and optionally overridden per AccessoryConfig.
/// </summary>
[Serializable]
public class AccessoryPositioningData
{
    [Tooltip("Distance from player center to accessory pivot point (0 = mount directly on the offset)")]
    public float aimingRadius = 0f;

    [Tooltip("Accessory offset when facing North East")]
    public Vector2 northEastOffset = Vector2.zero;
    [Tooltip("Accessory offset when facing North West")]
    public Vector2 northWestOffset = Vector2.zero;
    [Tooltip("Accessory offset when facing South East")]
    public Vector2 southEastOffset = Vector2.zero;
    [Tooltip("Accessory offset when facing South West")]
    public Vector2 southWestOffset = Vector2.zero;

    [Header("Accessory Sorting")]
    [Tooltip("Accessory renders behind player when moving NorthEast")]
    public bool AccessoryBehindOnNE = false;
    [Tooltip("Accessory renders behind player when moving NorthWest")]
    public bool AccessoryBehindOnNW = false;
    [Tooltip("Accessory renders behind player when moving SouthEast")]
    public bool AccessoryBehindOnSE = false;
    [Tooltip("Accessory renders behind player when moving SouthWest")]
    public bool AccessoryBehindOnSW = false;

    
    [Header("Aiming & Flipping")]
    [Tooltip("Lock aiming to 2 cardinal directions (E, W) instead of 360 degrees")]
    public bool lockTo2Directions = false;
    [Tooltip("Enable Accessory sprite flipping")]
    public bool flipAccessoryOnTurn = false;
    [Tooltip("Flip on Y axis when facing left")]
    public bool flipAccessoryOnYAxis = false;
    [Tooltip("Flip on X axis when facing left")]
    public bool flipAccessoryOnXAxis = false;

    [Header("Hand Sorting")]
    public bool handBehindOnNE = false;
    public bool handBehindOnNW = false;
    public bool handBehindOnSE = false;
    public bool handBehindOnSW = false;

    [Tooltip("HandHolder rotation offset relative to the accessory")]
    public float handRotationOffset = 0f;
}
