using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws an <see cref="EffectApplication"/> as a single row: effect asset, chance, and the
/// overrides that actually apply to the referenced asset's type.
/// </summary>
[CustomPropertyDrawer(typeof(EffectApplication))]
public class EffectApplicationDrawer : PropertyDrawer
{
    private const float Spacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty effect = property.FindPropertyRelative("effect");
        SerializedProperty chance = property.FindPropertyRelative("applicationChance");
        SerializedProperty durationOverride = property.FindPropertyRelative("durationOverride");
        SerializedProperty damageOverride = property.FindPropertyRelative("damageOverride");

        float line = EditorGUIUtility.singleLineHeight;
        Rect row = new Rect(position.x, position.y, position.width, line);

        EditorGUI.PropertyField(row, effect, new GUIContent(DescribeEffect(effect)));
        row.y += line + Spacing;

        EditorGUI.indentLevel++;
        EditorGUI.PropertyField(row, chance, new GUIContent("Chance"));
        row.y += line + Spacing;

        EditorGUI.PropertyField(row, durationOverride, new GUIContent("Duration Override", "0 = use the effect asset's own duration."));
        row.y += line + Spacing;

        if (IsDamageOverTime(effect))
        {
            EditorGUI.PropertyField(row, damageOverride, new GUIContent("Damage Override", "0 = use the effect asset's own damage per tick."));
        }
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int rows = IsDamageOverTime(property.FindPropertyRelative("effect")) ? 4 : 3;
        return rows * EditorGUIUtility.singleLineHeight + (rows - 1) * Spacing;
    }

    private static bool IsDamageOverTime(SerializedProperty effect)
    {
        return effect != null && effect.objectReferenceValue is DamageOverTimeConfig;
    }

    private static string DescribeEffect(SerializedProperty effect)
    {
        if (effect == null || effect.objectReferenceValue == null)
            return "Effect";

        return effect.objectReferenceValue is EffectConfig config && !string.IsNullOrWhiteSpace(config.effectName)
            ? config.effectName
            : effect.objectReferenceValue.name;
    }
}
