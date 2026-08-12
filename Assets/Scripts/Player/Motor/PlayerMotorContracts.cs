using UnityEngine;


public enum CharacterFacingMode
{
    Aim,
    Movement
}

public interface ICharacterInputSource
{
    CharacterInputFrame CaptureInput();
}

public interface IAimProvider
{
    Vector2 GetAimWorldPosition();
}

public interface ICharacterMotor
{
    CharacterMotorState State { get; }
    void Simulate(in CharacterInputFrame input, float deltaTime);
    void Teleport(Vector2 worldPosition);
}

public readonly struct CharacterInputFrame
{
    public readonly Vector2 Move;
    public readonly Vector2 AimWorld;
    public readonly string[] PressedActionIds;
    public readonly string[] HeldActionIds;
    public readonly uint Tick;

    public CharacterInputFrame(Vector2 move, Vector2 aimWorld, string[] pressedActionIds, string[] heldActionIds, uint tick)
    {
        Move = Vector2.ClampMagnitude(move, 1f);
        AimWorld = aimWorld;
        PressedActionIds = pressedActionIds ?? System.Array.Empty<string>();
        HeldActionIds = heldActionIds ?? System.Array.Empty<string>();
        Tick = tick;
    }

    public bool WasPressed(string actionId)
    {
        return Contains(PressedActionIds, actionId);
    }

    public bool IsHeld(string actionId)
    {
        return Contains(HeldActionIds, actionId);
    }

    private static bool Contains(string[] actionIds, string actionId)
    {
        if (actionIds == null || actionIds.Length == 0 || string.IsNullOrWhiteSpace(actionId))
            return false;

        for (int i = 0; i < actionIds.Length; i++)
        {
            if (string.Equals(actionIds[i], actionId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public readonly struct CharacterMotorState
{
    public readonly Vector2 Position;
    public readonly Vector2 Velocity;
    public readonly float AimAngleDeg;
    public readonly bool FacingLeft;

    public CharacterMotorState(Vector2 position, Vector2 velocity, float aimAngleDeg, bool facingLeft)
    {
        Position = position;
        Velocity = velocity;
        AimAngleDeg = aimAngleDeg;
        FacingLeft = facingLeft;
    }
}

