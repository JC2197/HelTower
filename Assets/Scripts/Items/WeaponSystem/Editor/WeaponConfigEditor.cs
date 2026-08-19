using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WeaponConfig))]
public class WeaponConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WeaponConfig weaponConfig = (WeaponConfig)target;
        
        serializedObject.Update();
        
        SerializedProperty prop = serializedObject.GetIterator();
        if (prop.NextVisible(true))
        {
            do
            {
                if (prop.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(prop, true);
                }
                else if (prop.name == "offhandWeaponConfig")
                {
                    using (new EditorGUI.DisabledScope(weaponConfig.isOffhand))
                        EditorGUILayout.PropertyField(prop, true);
                }
                else if (prop.name == "overridePositioning")
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.PropertyField(prop);
                    
                    // Show which WeaponTypeConfig is being inherited from
                    if (!weaponConfig.overridePositioning)
                    {
                        var typeConfig = WeaponTypeConfig.EditorGetConfigForType(weaponConfig.weaponType);
                        if (typeConfig != null)
                        {
                            EditorGUILayout.HelpBox(
                                $"Inheriting positioning from WeaponTypeConfig: \"{typeConfig.typeName}\"",
                                MessageType.Info);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox(
                                $"No WeaponTypeConfig found for \"{weaponConfig.weaponType}\". Using local override values as fallback.",
                                MessageType.Warning);
                        }
                    }
                }
                else if (prop.name == "positioningOverride")
                {
                    // Only show the override data when override is enabled, or when no type config exists
                    if (weaponConfig.overridePositioning || 
                        WeaponTypeConfig.EditorGetConfigForType(weaponConfig.weaponType) == null)
                    {
                        EditorGUILayout.PropertyField(prop, new GUIContent("Positioning Override"), true);
                    }
                }
                // usesAmmo: draw the toggle, then conditionally expand ammoConfig below it
                else if (prop.name == "usesAmmo")
                {
                    EditorGUILayout.PropertyField(prop);
                }
                // ammoConfig: only draw (expanded inline) when usesAmmo is true
                else if (prop.name == "ammoConfig")
                {
                    if (weaponConfig.usesAmmo)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField("Ammo Configuration", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;

                        SerializedProperty ammoProp = prop.Copy();
                        SerializedProperty dependsOnAmmo  = ammoProp.FindPropertyRelative("dependsOnAmmo");
                        SerializedProperty magazineSize   = ammoProp.FindPropertyRelative("magazineSize");
                        SerializedProperty reloadTime     = ammoProp.FindPropertyRelative("reloadTime");
                        SerializedProperty ammoIcon       = ammoProp.FindPropertyRelative("ammoIcon");

                        EditorGUILayout.PropertyField(dependsOnAmmo,  new GUIContent("Depends On Ammo",  "Ability won't fire when out of ammo."));
                        EditorGUILayout.PropertyField(magazineSize,   new GUIContent("Magazine Size",    "Shots before reload is required."));
                        EditorGUILayout.PropertyField(reloadTime,     new GUIContent("Reload Time (s)",  "Seconds to complete a reload."));
                        EditorGUILayout.PropertyField(ammoIcon,       new GUIContent("Ammo Icon",        "HUD icon for this ammo type."));

                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndVertical();
                    }
                    // else skip — ammoConfig is hidden when usesAmmo is false
                }
                else
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
            }
            while (prop.NextVisible(false));
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
