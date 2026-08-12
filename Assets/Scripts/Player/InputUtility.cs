using UnityEngine;
using UnityEngine.InputSystem;

public static class InputUtility
{
    /// <summary>
    /// Gets the current mouse position in world space (Z = 0)
    /// </summary>
    public static Vector3 GetMouseWorldPosition()
    {
        if (Mouse.current == null || Camera.main == null)
            return Vector3.zero;
        
        Vector2 mousePos = Mouse.current.position.ReadValue();
        
        // Check if mouse is within screen bounds
        if (mousePos.x < 0 || mousePos.x > Screen.width || mousePos.y < 0 || mousePos.y > Screen.height)
            return Vector3.zero;
        
        Vector3 screenPos = new Vector3(mousePos.x, mousePos.y, 0f);
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        worldPos.z = 0f;
        return worldPos;
    }
    
    /// <summary>
    /// Gets the direction from a position to the mouse cursor
    /// </summary>
    public static Vector2 GetDirectionToMouse(Vector3 fromPosition)
    {
        return (GetMouseWorldPosition() - fromPosition).normalized;
    }
    
    /// <summary>
    /// Gets the angle in degrees from a position to the mouse cursor
    /// </summary>
    public static float GetAngleToMouse(Vector3 fromPosition)
    {
        Vector2 direction = GetDirectionToMouse(fromPosition);
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }
    
    /// <summary>
    /// Gets the distance from a position to the mouse cursor
    /// </summary>
    public static float GetDistanceToMouse(Vector3 fromPosition)
    {
        return Vector2.Distance(fromPosition, GetMouseWorldPosition());
    }
    
    /// <summary>
    /// Gets the mouse position in world space, clamped to a maximum range from the origin
    /// </summary>
    public static Vector3 GetMouseWorldPositionClamped(Vector3 fromPosition, float maxRange)
    {
        Vector3 mousePos = GetMouseWorldPosition();
        float distance = Vector2.Distance(fromPosition, mousePos);
        
        if (distance > maxRange)
        {
            Vector2 direction = (mousePos - fromPosition).normalized;
            mousePos = (Vector2)fromPosition + direction * maxRange;
        }
        
        return mousePos;
    }
    
    /// <summary>
    /// Checks if the mouse is within a specific range from a position
    /// </summary>
    public static bool IsMouseInRange(Vector3 fromPosition, float range)
    {
        return GetDistanceToMouse(fromPosition) <= range;
    }
}