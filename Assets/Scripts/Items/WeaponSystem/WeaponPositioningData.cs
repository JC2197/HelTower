using UnityEngine;
using System;

/// <summary>
/// All positioning, flipping, and sorting data for a weapon.
/// Shared via WeaponTypeConfig and optionally overridden per WeaponConfig.
/// </summary>
[Serializable]
public class WeaponPositioningData
{
    [Tooltip("Distance from player center to weapon pivot point")]
    public float aimingRadius = 0.3f;

    public bool isMainHand = true;
    public bool is2Handed = false;

    [Tooltip("Weapon offset when facing North East")]
    public Vector2 northEastOffset = Vector2.zero;
    [Tooltip("Weapon offset when facing North West")]
    public Vector2 northWestOffset = Vector2.zero;
    [Tooltip("Weapon offset when facing South East")]
    public Vector2 southEastOffset = Vector2.zero;
    [Tooltip("Weapon offset when facing South West")]
    public Vector2 southWestOffset = Vector2.zero;

    [Header("Weapon Sorting")]
    [Tooltip("Weapon renders behind player when moving NorthEast")]
    public bool weaponBehindOnNE = false;
    [Tooltip("Weapon renders behind player when moving NorthWest")]
    public bool weaponBehindOnNW = false;
    [Tooltip("Weapon renders behind player when moving SouthEast")]
    public bool weaponBehindOnSE = false;
    [Tooltip("Weapon renders behind player when moving SouthWest")]
    public bool weaponBehindOnSW = false;


    [Tooltip("Check this if this weapon IS an offhand weapon (disables offhand config field)")]
    public bool isOffhand = false;

    [Header("OffHand Offsets (only used if isOffhand = true)")]
    public Vector2 offhandNorthEastOffset = Vector2.zero;
    public Vector2 offhandNorthWestOffset = Vector2.zero;
    public Vector2 offhandSouthEastOffset = Vector2.zero;
    public Vector2 offhandSouthWestOffset = Vector2.zero;
    public bool offhandWeaponBehindOnNE = false;
    [Tooltip("Weapon renders behind player when moving NorthWest")]
    public bool offhandWeaponBehindOnNW = false;
    [Tooltip("Weapon renders behind player when moving SouthEast")]
    public bool offhandWeaponBehindOnSE = false;
    [Tooltip("Weapon renders behind player when moving SouthWest")]
    public bool offhandWeaponBehindOnSW = false;


    
    [Header("Aiming & Flipping")]
    [Tooltip("Lock aiming to 2 cardinal directions (E, W) instead of 360 degrees")]
    public bool lockTo2Directions = false;
    [Tooltip("Enable weapon sprite flipping")]
    public bool flipWeaponOnTurn = false;
    [Tooltip("Flip on Y axis when facing left")]
    public bool flipWeaponOnYAxis = false;
    [Tooltip("Flip on X axis when facing left")]
    public bool flipWeaponOnXAxis = false;



    [Header("Hand Sorting")]
    [Tooltip("HandHolder renders behind weapon when moving NorthEast")]
    public bool handBehindOnNE = false;
    [Tooltip("HandHolder renders behind weapon when moving NorthWest")]
    public bool handBehindOnNW = false;
    [Tooltip("HandHolder renders behind weapon when moving SouthEast")]
    public bool handBehindOnSE = false;
    [Tooltip("HandHolder renders behind weapon when moving SouthWest")]
    public bool handBehindOnSW = false;


    [Tooltip("HandHolder Rotation offset relative to the weapon")]
    public float handRotationOffset = 0f;
}
