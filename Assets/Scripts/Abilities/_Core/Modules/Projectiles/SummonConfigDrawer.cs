#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for SummonConfig.
/// Suppresses the damage / damageTypeName fields inside meleeConfig and projectileConfig
/// because those values are always driven by the parent SummonConfig-level fields.
/// </summary>
[CustomPropertyDrawer(typeof(SummonConfig))]
public class SummonConfigDrawer : PropertyDrawer
{
    // Sub-config field names whose damage/type are always overwritten at runtime.
    private static readonly string[] _suppressedFields = { "damage", "damageTypeName" };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float yPos = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // --- Summon Prefab ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("summonPrefab"), position, yPos);

            // --- Summon Limits ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("maxSummons"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("limitBehavior"), position, yPos);

            // --- Lifetime ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("lifetime"), position, yPos);

            // --- Health ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("maxHealth"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("healthBarPrefab"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("seekBehavior"), position, yPos);

            // --- Follow ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("followDistance"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("stopDistance"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("moveSpeed"), position, yPos);

            // --- Combat (parent-level — single source of truth for damage) ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("detectionRange"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("attackSpeed"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("damage"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("damageTypeName"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("attackRange"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("lifeSteal"), position, yPos, true);

            // --- Pathfinding ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("pathfindingObstacleLayers"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("obstacleAvoidanceStrength"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("debugDrawPathfindingRays"), position, yPos);

            // --- Attack Type + Sub-configs ---
            SerializedProperty attackType = property.FindPropertyRelative("attackType");
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(attackType, position, yPos);
            if (attackType.enumValueIndex == (int)SummonAttackType.Melee)
            {
                SerializedProperty meleeConfig = property.FindPropertyRelative("meleeConfig");
                yPos = DrawSubConfigWithoutDamage(meleeConfig, position, yPos,
                    new GUIContent("Melee Config", "Damage, DamageTypeName, and LifeSteal are driven by the SummonConfig parent fields above."));
            }
            else if (attackType.enumValueIndex == (int)SummonAttackType.Projectile)
            {
                SerializedProperty projectileConfig = property.FindPropertyRelative("projectileConfig");
                yPos = DrawSubConfigWithoutDamage(projectileConfig, position, yPos,
                    new GUIContent("Projectile Config", "Damage, DamageTypeName, and LifeSteal are driven by the SummonConfig parent fields above."));
            }
            else if (attackType.enumValueIndex == (int)SummonAttackType.Beam)
            {
                SerializedProperty beamConfig = property.FindPropertyRelative("beamConfig");
                yPos = DrawSubConfigWithoutDamage(beamConfig, position, yPos,
                    new GUIContent("Beam Config", "LifeSteal is driven by the SummonConfig parent field above."));
            }

            // --- Animations ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("idleAnimation"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("moveAnimation"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("attackAnimation"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("attackTriggerNormalizedTime"), position, yPos);

            // --- Spawn ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spawnOffset"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spawnAnimation"), position, yPos);

            // --- Visual Effects ---
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spawnEffectPrefab"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("deathEffectPrefab"), position, yPos);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (!property.isExpanded)
            return height;

        height += EditorGUIUtility.standardVerticalSpacing;

        string[] topLevelFields =
        {
            "summonPrefab",
            "maxSummons", "limitBehavior",
            "lifetime",
            "maxHealth", "healthBarPrefab", "seekBehavior",
            "followDistance", "stopDistance", "moveSpeed",
            "detectionRange", "attackSpeed", "damage", "damageTypeName", "attackRange",
            "attackType",
            "lifeSteal",
            "pathfindingObstacleLayers", "obstacleAvoidanceStrength", "debugDrawPathfindingRays",
            "idleAnimation", "moveAnimation", "attackAnimation", "attackTriggerNormalizedTime",
            "spawnOffset", "spawnAnimation",
            "spawnEffectPrefab", "deathEffectPrefab"
        };

        foreach (var fieldName in topLevelFields)
        {
            var prop = property.FindPropertyRelative(fieldName);
            if (prop != null)
                height += EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Sub-config heights (without damage + damageTypeName + lifeSteal)
        height += GetSubConfigHeightWithoutDamage(property.FindPropertyRelative("meleeConfig"));
        height += GetSubConfigHeightWithoutDamage(property.FindPropertyRelative("projectileConfig"));
        height += GetSubConfigHeightWithoutDamage(property.FindPropertyRelative("beamConfig"));

        return height;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Draws a sub-config (MeleeConfig / ProjectileConfig) as a foldout, omitting
    /// the "damage" and "damageTypeName" child fields.
    /// </summary>
    private static float DrawSubConfigWithoutDamage(SerializedProperty prop, Rect position, float yPos, GUIContent label)
    {
        if (prop == null)
            return yPos;

        Rect foldoutRect = new Rect(position.x + EditorGUI.indentLevel * 15f, yPos, position.width, EditorGUIUtility.singleLineHeight);
        prop.isExpanded = EditorGUI.Foldout(foldoutRect, prop.isExpanded, label, true);
        yPos += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (!prop.isExpanded)
            return yPos;

        EditorGUI.indentLevel++;

        SerializedProperty child = prop.Copy();
        SerializedProperty endProp = prop.GetEndProperty();
        bool enterChildren = true;

        while (child.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (SerializedProperty.EqualContents(child, endProp))
                break;

            if (child.name == "damage" || child.name == "damageTypeName" || child.name == "lifeSteal")
                continue;
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(child, true)), child, true);
            yPos += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        EditorGUI.indentLevel--;
        return yPos;
    }

    private static float GetSubConfigHeightWithoutDamage(SerializedProperty prop)
    {
        if (prop == null)
            return 0f;

        // Always reserve height for the foldout header
        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (!prop.isExpanded)
            return height;

        SerializedProperty child = prop.Copy();
        SerializedProperty endProp = prop.GetEndProperty();
        bool enterChildren = true;

        while (child.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (SerializedProperty.EqualContents(child, endProp))
                break;

            if (child.name == "damage" || child.name == "damageTypeName" || child.name == "lifeSteal")
                continue;

            height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }
}
#endif
