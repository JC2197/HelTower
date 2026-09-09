#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;


public static class DrawerUtil
{
    // A null property means the field was renamed or removed. Drawing it would throw mid-layout
    // and unbalance the GUI stack, so the error would surface in an unrelated drawer instead.
    public static float DrawPropertyAndAdvanceYPos(SerializedProperty property, Rect position, float yPos)
    {
        if (property == null)
            return yPos;

        EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(property)), property);
        yPos += EditorGUI.GetPropertyHeight(property) + EditorGUIUtility.standardVerticalSpacing;
        return yPos;
    }

    // Overload for properties that need includeChildren parameter (lists, nested objects)
    public static float DrawPropertyAndAdvanceYPos(SerializedProperty property, Rect position, float yPos, bool includeChildren)
    {
        if (property == null)
            return yPos;

        EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(property, includeChildren)), property, includeChildren);
        yPos += EditorGUI.GetPropertyHeight(property, includeChildren) + EditorGUIUtility.standardVerticalSpacing;
        return yPos;
    }

    /// <summary>Height contribution of a property, or 0 when the field no longer exists.</summary>
    public static float SafePropertyHeight(SerializedProperty property, bool includeChildren = true)
    {
        return property == null
            ? 0f
            : EditorGUI.GetPropertyHeight(property, includeChildren) + EditorGUIUtility.standardVerticalSpacing;
    }
}
#endif


