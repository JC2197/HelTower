using System;
using System.Collections.Generic;
using JoeConticello.ModularCombatCore;
using UnityEditor;
using UnityEngine;

namespace JoeConticello.ModularCombatCore.Editor
{
	[CustomPropertyDrawer(typeof(StatContainer))]
	public sealed class StatContainerDrawer : PropertyDrawer
	{
		private static readonly Dictionary<string, bool> CategoryFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			SerializedProperty statsProperty = property.FindPropertyRelative("stats");
			float y = position.y;

			Rect foldoutRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
			property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true, EditorStyles.foldoutHeader);
			y += EditorGUIUtility.singleLineHeight + 4f;

			if (property.isExpanded)
			{
				EditorGUI.indentLevel++;

				if (statsProperty.arraySize == 0)
				{
					Rect helpRect = new Rect(position.x, y, position.width, 36f);
					EditorGUI.HelpBox(helpRect, "Stat container starts empty. Initialize it from a StatTypeDatabase asset or add stats in the database editor.", MessageType.Info);
				}
				else
				{
					List<SerializedProperty> stats = new List<SerializedProperty>(statsProperty.arraySize);
					for (int i = 0; i < statsProperty.arraySize; i++)
						stats.Add(statsProperty.GetArrayElementAtIndex(i));

					List<string> categoryOrder = BuildCategoryOrder(statsProperty);

					foreach (string categoryId in categoryOrder)
					{
						string headerText = string.IsNullOrWhiteSpace(categoryId) ? "Uncategorized" : categoryId;
						if (!CategoryFoldouts.ContainsKey(headerText))
							CategoryFoldouts[headerText] = true;

						Rect headerRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
						CategoryFoldouts[headerText] = EditorGUI.Foldout(headerRect, CategoryFoldouts[headerText], headerText, true, EditorStyles.foldoutHeader);
						y += EditorGUIUtility.singleLineHeight + 2f;

						if (!CategoryFoldouts[headerText])
							continue;

						EditorGUI.indentLevel++;
						foreach (SerializedProperty statProperty in stats)
						{
							string statCategory = statProperty.FindPropertyRelative("categoryId").stringValue;
							if (!string.Equals(statCategory ?? string.Empty, categoryId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
								continue;

							SerializedProperty statId = statProperty.FindPropertyRelative("statId");
							SerializedProperty displayName = statProperty.FindPropertyRelative("displayName");
							SerializedProperty currentValue = statProperty.FindPropertyRelative("currentValue");

							Rect rowRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
							Rect leftRect = new Rect(rowRect.x, rowRect.y, rowRect.width * 0.65f, rowRect.height);
							Rect rightRect = new Rect(rowRect.x + rowRect.width * 0.67f, rowRect.y, rowRect.width * 0.33f, rowRect.height);
							string labelText = string.IsNullOrEmpty(displayName.stringValue) ? statId.stringValue : displayName.stringValue;
							EditorGUI.LabelField(leftRect, labelText);
							currentValue.floatValue = EditorGUI.FloatField(rightRect, currentValue.floatValue);
							y += EditorGUIUtility.singleLineHeight + 2f;
						}

						EditorGUI.indentLevel--;
						y += 4f;
					}
				}

				EditorGUI.indentLevel--;
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (!property.isExpanded)
				return EditorGUIUtility.singleLineHeight;

			SerializedProperty statsProperty = property.FindPropertyRelative("stats");
			float height = EditorGUIUtility.singleLineHeight + 4f;

			if (statsProperty.arraySize == 0)
				return height + 40f;

			List<string> categoryOrder = BuildCategoryOrder(statsProperty);
			foreach (string categoryId in categoryOrder)
			{
				height += EditorGUIUtility.singleLineHeight + 2f;

				if (!CategoryFoldouts.TryGetValue(string.IsNullOrWhiteSpace(categoryId) ? "Uncategorized" : categoryId, out bool isExpanded) || !isExpanded)
					continue;

				for (int i = 0; i < statsProperty.arraySize; i++)
				{
					SerializedProperty statProperty = statsProperty.GetArrayElementAtIndex(i);
					string statCategory = statProperty.FindPropertyRelative("categoryId").stringValue;
					if (!string.Equals(statCategory ?? string.Empty, categoryId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
						continue;

					height += EditorGUIUtility.singleLineHeight + 2f;
				}

				height += 4f;
			}

			return height;
		}

		private static List<string> BuildCategoryOrder(SerializedProperty statsProperty)
		{
			List<string> order = new List<string>();
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			for (int i = 0; i < statsProperty.arraySize; i++)
			{
				SerializedProperty stat = statsProperty.GetArrayElementAtIndex(i);
				string categoryId = stat.FindPropertyRelative("categoryId").stringValue;
				string key = string.IsNullOrWhiteSpace(categoryId) ? string.Empty : categoryId;
				if (seen.Add(key))
					order.Add(key);
			}

			return order;
		}
	}
}
