#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;

/// <summary>
/// Property drawer for AbilityConfigModifier using the Property Path System.
/// Shows a list of overrides with property dropdown, mode selector, and value field.
/// Displays base value → effective value for quick reference.
/// </summary>
[CustomPropertyDrawer(typeof(AbilityConfigModifier))]
public class AbilityConfigModifierDrawer : PropertyDrawer
{
    private static float LH => EditorGUIUtility.singleLineHeight;
    private static float VS => EditorGUIUtility.standardVerticalSpacing;
    private const System.Reflection.BindingFlags SerializableInstanceFieldFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return LH;

        var overridesProp = property.FindPropertyRelative("overrides");
        var addTriggeredAbilityConfigProp = property.FindPropertyRelative("addTriggeredAbilityConfig");
        var addTriggeredAbilityAbilityProp = addTriggeredAbilityConfigProp?.FindPropertyRelative("abilityConfig");
        int overrideCount = overridesProp?.arraySize ?? 0;

        float totalHeight = 0f;
        totalHeight += LH + VS; // foldout
        totalHeight += LH + VS; // target ability
        totalHeight += LH + VS; // ability icon

        if (addTriggeredAbilityConfigProp != null)
            totalHeight += EditorGUI.GetPropertyHeight(addTriggeredAbilityConfigProp, true) + VS;
        else
            totalHeight += LH + VS;

        bool showTriggeredPath = addTriggeredAbilityAbilityProp != null && addTriggeredAbilityAbilityProp.objectReferenceValue != null;
        if (showTriggeredPath)
            totalHeight += LH + VS;

        totalHeight += LH + VS; // "Overrides" label
        totalHeight += overrideCount * (LH * 2 + VS * 2);
        totalHeight += LH + VS; // add button

        return totalHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var targetProp = property.FindPropertyRelative("targetAbility");
        var target = targetProp.objectReferenceValue as AbilityDataConfig;
        var overridesProp = property.FindPropertyRelative("overrides");

        // Foldout header
        string foldLabel = target != null ? $"→ {target.abilityName}" : "Ability Config Modifier";
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, LH),
            property.isExpanded, foldLabel, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        float y = position.y + LH + VS;

        // Target ability field
        EditorGUI.PropertyField(new Rect(position.x, y, position.width, LH), targetProp);
        y += LH + VS;

        // Ability icon override field
        var abilityIconProp = property.FindPropertyRelative("abilityIcon");
        EditorGUI.PropertyField(new Rect(position.x, y, position.width, LH), abilityIconProp, new GUIContent("Ability Icon Override"));
        y += LH + VS;

        // Direct triggered-ability append helper field (full config)
        var addTriggeredAbilityConfigProp = property.FindPropertyRelative("addTriggeredAbilityConfig");
        var addTriggeredAbilityAbilityProp = addTriggeredAbilityConfigProp?.FindPropertyRelative("abilityConfig");
        if (addTriggeredAbilityConfigProp != null)
        {
            float triggeredHeight = EditorGUI.GetPropertyHeight(addTriggeredAbilityConfigProp, true);
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, triggeredHeight),
                addTriggeredAbilityConfigProp,
                new GUIContent("Add Triggered Ability"),
                true);
            y += triggeredHeight + VS;
        }
        else
        {
            y += LH + VS;
        }

        // Optional target source path (summon/construct sub-ability selection)
        var addTriggeredAbilityPathProp = property.FindPropertyRelative("addTriggeredAbilityPath");
        bool hasTriggeredAbility = addTriggeredAbilityAbilityProp != null && addTriggeredAbilityAbilityProp.objectReferenceValue != null;
        if (hasTriggeredAbility)
        {
            List<string> triggerTargets = AbilityModifierRuntime.GetTriggeredAbilityAppendTargets(target);
            string[] targetOptions = new string[triggerTargets.Count + 1];
            targetOptions[0] = "All Trigger Sources";
            for (int i = 0; i < triggerTargets.Count; i++)
                targetOptions[i + 1] = FormatPropertyPath(triggerTargets[i]);

            int selectedTargetIndex = 0;
            if (!string.IsNullOrEmpty(addTriggeredAbilityPathProp.stringValue))
            {
                int match = triggerTargets.IndexOf(addTriggeredAbilityPathProp.stringValue);
                selectedTargetIndex = match >= 0 ? match + 1 : 0;
            }

            int newIndex = EditorGUI.Popup(
                new Rect(position.x, y, position.width, LH),
                "Add Triggered Ability Target",
                selectedTargetIndex,
                targetOptions);

            addTriggeredAbilityPathProp.stringValue = newIndex <= 0
                ? ""
                : triggerTargets[newIndex - 1];
            y += LH + VS;
        }
        else if (addTriggeredAbilityPathProp != null && !string.IsNullOrEmpty(addTriggeredAbilityPathProp.stringValue))
        {
            addTriggeredAbilityPathProp.stringValue = string.Empty;
        }

        if (target == null)
        {
            EditorGUI.HelpBox(new Rect(position.x + 15, y, position.width - 15, LH * 1.5f),
                "Assign a target ability to add overrides.", MessageType.Info);
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
            return;
        }

        // Get available properties for this ability
        var availableProperties = AbilityModifierRuntime.GetAllModifiableProperties(target);

        // Overrides header
        EditorGUI.LabelField(new Rect(position.x, y, position.width, LH), "Overrides", EditorStyles.boldLabel);
        y += LH + VS;

        // Draw each override
        for (int i = 0; i < overridesProp.arraySize; i++)
        {
            var overrideProp = overridesProp.GetArrayElementAtIndex(i);
            bool deleted = DrawOverride(ref y, position, overrideProp, target, availableProperties, i, overridesProp);
            if (deleted)
            {
                i--;
                continue;
            }
        }

        // Add button
        Rect addRect = new Rect(position.x + 15, y, position.width - 15, LH);
        if (GUI.Button(addRect, "+ Add Override"))
        {
            overridesProp.InsertArrayElementAtIndex(overridesProp.arraySize);
            var newProp = overridesProp.GetArrayElementAtIndex(overridesProp.arraySize - 1);
            newProp.FindPropertyRelative("propertyPath").stringValue = "";
            newProp.FindPropertyRelative("overrideMode").enumValueIndex = 0;
            newProp.FindPropertyRelative("numericValue").floatValue = 0f;
            newProp.FindPropertyRelative("stringValue").stringValue = "";
            newProp.FindPropertyRelative("objectValue").objectReferenceValue = null;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    /// <summary>
    /// Draws a single override entry. Returns true if deleted.
    /// </summary>
    private bool DrawOverride(ref float y, Rect position, SerializedProperty overrideProp,
        AbilityDataConfig target, List<string> availableProperties, int index, SerializedProperty listProp)
    {
        float x = position.x + 15;
        float w = position.width - 15;

        var pathProp = overrideProp.FindPropertyRelative("propertyPath");
        var modeProp = overrideProp.FindPropertyRelative("overrideMode");
        var numericProp = overrideProp.FindPropertyRelative("numericValue");
        var stringProp = overrideProp.FindPropertyRelative("stringValue");
        var objectProp = overrideProp.FindPropertyRelative("objectValue");

        // Subtle depth tint: nested property paths get slightly different panel colors.
        int nestedDepth = GetPathNestingDepth(pathProp.stringValue);
        Rect bgRect = new Rect(x - 2f, y - 1f, w + 2f, (LH * 2f) + VS + 2f);
        EditorGUI.DrawRect(bgRect, GetDepthTint(nestedDepth));

        // Row 1: [X] [Property Dropdown] [Mode Dropdown]
        float btnW = 20f;
        float modeW = 70f;
        float pathW = w - btnW - modeW - 10f;

        // Delete button
        if (GUI.Button(new Rect(x, y, btnW, LH), "×"))
        {
            listProp.DeleteArrayElementAtIndex(index);
            return true;
        }

        // Property dropdown
        int currentIndex = availableProperties.IndexOf(pathProp.stringValue);

        Rect propertyPopupRect = new Rect(x + btnW + 4, y, pathW, LH);
        string currentLabel = currentIndex >= 0 && currentIndex < availableProperties.Count
            ? FormatPropertyPath(availableProperties[currentIndex])
            : "(Select Property)";

        if (GUI.Button(propertyPopupRect, currentLabel, EditorStyles.popup))
        {
            UnityEngine.Object hostObject = pathProp.serializedObject.targetObject;
            string hostPropertyPath = pathProp.propertyPath;
            PopupWindow.Show(propertyPopupRect, new PropertyPathDropdownPopup(
                availableProperties,
                currentIndex,
                selectedIndex =>
                {
                    if (hostObject == null || string.IsNullOrEmpty(hostPropertyPath))
                        return;

                    string selectedPath = selectedIndex >= 0 && selectedIndex < availableProperties.Count
                        ? availableProperties[selectedIndex]
                        : "";

                    SerializedObject so = new SerializedObject(hostObject);
                    SerializedProperty resolvedProp = so.FindProperty(hostPropertyPath);
                    if (resolvedProp == null)
                        return;

                    resolvedProp.stringValue = selectedPath;
                    so.ApplyModifiedProperties();
                    GUI.changed = true;
                }));
        }

        // Mode dropdown
        EditorGUI.PropertyField(new Rect(x + btnW + pathW + 8, y, modeW, LH), modeProp, GUIContent.none);
        y += LH + VS;

        // Row 2: Value field + hint
        if (!string.IsNullOrEmpty(pathProp.stringValue))
        {
            var mode = (OverrideMode)modeProp.enumValueIndex;
            var fieldType = GetFieldType(target, pathProp.stringValue);

            float valueW = 80f;
            float hintX = x + valueW + 10f;
            float hintW = w - valueW - 10f;

            if (mode == OverrideMode.Set && fieldType == typeof(string))
            {
                // String value for damage types etc.
                EditorGUI.PropertyField(new Rect(x, y, valueW * 1.5f, LH), stringProp, GUIContent.none);
                string baseVal = GetBaseStringValue(target, pathProp.stringValue);
                string hint = !string.IsNullOrEmpty(stringProp.stringValue)
                    ? $"{baseVal} → {stringProp.stringValue}"
                    : $"base: {baseVal}";
                DrawHint(new Rect(hintX + valueW * 0.5f, y, hintW - valueW * 0.5f, LH), hint);
            }
            else if (mode == OverrideMode.Set && fieldType.IsEnum)
            {
                string[] enumNames = System.Enum.GetNames(fieldType);
                int enumCount = enumNames.Length;
                int baseIndex = GetBaseEnumIndex(target, pathProp.stringValue, fieldType);
                int selectedIndex = Mathf.Clamp(Mathf.RoundToInt(numericProp.floatValue), 0, Mathf.Max(0, enumCount - 1));

                if (enumCount > 0)
                {
                    selectedIndex = EditorGUI.Popup(new Rect(x, y, valueW * 1.5f, LH), selectedIndex, enumNames);
                    numericProp.floatValue = selectedIndex;
                    string baseLabel = enumNames[Mathf.Clamp(baseIndex, 0, enumCount - 1)];
                    string selectedLabel = enumNames[selectedIndex];
                    string hint = baseIndex != selectedIndex
                        ? $"{baseLabel} → {selectedLabel}"
                        : $"base: {baseLabel}";
                    DrawHint(new Rect(hintX + valueW * 0.5f, y, hintW - valueW * 0.5f, LH), hint);
                }
            }
            else if (mode == OverrideMode.Set && typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                // Object reference for prefabs etc.
                EditorGUI.PropertyField(new Rect(x, y, valueW * 2f, LH), objectProp, GUIContent.none);
                DrawHint(new Rect(hintX + valueW, y, hintW - valueW, LH),
                    objectProp.objectReferenceValue != null ? "→ custom" : "base prefab");
            }
            else
            {
                // Numeric value
                EditorGUI.PropertyField(new Rect(x, y, valueW, LH), numericProp, GUIContent.none);

                float baseVal = GetBaseNumericValue(target, pathProp.stringValue);
                float delta = numericProp.floatValue;
                string hint;

                if (mode == OverrideMode.Flat)
                    hint = delta != 0 ? $"{baseVal:0.##} → {baseVal + delta:0.##}" : $"base: {baseVal:0.##}";
                else if (mode == OverrideMode.Percent)
                    hint = delta != 0 ? $"{baseVal:0.##} × {1f + delta / 100f:0.##} = {baseVal * (1f + delta / 100f):0.##}" : $"base: {baseVal:0.##}";
                else
                    hint = $"→ {delta:0.##}";

                DrawHint(new Rect(hintX, y, hintW, LH), hint);
            }
        }
        y += LH + VS;
        return false;
    }

    private static int GetPathNestingDepth(string path)
    {
        if (string.IsNullOrEmpty(path))
            return 0;

        int dots = 0;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == '.')
                dots++;
        }
        return dots;
    }

    private static Color GetDepthTint(int depth) 
{ 
    int band = Mathf.Max(0, depth); 
    
    // Changing alpha from 0.44f-0.54f to 1.0f makes them fully visible
    Color[] lightPalette = { 
        new Color(0.19f, 0.22f, 0.26f, 1.0f), // base 
        new Color(0.16f, 0.27f, 0.33f, 1.0f), // teal 
        new Color(0.20f, 0.31f, 0.28f, 1.0f), // green 
        new Color(0.31f, 0.30f, 0.20f, 1.0f), // olive 
        new Color(0.33f, 0.25f, 0.20f, 1.0f), // amber 
        new Color(0.31f, 0.20f, 0.23f, 1.0f), // rose 
    }; 
    
    if (band < lightPalette.Length) return lightPalette[band]; 
    return Color.Lerp(lightPalette[lightPalette.Length - 2], lightPalette[lightPalette.Length - 1], 0.7f); 
}

    private sealed class PropertyPathDropdownPopup : PopupWindowContent
    {
        private readonly List<string> _availableProperties;
        private readonly int _selectedIndex;
        private readonly Action<int> _onSelect;
        private readonly List<int> _filteredIndices = new List<int>();
        private Vector2 _scroll;
        private string _search = "";

        private const float RowHeight = 20f;
        private const float HeaderHeight = 24f;
        private const float SearchHeight = 22f;

        public PropertyPathDropdownPopup(List<string> availableProperties, int selectedIndex, Action<int> onSelect)
        {
            _availableProperties = availableProperties ?? new List<string>();
            _selectedIndex = selectedIndex;
            _onSelect = onSelect;
            RebuildFilter();
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(420f, 360f);
        }

        public override void OnGUI(Rect rect)
        {
            DrawHeader();
            DrawSearchBar();
            DrawList();
        }

        private void DrawHeader()
        {
            Rect headerRect = GUILayoutUtility.GetRect(1f, HeaderHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, EditorGUIUtility.isProSkin
                ? new Color(0.16f, 0.16f, 0.16f, 1f)
                : new Color(0.86f, 0.86f, 0.86f, 1f));

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 0, 0)
            };
            EditorGUI.LabelField(headerRect, "Select Property Override", headerStyle);
        }

        private void DrawSearchBar()
        {
            Rect searchRect = GUILayoutUtility.GetRect(1f, SearchHeight, GUILayout.ExpandWidth(true));
            Rect inner = new Rect(searchRect.x + 6f, searchRect.y + 2f, searchRect.width - 12f, searchRect.height - 4f);

            EditorGUI.BeginChangeCheck();
            _search = EditorGUI.TextField(inner, _search);
            if (EditorGUI.EndChangeCheck())
                RebuildFilter();
        }

        private void DrawList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawOption(-1, "(Select Property)", 0, _selectedIndex < 0);

            for (int i = 0; i < _filteredIndices.Count; i++)
            {
                int optionIndex = _filteredIndices[i];
                string propertyPath = _availableProperties[optionIndex];
                string label = FormatPropertyPath(propertyPath);
                int depth = GetPathNestingDepth(propertyPath);
                bool isSelected = optionIndex == _selectedIndex;
                DrawOption(optionIndex, label, depth, isSelected);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawOption(int optionIndex, string label, int depth, bool isSelected)
        {
            Rect rowRect = GUILayoutUtility.GetRect(1f, RowHeight, GUILayout.ExpandWidth(true));

            Color baseTint = GetDepthTint(depth);
            Color rowTint = isSelected
                ? Color.Lerp(baseTint, Color.white, EditorGUIUtility.isProSkin ? 0.22f : 0.08f)
                : baseTint;

            EditorGUI.DrawRect(rowRect, rowTint);

            Rect labelRect = new Rect(
                rowRect.x + 8f + (depth * 12f),
                rowRect.y,
                rowRect.width - 12f - (depth * 12f),
                rowRect.height);

            GUIStyle rowLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.1f, 0.1f, 0.1f) }
            };

            EditorGUI.LabelField(labelRect, label, rowLabelStyle);

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                _onSelect?.Invoke(optionIndex);
                editorWindow.Close();
                Event.current.Use();
            }
        }

        private void RebuildFilter()
        {
            _filteredIndices.Clear();
            string term = string.IsNullOrEmpty(_search) ? "" : _search.Trim().ToLowerInvariant();

            for (int i = 0; i < _availableProperties.Count; i++)
            {
                string display = FormatPropertyPath(_availableProperties[i]);
                if (string.IsNullOrEmpty(term)
                    || display.ToLowerInvariant().Contains(term)
                    || _availableProperties[i].ToLowerInvariant().Contains(term))
                {
                    _filteredIndices.Add(i);
                }
            }
        }
    }

    private static string FormatPropertyPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.Contains("."))
        {
            string[] parts = path.Split('.');
            var formattedParts = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string clean = parts[i].Replace("Config", "").Replace("config", "");
                if (string.IsNullOrEmpty(clean))
                    continue;

                formattedParts.Add(FormatFieldName(clean));
            }

            return string.Join("/", formattedParts);
        }
        return FormatFieldName(path);
    }

    private static string FormatFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return fieldName;
        var sb = new System.Text.StringBuilder();
        sb.Append(char.ToUpper(fieldName[0]));
        for (int i = 1; i < fieldName.Length; i++)
        {
            if (char.IsUpper(fieldName[i]) && !char.IsUpper(fieldName[i - 1]))
                sb.Append(' ');
            sb.Append(fieldName[i]);
        }
        return sb.ToString();
    }

    private struct PathSegment
    {
        public string fieldName;
        public int index;
        public bool hasIndex;
    }

    private static bool IsSerializedInstanceField(System.Reflection.FieldInfo field)
    {
        return field != null
            && !field.IsStatic
            && (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null);
    }

    private static System.Reflection.FieldInfo FindSerializedInstanceField(System.Type type, string fieldName)
    {
        while (type != null)
        {
            System.Reflection.FieldInfo field = type.GetField(fieldName, SerializableInstanceFieldFlags | System.Reflection.BindingFlags.DeclaredOnly);
            if (IsSerializedInstanceField(field))
                return field;

            type = type.BaseType;
        }

        return null;
    }

    private static bool TryParsePathSegment(string rawSegment, out PathSegment segment)
    {
        segment = default;
        if (string.IsNullOrEmpty(rawSegment))
            return false;

        int bracketStart = rawSegment.IndexOf('[');
        if (bracketStart < 0)
        {
            segment.fieldName = rawSegment;
            segment.index = -1;
            segment.hasIndex = false;
            return true;
        }

        int bracketEnd = rawSegment.IndexOf(']', bracketStart + 1);
        if (bracketEnd < 0)
            return false;

        string fieldName = rawSegment.Substring(0, bracketStart);
        string indexText = rawSegment.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
        if (string.IsNullOrEmpty(fieldName) || !int.TryParse(indexText, out int index))
            return false;

        segment.fieldName = fieldName;
        segment.index = index;
        segment.hasIndex = true;
        return true;
    }

    private static bool TryResolvePathValue(AbilityDataConfig config, string propertyPath, out object value, out System.Type valueType)
    {
        value = null;
        valueType = null;

        if (config == null || string.IsNullOrEmpty(propertyPath))
            return false;

        string[] parts = propertyPath.Split('.');
        object current = config;
        System.Type currentType = typeof(AbilityDataConfig);

        for (int i = 0; i < parts.Length; i++)
        {
            if (!TryParsePathSegment(parts[i], out PathSegment segment))
                return false;

            var field = FindSerializedInstanceField(currentType, segment.fieldName);
            if (field == null)
                return false;

            object fieldValue = field.GetValue(current);
            if (fieldValue == null)
                return false;

            if (segment.hasIndex)
            {
                if (fieldValue is System.Array arr)
                {
                    if (segment.index < 0 || segment.index >= arr.Length)
                        return false;

                    object element = arr.GetValue(segment.index);
                    if (element == null)
                        return false;

                    current = element;
                    currentType = element.GetType();
                    continue;
                }

                if (fieldValue is IList list)
                {
                    if (segment.index < 0 || segment.index >= list.Count)
                        return false;

                    object element = list[segment.index];
                    if (element == null)
                        return false;

                    current = element;
                    currentType = element.GetType();
                    continue;
                }

                return false;
            }

            current = fieldValue;
            currentType = fieldValue.GetType();
        }

        value = current;
        valueType = currentType;
        return true;
    }

    private static System.Type GetFieldType(AbilityDataConfig config, string propertyPath)
    {
        return TryResolvePathType(config, propertyPath, out System.Type valueType)
            ? valueType
            : typeof(float);
    }

    private static bool TryResolvePathType(AbilityDataConfig config, string propertyPath, out System.Type valueType)
    {
        valueType = null;
        if (config == null || string.IsNullOrEmpty(propertyPath))
            return false;

        string[] parts = propertyPath.Split('.');
        object current = config;
        System.Type currentType = typeof(AbilityDataConfig);

        for (int i = 0; i < parts.Length; i++)
        {
            if (!TryParsePathSegment(parts[i], out PathSegment segment))
                return false;

            var field = FindSerializedInstanceField(currentType, segment.fieldName);
            if (field == null)
                return false;

            object fieldValue = field.GetValue(current);
            if (fieldValue == null)
                return false;

            currentType = fieldValue.GetType();

            if (segment.hasIndex)
            {
                if (fieldValue is System.Array arr)
                {
                    if (segment.index < 0 || segment.index >= arr.Length)
                        return false;

                    object element = arr.GetValue(segment.index);
                    if (element == null)
                        return false;

                    current = element;
                    currentType = element.GetType();
                    continue;
                }

                if (fieldValue is IList list)
                {
                    if (segment.index < 0 || segment.index >= list.Count)
                        return false;

                    object element = list[segment.index];
                    if (element == null)
                        return false;

                    current = element;
                    currentType = element.GetType();
                    continue;
                }

                return false;
            }

            current = fieldValue;
        }

        valueType = currentType;
        return true;
    }

    private static float GetBaseNumericValue(AbilityDataConfig config, string propertyPath)
    {
        if (!TryResolvePathValue(config, propertyPath, out object current, out _))
            return 0f;

        if (current is float f) return f;
        if (current is int i) return i;
        return 0f;
    }

    private static string GetBaseStringValue(AbilityDataConfig config, string propertyPath)
    {
        if (!TryResolvePathValue(config, propertyPath, out object current, out _))
            return "";

        return current as string ?? "";
    }

    private static int GetBaseEnumIndex(AbilityDataConfig config, string propertyPath, System.Type enumType)
    {
        if (config == null || enumType == null || !enumType.IsEnum) return 0;
        if (!TryResolvePathValue(config, propertyPath, out object current, out _))
            return 0;

        if (current == null) return 0;
        return System.Array.IndexOf(System.Enum.GetValues(enumType), current);
    }

    private static GUIStyle _hintStyle;
    private static void DrawHint(Rect r, string text)
    {
        if (_hintStyle == null)
        {
            _hintStyle = new GUIStyle(EditorStyles.miniLabel);
            _hintStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.55f, 0.55f, 0.55f)
                : new Color(0.4f, 0.4f, 0.4f);
        }
        EditorGUI.LabelField(r, text, _hintStyle);
    }
}
#endif
