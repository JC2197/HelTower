using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for MeleeConfig
/// </summary>
[CustomPropertyDrawer(typeof(MeleeConfig))]
public class MeleeConfigDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            float yPos = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Shared hitbox (prefab, scale, hit layers, damage, effects, knockback, pull, life steal, on-hit effects)
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("hitbox"), position, yPos, true);

            // MeleeFX specific
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("meleeFXRadiusDistance"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("meleeFXSpeed"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("allowMultiHit"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("flipMeleeFX"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("flipMeleeFXY"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("stickToCharacter"), position, yPos);

            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("meleeSound"), position, yPos);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        // Shared hitbox
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("hitbox"), true) + EditorGUIUtility.standardVerticalSpacing;

        // MeleeFX specific
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("meleeFXRadiusDistance")) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("meleeFXSpeed")) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("allowMultiHit")) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("flipMeleeFX")) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("flipMeleeFXY")) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("stickToCharacter")) + EditorGUIUtility.standardVerticalSpacing;

        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("meleeSound")) + EditorGUIUtility.standardVerticalSpacing;

        return height;
    }
}
