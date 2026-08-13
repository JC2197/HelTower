using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for the shared <see cref="HitboxConfig"/> block used by every
/// hitbox-based sub-ability (melee, projectile, area, explosion, aura).
/// Hides <c>percentWeaponDamage</c> unless <c>useWeaponDamage</c> is enabled.
/// </summary>
[CustomPropertyDrawer(typeof(HitboxConfig))]
public class HitboxConfigDrawer : PropertyDrawer
{
    private static readonly string[] AlwaysFields =
    {
        "prefab", "scaleX", "scaleY", "hitLayers", "positiveHitLayers",
        "damage", "damageTypeName", "useWeaponDamage"
    };

    private static readonly string[] TrailingFields =
    {
        "lifeSteal", "knockback", "pull", "onHitEffects", "positiveHealing", "onHitBuffEffects", "effects"
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            float yPos = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            foreach (string field in AlwaysFields)
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative(field), position, yPos, true);

            if (property.FindPropertyRelative("useWeaponDamage").boolValue)
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("percentWeaponDamage"), position, yPos);

            foreach (string field in TrailingFields)
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative(field), position, yPos, true);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        foreach (string field in AlwaysFields)
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + EditorGUIUtility.standardVerticalSpacing;

        if (property.FindPropertyRelative("useWeaponDamage").boolValue)
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("percentWeaponDamage")) + EditorGUIUtility.standardVerticalSpacing;

        foreach (string field in TrailingFields)
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + EditorGUIUtility.standardVerticalSpacing;

        return height;
    }
}
