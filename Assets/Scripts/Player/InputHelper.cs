using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Centralized input helper for the new Input System.
/// Provides easy access to common input checks across the entire project.
/// </summary>
public static class InputHelper
{
    // Cache the keyboard for performance
    private static Keyboard keyboard => Keyboard.current;
    private static Mouse mouse => Mouse.current;
    private static Gamepad gamepad => Gamepad.current;
    public static bool GetOffhandAbility => mouse != null && mouse.rightButton.wasPressedThisFrame;
    #region Keyboard Input
    
    /// <summary>
    /// Returns true during the frame the user starts pressing down the key.
    /// </summary>
    public static bool GetKeyDown(Key key)
    {
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }
    
    /// <summary>
    /// Returns true while the user holds down the key.
    /// </summary>
    public static bool GetKey(Key key)
    {
        return keyboard != null && keyboard[key].isPressed;
    }
    
    /// <summary>
    /// Returns true during the frame the user releases the key.
    /// </summary>
    public static bool GetKeyUp(Key key)
    {
        return keyboard != null && keyboard[key].wasReleasedThisFrame;
    }
    
    #endregion
    
    #region Mouse Input
    
    /// <summary>
    /// Returns true during the frame the user presses the given mouse button.
    /// 0 = left, 1 = right, 2 = middle
    /// </summary>
    public static bool GetMouseButtonDown(int button)
    {
        if (mouse == null) return false;
        
        return button switch
        {
            0 => mouse.leftButton.wasPressedThisFrame,
            1 => mouse.rightButton.wasPressedThisFrame,
            2 => mouse.middleButton.wasPressedThisFrame,
            _ => false
        };
    }
    
    /// <summary>
    /// Returns true while the user holds down the given mouse button.
    /// </summary>
    public static bool GetMouseButton(int button)
    {
        if (mouse == null) return false;
        
        return button switch
        {
            0 => mouse.leftButton.isPressed,
            1 => mouse.rightButton.isPressed,
            2 => mouse.middleButton.isPressed,
            _ => false
        };
    }
    
    /// <summary>
    /// Returns true during the frame the user releases the given mouse button.
    /// </summary>
    public static bool GetMouseButtonUp(int button)
    {
        if (mouse == null) return false;
        
        return button switch
        {
            0 => mouse.leftButton.wasReleasedThisFrame,
            1 => mouse.rightButton.wasReleasedThisFrame,
            2 => mouse.middleButton.wasReleasedThisFrame,
            _ => false
        };
    }
    
    /// <summary>
    /// Current mouse position in screen space.
    /// </summary>
    public static Vector2 MousePosition => mouse != null ? mouse.position.ReadValue() : Vector2.zero;
    
    /// <summary>
    /// Mouse scroll delta.
    /// </summary>
    public static Vector2 MouseScrollDelta => mouse != null ? mouse.scroll.ReadValue() : Vector2.zero;
    
    #endregion
    
    #region Gamepad Input
    
    /// <summary>
    /// Returns true if a gamepad is connected.
    /// </summary>
    public static bool IsGamepadConnected => gamepad != null;
    
    /// <summary>
    /// Returns true during the frame the user presses the gamepad button.
    /// </summary>
    public static bool GetGamepadButtonDown(GamepadButton button)
    {
        if (gamepad == null) return false;
        
        return button switch
        {
            GamepadButton.South => gamepad.buttonSouth.wasPressedThisFrame,
            GamepadButton.East => gamepad.buttonEast.wasPressedThisFrame,
            GamepadButton.West => gamepad.buttonWest.wasPressedThisFrame,
            GamepadButton.North => gamepad.buttonNorth.wasPressedThisFrame,
            GamepadButton.LeftShoulder => gamepad.leftShoulder.wasPressedThisFrame,
            GamepadButton.RightShoulder => gamepad.rightShoulder.wasPressedThisFrame,
            GamepadButton.Start => gamepad.startButton.wasPressedThisFrame,
            GamepadButton.Select => gamepad.selectButton.wasPressedThisFrame,
            _ => false
        };
    }
    
    /// <summary>
    /// Left stick direction (normalized).
    /// </summary>
    public static Vector2 LeftStick => gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
    
    /// <summary>
    /// Right stick direction (normalized).
    /// </summary>
    public static Vector2 RightStick => gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;
    
    #endregion
    
    #region Common Key Shortcuts
    
    // Movement keys (WASD)
    public static bool GetMoveForward => GetKey(Key.W);
    public static bool GetMoveBack => GetKey(Key.S);
    public static bool GetMoveLeft => GetKey(Key.A);
    public static bool GetMoveRight => GetKey(Key.D);
    public static Vector2 GetMovementInput()
    {
        float horizontal = 0f;
        float vertical = 0f;
        
        if (GetMoveLeft) horizontal -= 1f;
        if (GetMoveRight) horizontal += 1f;
        if (GetMoveBack) vertical -= 1f;
        if (GetMoveForward) vertical += 1f;
        
        return new Vector2(horizontal, vertical).normalized;
    }
    
    // Common action keys
    public static bool GetJump => GetKeyDown(Key.Space);
    public static bool GetDash => GetKeyDown(Key.Space);
    public static bool GetInteract => GetKeyDown(Key.E);
    public static bool GetReload => GetKeyDown(Key.R);
    public static bool GetSprint => GetKey(Key.LeftShift);
    public static bool GetCrouch => GetKey(Key.LeftCtrl);

    /// <summary>
    /// Returns true the frame the ability button for this slot is pressed.
    /// This is a fallback for abilities that poll input directly (e.g., trait abilities).
    /// 
    /// Slot mapping:
    ///   Slot 0 = LMB (primary weapon)
    ///   Slot 1 = RMB (secondary weapon)
    ///   Slot 2 = Shift
    ///   Slot 3 = Q
    ///   Slot 4 = E
    ///   Slot 5 = R
    ///   Slot -1 = autocast (no manual input)
    /// </summary>
    public static bool GetAbilityButtonDown(int slot) => slot switch
    {
        -1 => false, // Autocast abilities don't use manual input
        0 => GetMouseButtonDown(0),
        1 => GetMouseButtonDown(1),
        2 => GetKeyDown(Key.LeftShift),
        3 => GetKeyDown(Key.Q),
        4 => GetKeyDown(Key.E),
        5 => GetKeyDown(Key.R),
        _ => false
    };

    /// <summary>
    /// Returns true while the ability button for this slot is held.
    /// </summary>
    public static bool IsAbilityButtonHeld(int slot) => slot switch
    {
        -1 => false, // Autocast abilities don't use manual input
        0 => GetMouseButton(0),
        1 => GetMouseButton(1),
        2 => GetKey(Key.LeftShift),
        3 => GetKey(Key.Q),
        4 => GetKey(Key.E),
        5 => GetKey(Key.R),
        _ => false
    };

    /// <summary>
    /// Gets the keybind display string for a given slot.
    /// Used for UI to show what key activates an ability.
    /// </summary>
    public static string GetKeybindForSlot(int slot) => slot switch
    {
        -1 => "Auto",
        0 => "LMB",
        1 => "RMB",
        2 => "Shift",
        3 => "Q",
        4 => "E",
        5 => "R",
        _ => "?"
    };
    
    // UI keys
    public static bool GetEscape => GetKeyDown(Key.Escape);
    public static bool GetTab => GetKeyDown(Key.Tab);
    public static bool GetInventory => GetKeyDown(Key.I);
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Checks if any key is currently pressed.
    /// </summary>
    public static bool AnyKey()
    {
        return keyboard != null && keyboard.anyKey.isPressed;
    }
    
    /// <summary>
    /// Checks if any key was pressed this frame.
    /// </summary>
    public static bool AnyKeyDown()
    {
        return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
    }
    
    /// <summary>
    /// Converts old Input.GetKey KeyCode to new Input System Key.
    /// </summary>
    public static Key KeyCodeToKey(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.A => Key.A,
            KeyCode.B => Key.B,
            KeyCode.C => Key.C,
            KeyCode.D => Key.D,
            KeyCode.E => Key.E,
            KeyCode.F => Key.F,
            KeyCode.G => Key.G,
            KeyCode.H => Key.H,
            KeyCode.I => Key.I,
            KeyCode.J => Key.J,
            KeyCode.K => Key.K,
            KeyCode.L => Key.L,
            KeyCode.M => Key.M,
            KeyCode.N => Key.N,
            KeyCode.O => Key.O,
            KeyCode.P => Key.P,
            KeyCode.Q => Key.Q,
            KeyCode.R => Key.R,
            KeyCode.S => Key.S,
            KeyCode.T => Key.T,
            KeyCode.U => Key.U,
            KeyCode.V => Key.V,
            KeyCode.W => Key.W,
            KeyCode.X => Key.X,
            KeyCode.Y => Key.Y,
            KeyCode.Z => Key.Z,
            KeyCode.Space => Key.Space,
            KeyCode.Return => Key.Enter,
            KeyCode.Escape => Key.Escape,
            KeyCode.LeftShift => Key.LeftShift,
            KeyCode.RightShift => Key.RightShift,
            KeyCode.LeftControl => Key.LeftCtrl,
            KeyCode.RightControl => Key.RightCtrl,
            KeyCode.LeftAlt => Key.LeftAlt,
            KeyCode.RightAlt => Key.RightAlt,
            KeyCode.Tab => Key.Tab,
            KeyCode.Alpha0 => Key.Digit0,
            KeyCode.Alpha1 => Key.Digit1,
            KeyCode.Alpha2 => Key.Digit2,
            KeyCode.Alpha3 => Key.Digit3,
            KeyCode.Alpha4 => Key.Digit4,
            KeyCode.Alpha5 => Key.Digit5,
            KeyCode.Alpha6 => Key.Digit6,
            KeyCode.Alpha7 => Key.Digit7,
            KeyCode.Alpha8 => Key.Digit8,
            KeyCode.Alpha9 => Key.Digit9,
            _ => Key.None
        };
    }
    
    #endregion
}

/// <summary>
/// Enum for common gamepad buttons.
/// </summary>
public enum GamepadButton
{
    South,      // A on Xbox, Cross on PlayStation
    East,       // B on Xbox, Circle on PlayStation
    West,       // X on Xbox, Square on PlayStation
    North,      // Y on Xbox, Triangle on PlayStation
    LeftShoulder,
    RightShoulder,
    Start,
    Select
}