using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for ExplosionConfig to handle conditional field visibility
/// </summary>
[CustomPropertyDrawer(typeof(ExplosionConfig))]
public class ExplosionConfigDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw foldout
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            float yPos = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Hitbox (scale, hit layers, damage, weapon damage, knockback, pull, on-hit effects, life steal, hit feedback)
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("hitbox"), position, yPos, true);

            // Area Settings / Single-Target Mode
            SerializedProperty singleTargetMode = property.FindPropertyRelative("singleTargetMode");
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(singleTargetMode, position, yPos);
            if (singleTargetMode.boolValue)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("singleTargetSearchRadius"), position, yPos);
            }
            else
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("shape"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("dimensions"), position, yPos);
            }

            // Effects
            SerializedProperty timeDelay = property.FindPropertyRelative("timeDelay");
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(timeDelay, position, yPos);
            if (timeDelay.floatValue > 0f)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("delayEffectPrefab"), position, yPos);
            }
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("explosionEffectPrefab"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("explosionSound"), position, yPos);
            
            // Activation
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("activationRange"), position, yPos);

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

        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // Foldout

        // Hitbox
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("hitbox"), true) + EditorGUIUtility.standardVerticalSpacing;

        // Area Settings / Single-Target Mode
        SerializedProperty singleTargetMode = property.FindPropertyRelative("singleTargetMode");
        height += EditorGUI.GetPropertyHeight(singleTargetMode) + EditorGUIUtility.standardVerticalSpacing;
        if (singleTargetMode.boolValue)
        {
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("singleTargetSearchRadius")) + EditorGUIUtility.standardVerticalSpacing;
        }
        else
        {
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("shape")) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("dimensions")) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Effects
        SerializedProperty timeDelay = property.FindPropertyRelative("timeDelay");
        height += EditorGUI.GetPropertyHeight(timeDelay) + EditorGUIUtility.standardVerticalSpacing;
        if (timeDelay.floatValue > 0f)
        {
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("delayEffectPrefab")) + EditorGUIUtility.standardVerticalSpacing;
        }
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("explosionEffectPrefab")) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("explosionSound")) + EditorGUIUtility.standardVerticalSpacing;
        
        // Activation
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("activationRange")) + EditorGUIUtility.standardVerticalSpacing;

        return height;
    }
}
