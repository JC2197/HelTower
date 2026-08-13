using UnityEditor;
using UnityEngine;

/// <summary>
/// Static helper methods for drawing shared AuraConfig/AreaConfig sections.
/// The AuraConfig class no longer exists; these helpers are reused by AreaConfigDrawer.
/// </summary>
public static class AuraConfigDrawer
{
    public static float DrawVisualPrefabFields(SerializedProperty property, Rect position, float yPos)
    {
        yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spellPrefab"), position, yPos);
        return yPos;
    }

    public static float GetVisualPrefabHeight(SerializedProperty property)
    {
        float height = 0f;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("spellPrefab")) + EditorGUIUtility.standardVerticalSpacing;
        return height;
    }

}
