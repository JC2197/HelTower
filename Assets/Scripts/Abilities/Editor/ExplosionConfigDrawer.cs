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
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("timeDelay"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("delayEffectPrefab"), position, yPos);
            SerializedProperty salvos = property.FindPropertyRelative("salvos");
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(salvos, position, yPos);
            if (salvos.boolValue)
            {
                SerializedProperty multiCastAmount = property.FindPropertyRelative("multiCastAmount");
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(multiCastAmount, position, yPos);
                SerializedProperty salvoAmount = property.FindPropertyRelative("salvoAmount");
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(salvoAmount, position, yPos);
                SerializedProperty salvoDelay = property.FindPropertyRelative("salvoDelay");
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(salvoDelay, position, yPos);
                SerializedProperty salvoOffset = property.FindPropertyRelative("salvoOffset");
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(salvoOffset, position, yPos);
                if (salvoOffset.boolValue)
                {
                    SerializedProperty salvoOffsetDistance = property.FindPropertyRelative("salvoOffsetDistance");
                    yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(salvoOffsetDistance, position, yPos);
                    SerializedProperty salvoOffsetTarget = property.FindPropertyRelative("salvoOffsetTarget");
                    yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(salvoOffsetTarget, position, yPos);
                    SerializedProperty salvoOffsetMouse = property.FindPropertyRelative("salvoOffsetMouse");
                    yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(salvoOffsetMouse, position, yPos);
                    SerializedProperty salvoRandom = property.FindPropertyRelative("salvoRandom");
                    yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(salvoRandom, position, yPos);
                    SerializedProperty salvoRadial = property.FindPropertyRelative("salvoRadial");
                    yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(salvoRadial, position, yPos);
                }
            }

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
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("timeDelay")) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("delayEffectPrefab")) + EditorGUIUtility.standardVerticalSpacing;
        
        // Activation
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("activationRange")) + EditorGUIUtility.standardVerticalSpacing;
        SerializedProperty salvos = property.FindPropertyRelative("salvos");
        height += EditorGUI.GetPropertyHeight(salvos) + EditorGUIUtility.standardVerticalSpacing;
        if (salvos.boolValue)
        {
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("salvoAmount")) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("multiCastAmount")) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("salvoDelay")) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("salvoOffset")) + EditorGUIUtility.standardVerticalSpacing;
            if (property.FindPropertyRelative("salvoOffset").boolValue)
            {
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("salvoOffsetDistance")) + EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("salvoOffsetMouse")) + EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("salvoOffsetTarget")) + EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("salvoRandom")) + EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("salvoRadial")) + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        return height;
    }
}
