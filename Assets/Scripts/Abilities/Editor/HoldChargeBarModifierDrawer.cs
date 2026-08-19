using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
[CustomPropertyDrawer(typeof(ChargeBarModifier))]
public class HoldChargeBarModifierDrawer : PropertyDrawer
{
    private static readonly GUIContent[] emptyContents = new GUIContent[] { new GUIContent("None") };
    private const int RowCount = 6;
    private const float TopPadding = 6f;
    private const float BottomPadding = 10f;
    private const float ExtraRowSpacing = 4f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float rowSpacing = EditorGUIUtility.standardVerticalSpacing + ExtraRowSpacing;
        return TopPadding + (EditorGUIUtility.singleLineHeight * RowCount) + (rowSpacing * (RowCount - 1)) + BottomPadding;
    }

    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        rect.y += TopPadding;
        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing + ExtraRowSpacing;

        var propertyPathProp = property.FindPropertyRelative("propertyPath");
        var abilityTypeProp = property.FindPropertyRelative("abilityType");
        var overrideModeProp = property.FindPropertyRelative("overrideMode");
        var valuePerBarProp = property.FindPropertyRelative("valuePerBar");
        var allowFractionalProp = property.FindPropertyRelative("allowFractional");

        var headerRect = new Rect(rect.x, rect.y, rect.width, lineHeight);
        EditorGUI.LabelField(headerRect, "Charge Bar Modifier", EditorStyles.boldLabel);

        var abilityTypeRect = new Rect(rect.x, rect.y + lineHeight + spacing, rect.width, lineHeight);
        EditorGUI.PropertyField(abilityTypeRect, abilityTypeProp);

        var dropdownRect = new Rect(rect.x, rect.y + (lineHeight + spacing) * 2, rect.width, lineHeight);

        string selected = propertyPathProp.stringValue ?? "";
        List<string> candidates = null;

        var abilityType = (ChargeBarModifier.AbilityType)abilityTypeProp.enumValueIndex;
        candidates = BuildCandidatesFromType(abilityType);

        if (candidates != null && candidates.Count > 0)
        {
            var prefix = AbilityTypeToPrefix((ChargeBarModifier.AbilityType)abilityTypeProp.enumValueIndex);
            var display = candidates.Select(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? p.Substring(prefix.Length) : p).ToArray();
            int currentIndex = candidates.IndexOf(selected);
            bool hadSelection = currentIndex >= 0;
            if (!hadSelection)
            {
                display = (new[] { "-- Choose Field --" }).Concat(display).ToArray();
                currentIndex = 0;
            }
            int newIndex = EditorGUI.Popup(dropdownRect, "Target Field", currentIndex, display);
            if (!hadSelection)
            {
                if (newIndex == 0)
                {
                    // keep existing propertyPath unchanged (or set to empty if you prefer)
                }
                else
                {
                    propertyPathProp.stringValue = candidates[newIndex - 1];
                }
            }
            else
            {
                propertyPathProp.stringValue = candidates[newIndex];
            }
        }
        else
        {
            // Fallback to text field when no candidates found
            EditorGUI.BeginChangeCheck();
            string newPath = EditorGUI.TextField(dropdownRect, "Target Field (path)", selected);
            if (EditorGUI.EndChangeCheck())
                propertyPathProp.stringValue = newPath;
        }

        var overrideRect = new Rect(rect.x, dropdownRect.yMax + spacing, rect.width, lineHeight);
        EditorGUI.PropertyField(overrideRect, overrideModeProp);

        var valueRect = new Rect(rect.x, overrideRect.yMax + spacing, rect.width, lineHeight);
        EditorGUI.PropertyField(valueRect, valuePerBarProp);

        var fracRect = new Rect(rect.x, valueRect.yMax + spacing, rect.width, lineHeight);
        EditorGUI.PropertyField(fracRect, allowFractionalProp);
    }
    private static string AbilityTypeToPrefix(ChargeBarModifier.AbilityType type)
    {
        switch (type)
        {
            case ChargeBarModifier.AbilityType.Melee:
                return "meleeConfig.";
            case ChargeBarModifier.AbilityType.Projectile:
                return "projectileConfig.";
            default:
                return "";
        }
    }
    private static List<string> BuildCandidatesFromType(ChargeBarModifier.AbilityType abilityType)
{
    string prefix = AbilityTypeToPrefix(abilityType);
    System.Type configType = abilityType switch
    {
        ChargeBarModifier.AbilityType.Melee => typeof(MeleeConfig),
        ChargeBarModifier.AbilityType.Projectile => typeof(ProjectileConfig),
        _ => null
    };
    if (configType == null) return null;

    var result = new List<string>();

    // Direct float/int fields on the config type
    foreach (var field in configType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
    {
        if (field.FieldType == typeof(float) || field.FieldType == typeof(int))
            result.Add(prefix + field.Name);
    }

    // HitboxConfig nested float/int fields (e.g. projectileConfig.hitbox.damage)
    var hitboxField = configType.GetField("hitbox", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
    if (hitboxField != null)
    {
        string hitboxPrefix = prefix + "hitbox.";
        foreach (var field in hitboxField.FieldType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (field.FieldType == typeof(float) || field.FieldType == typeof(int))
                result.Add(hitboxPrefix + field.Name);
        }
    }

    return result.Count > 0 ? result : null;
}
}

