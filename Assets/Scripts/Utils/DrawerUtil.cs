#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;


public static class DrawerUtil
{


    // Generic method to draw a property field and update yPos
    public static float DrawPropertyAndAdvanceYPos(SerializedProperty property, Rect position, float yPos)
    {
        EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(property)), property);
        yPos += EditorGUI.GetPropertyHeight(property) + EditorGUIUtility.standardVerticalSpacing;
        return yPos;
    }

    // Overload for properties that need includeChildren parameter (lists, nested objects)
    public static float DrawPropertyAndAdvanceYPos(SerializedProperty property, Rect position, float yPos, bool includeChildren)
    {
        EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(property, includeChildren)), property, includeChildren);
        yPos += EditorGUI.GetPropertyHeight(property, includeChildren) + EditorGUIUtility.standardVerticalSpacing;
        return yPos;
    }
}
#endif


