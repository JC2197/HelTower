using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(WeaponTypeDropdownAttribute))]
public class WeaponTypeDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Use [WeaponTypeDropdown] with string fields only");
            return;
        }

        // Load weapon type list
        WeaponTypeList weaponTypeList = WeaponTypeList.GetInstance();
        if (weaponTypeList == null)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        List<string> weaponTypes = weaponTypeList.weaponTypes;
        if (weaponTypes == null || weaponTypes.Count == 0)
        {
            EditorGUI.LabelField(position, label.text, "No weapon types defined");
            return;
        }

        // Find current index
        string currentValue = property.stringValue;
        int currentIndex = weaponTypes.IndexOf(currentValue);
        if (currentIndex == -1) currentIndex = 0;

        // Draw dropdown
        EditorGUI.BeginProperty(position, label, property);
        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, weaponTypes.ToArray());
        if (newIndex != currentIndex)
        {
            property.stringValue = weaponTypes[newIndex];
        }
        EditorGUI.EndProperty();
    }
}
