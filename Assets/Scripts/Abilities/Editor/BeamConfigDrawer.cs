using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for BeamAbilityConfig.
/// </summary>
[CustomPropertyDrawer(typeof(BeamAbilityConfig))]
public class BeamAbilityConfigDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float yPos = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Rendering
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("beamRendererPrefab"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("targetingMode"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("fallbackToCursorWhenNoEnemy"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("singleShotDuration"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("beamWidth"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("beamColor"), position, yPos);

            // Beam behavior
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("maxBeamDistance"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("beamAmount"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("multiBeamAngle"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("chain"), position, yPos);
            SerializedProperty chain = property.FindPropertyRelative("chain");
            if (chain != null && chain.boolValue)
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("chainAmount"), position, yPos);

            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("value"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("hitsPerSecond"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("damageTypeName"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("hitLayers"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("onHitEffects"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("lifeSteal"), position, yPos, true);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("canHeal"), position, yPos);
            SerializedProperty canHeal = property.FindPropertyRelative("canHeal");
            if (canHeal != null && canHeal.boolValue)
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("healTargets"), position, yPos);

            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("canHoldToFire"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("channelCostPerSecond"), position, yPos);

            // Tracking
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("trackingRadius"), position, yPos);

            // Muzzle effect
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("muzzleFlashPrefab"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("enableMuzzleLight"), position, yPos);
            SerializedProperty enableMuzzleLight = property.FindPropertyRelative("enableMuzzleLight");
            if (enableMuzzleLight.boolValue)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("muzzleLightColor"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("muzzleLightIntensity"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("muzzleLightRange"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("muzzleLightDuration"), position, yPos);
            }

            // Impact & audio
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("impactEffectPrefab"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("impactAnimationName"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("impactParticlePrefab"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("beamSound"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("impactSound"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("hitFlashColor"), position, yPos);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        height += GetPropertyHeight(property, "beamRendererPrefab");
        height += GetPropertyHeight(property, "targetingMode");
        height += GetPropertyHeight(property, "fallbackToCursorWhenNoEnemy");
        height += GetPropertyHeight(property, "singleShotDuration");
        height += GetPropertyHeight(property, "beamWidth");
        height += GetPropertyHeight(property, "beamColor");
        height += GetPropertyHeight(property, "maxBeamDistance");
        height += GetPropertyHeight(property, "beamAmount");
        height += GetPropertyHeight(property, "multiBeamAngle");
        height += GetPropertyHeight(property, "chain");
        SerializedProperty chain = property.FindPropertyRelative("chain");
        if (chain != null && chain.boolValue)
            height += GetPropertyHeight(property, "chainAmount");

        height += GetPropertyHeight(property, "value");
        height += GetPropertyHeight(property, "hitsPerSecond");
        height += GetPropertyHeight(property, "damageTypeName");
        height += GetPropertyHeight(property, "hitLayers");
        height += GetPropertyHeight(property, "onHitEffects");
        height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("lifeSteal"), true) + EditorGUIUtility.standardVerticalSpacing;
        height += GetPropertyHeight(property, "canHeal");
        SerializedProperty canHeal = property.FindPropertyRelative("canHeal");
        if (canHeal != null && canHeal.boolValue)
            height += GetPropertyHeight(property, "healTargets");

        height += GetPropertyHeight(property, "canHoldToFire");
        height += GetPropertyHeight(property, "channelCostPerSecond");
        height += GetPropertyHeight(property, "trackingRadius");
        height += GetPropertyHeight(property, "muzzleFlashPrefab");
        height += GetPropertyHeight(property, "enableMuzzleLight");
        SerializedProperty enableMuzzleLight = property.FindPropertyRelative("enableMuzzleLight");
        if (enableMuzzleLight != null && enableMuzzleLight.boolValue)
        {
            height += GetPropertyHeight(property, "muzzleLightColor");
            height += GetPropertyHeight(property, "muzzleLightIntensity");
            height += GetPropertyHeight(property, "muzzleLightRange");
            height += GetPropertyHeight(property, "muzzleLightDuration");
        }

        height += GetPropertyHeight(property, "impactEffectPrefab");
        height += GetPropertyHeight(property, "impactAnimationName");
        height += GetPropertyHeight(property, "impactParticlePrefab");
        height += GetPropertyHeight(property, "beamSound");
        height += GetPropertyHeight(property, "impactSound");
        height += GetPropertyHeight(property, "hitFlashColor");

        return height;
    }

    private float GetPropertyHeight(SerializedProperty property, string fieldName)
    {
        SerializedProperty prop = property.FindPropertyRelative(fieldName);
        if (prop == null) return 0f;
        return EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
    }
}
