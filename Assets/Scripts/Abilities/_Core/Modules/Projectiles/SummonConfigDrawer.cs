#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for SummonConfig.
/// Clean, modular, and performance-optimized for the consolidated AbilityDataConfig arrays list layout.
/// </summary>
[CustomPropertyDrawer(typeof(SummonConfig))]
public class SummonConfigDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float yPos = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // --- Basic Prefab Setup Profile ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("summonPrefab"), position, yPos);

            // --- Summon Limits ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("maxSummons"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("limitBehavior"), position, yPos);

            // --- Lifetime Lifecycle Windows ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("lifetime"), position, yPos);

            // --- Health Data Container ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("statContainer"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("healthBarPrefab"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("isConstruct"), position, yPos);
            
            // --- AI Seek & Follow Behaviours ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("seekBehavior"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("followDistance"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("slotOffsets"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("stopDistance"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("moveSpeed"), position, yPos);

            // --- Target Detection Coordinates ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("detectionRange"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("attackRange"), position, yPos);

            // --- Pathfinding Boundaries ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("pathfindingObstacleLayers"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("obstacleAvoidanceStrength"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("debugDrawPathfindingRays"), position, yPos);

            // --- Streamlined Combat Capability List Array ---
SerializedProperty abilitiesProp = property.FindPropertyRelative("summonAbilities");
            if (abilitiesProp != null)
            {
                // Calculate precise drawing bounding boxes for the collection container
                float propHeight = EditorGUI.GetPropertyHeight(abilitiesProp, true);
                Rect abilitiesRect = new Rect(position.x, yPos, position.width, propHeight);
                
                // CRITICAL INJECTION: Pass includeChildren = true to tell the layout engine 
                // to draw the list size fields and array indices recursively!
                EditorGUI.PropertyField(abilitiesRect, abilitiesProp, true);
                
                yPos += propHeight + EditorGUIUtility.standardVerticalSpacing;
            }
            // --- Conditional Rotational Turret Settings ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("isRotationalTurret"), position, yPos);
            SerializedProperty isRotationalTurretProp = property.FindPropertyRelative("isRotationalTurret");
            if (isRotationalTurretProp != null && isRotationalTurretProp.boolValue)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("turretChildName"), position, yPos);
            }

            // --- Body Animations ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("idleAnimation"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("moveAnimation"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spawnAnimation"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("deathAnimation"), position, yPos);
            // --- Spawn Coordinates & Placements ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spawnOffset"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spawnAnimation"), position, yPos);

            // --- Particles & Feedback Visual Effects ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spawnEffectPrefab"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("deathEffectPrefab"), position, yPos);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded) return height;

        height += EditorGUIUtility.standardVerticalSpacing;

        // Cleaned fields map list matching your active class properties
        string[] fieldsToCalculate = {
            "summonPrefab", "maxSummons", "limitBehavior", "lifetime", "statContainer", "healthBarPrefab", "isConstruct", 
            "seekBehavior", "followDistance", "slotOffsets", "stopDistance", "moveSpeed", "detectionRange", "attackRange", 
            "pathfindingObstacleLayers", "obstacleAvoidanceStrength", "debugDrawPathfindingRays", "summonAbilities", "isRotationalTurret",
            "idleAnimation", "moveAnimation", "spawnAnimation", "deathAnimation", "spawnOffset", "spawnEffectPrefab", "deathEffectPrefab"
        };

        foreach (var fieldName in fieldsToCalculate)
        {
            var prop = property.FindPropertyRelative(fieldName);
            if (prop != null)
            {
                height += EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        // Add additional layout pixel heights only if the turret overrides sub-box is checked active
        SerializedProperty isRotationalTurretProp = property.FindPropertyRelative("isRotationalTurret");
        if (isRotationalTurretProp != null && isRotationalTurretProp.boolValue)
        {
            var childProp = property.FindPropertyRelative("turretChildName");
            if (childProp != null) height += EditorGUI.GetPropertyHeight(childProp, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }
}
#endif
