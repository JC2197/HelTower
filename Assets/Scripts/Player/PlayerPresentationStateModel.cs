using System;
using UnityEngine;

[Flags]
public enum PresentationFlags : byte
{
    None = 0,
    FacingLeft = 1 << 0,
    DirectionLocked = 1 << 1,
    MainhandLocked = 1 << 2,
    OffhandLocked = 1 << 3
}
[Serializable]
public class PresentationStateModel
{
    public float aimAngleDeg;
    public bool facingLeft;
    public bool directionLocked;
    public float lockedAngleDeg;

    public bool isMainhandLocked;
    public bool isOffhandLocked;

    // Optional stable identifier. Use weaponName now, move to ushort id later if desired.
    public string weaponName;

    // Monotonic sequence for out-of-order protection.
    public uint sequence;

    public void NormalizeAngles()
    {
        aimAngleDeg = NormalizeAngle(aimAngleDeg);
        lockedAngleDeg = NormalizeAngle(lockedAngleDeg);
    }

    public static float NormalizeAngle(float angle)
    {
        float result = angle % 360f;
        if (result < 0f) result += 360f;
        return result;
    }
}