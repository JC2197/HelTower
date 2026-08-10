using System.Collections.Generic;
using JoeConticello.ModularCombatCore;
using UnityEditor;
using UnityEngine;

namespace JoeConticello.ModularCombatCore.Editor
{
	[CustomEditor(typeof(StatTypeDatabase))]
	public sealed class StatTypeDatabaseEditor : UnityEditor.Editor
	{
		private SerializedProperty categoriesProperty;
		private SerializedProperty statTypesProperty;

		private void OnEnable()
		{
			categoriesProperty = serializedObject.FindProperty("categories");
			statTypesProperty = serializedObject.FindProperty("statTypes");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawCategoriesSection();
			EditorGUILayout.Space(10f);
			DrawStatsSection();

			serializedObject.ApplyModifiedProperties();
			((StatTypeDatabase)target).RebuildLookup();
		}

		private void DrawCategoriesSection()
		{
			EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);

			for (int i = 0; i < categoriesProperty.arraySize; i++)
			{
				SerializedProperty element = categoriesProperty.GetArrayElementAtIndex(i);
				DrawCategoryElement(element, i);
			}

			if (GUILayout.Button("Add Category"))
			{
				categoriesProperty.arraySize++;
				SerializedProperty element = categoriesProperty.GetArrayElementAtIndex(categoriesProperty.arraySize - 1);
				element.FindPropertyRelative("categoryId").stringValue = $"Category{categoriesProperty.arraySize}";
				element.FindPropertyRelative("displayName").stringValue = "New Category";
				element.FindPropertyRelative("description").stringValue = string.Empty;
			}
		}

		private void DrawCategoryElement(SerializedProperty element, int index)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField($"Category {index + 1}", EditorStyles.boldLabel);
			if (GUILayout.Button("Remove", GUILayout.Width(70f)))
			{
				categoriesProperty.DeleteArrayElementAtIndex(index);
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
				return;
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.PropertyField(element.FindPropertyRelative("categoryId"), new GUIContent("Category Id"));
			EditorGUILayout.PropertyField(element.FindPropertyRelative("displayName"), new GUIContent("Display Name"));
			EditorGUILayout.PropertyField(element.FindPropertyRelative("description"), new GUIContent("Description"));
			EditorGUILayout.PropertyField(element.FindPropertyRelative("tint"), new GUIContent("Tint"));
			EditorGUILayout.PropertyField(element.FindPropertyRelative("sortOrder"), new GUIContent("Sort Order"));
			EditorGUILayout.EndVertical();
		}

		private void DrawStatsSection()
		{
			EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);

			List<string> categoryIds = GetCategoryIds();

			for (int i = 0; i < statTypesProperty.arraySize; i++)
			{
				SerializedProperty element = statTypesProperty.GetArrayElementAtIndex(i);
				DrawStatElement(element, i, categoryIds);
			}

			if (GUILayout.Button("Add Stat"))
			{
				statTypesProperty.arraySize++;
				SerializedProperty element = statTypesProperty.GetArrayElementAtIndex(statTypesProperty.arraySize - 1);
				element.FindPropertyRelative("statId").stringValue = $"NewStat{statTypesProperty.arraySize}";
				element.FindPropertyRelative("displayName").stringValue = "New Stat";
				element.FindPropertyRelative("categoryId").stringValue = categoryIds.Count > 0 ? categoryIds[0] : string.Empty;
				element.FindPropertyRelative("defaultValue").floatValue = 0f;
			}
		}

		private void DrawStatElement(SerializedProperty element, int index, List<string> categoryIds)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField($"Stat {index + 1}", EditorStyles.boldLabel);
			if (GUILayout.Button("Remove", GUILayout.Width(70f)))
			{
				statTypesProperty.DeleteArrayElementAtIndex(index);
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
				return;
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.PropertyField(element.FindPropertyRelative("statId"), new GUIContent("Stat Id"));
			EditorGUILayout.PropertyField(element.FindPropertyRelative("displayName"), new GUIContent("Display Name"));
			DrawCategoryPopup(element.FindPropertyRelative("categoryId"), categoryIds);
			EditorGUILayout.PropertyField(element.FindPropertyRelative("icon"), new GUIContent("Icon"));
			EditorGUILayout.PropertyField(element.FindPropertyRelative("defaultValue"), new GUIContent("Default Value"));
			EditorGUILayout.PropertyField(element.FindPropertyRelative("isPercentage"), new GUIContent("Is Percentage"));
			EditorGUILayout.PropertyField(element.FindPropertyRelative("maxValue"), new GUIContent("Max Value"));
			EditorGUILayout.PropertyField(element.FindPropertyRelative("description"), new GUIContent("Description"));
			EditorGUILayout.EndVertical();
		}

		private void DrawCategoryPopup(SerializedProperty categoryIdProperty, List<string> categoryIds)
		{
			if (categoryIds.Count == 0)
			{
				EditorGUILayout.PropertyField(categoryIdProperty, new GUIContent("Category Id"));
				return;
			}

			int selectedIndex = Mathf.Max(0, categoryIds.IndexOf(categoryIdProperty.stringValue));
			selectedIndex = EditorGUILayout.Popup("Category", selectedIndex, categoryIds.ToArray());
			categoryIdProperty.stringValue = categoryIds[selectedIndex];
		}

		private List<string> GetCategoryIds()
		{
			List<string> categoryIds = new List<string>();
			for (int i = 0; i < categoriesProperty.arraySize; i++)
			{
				string categoryId = categoriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("categoryId").stringValue;
				if (!string.IsNullOrWhiteSpace(categoryId))
					categoryIds.Add(categoryId);
			}
			return categoryIds;
		}
	}
}
