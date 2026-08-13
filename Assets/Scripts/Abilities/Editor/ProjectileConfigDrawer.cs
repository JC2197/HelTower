using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom property drawer for ProjectileConfig to handle conditional field visibility
/// based on behavior type and feature toggles.
/// </summary>
[CustomPropertyDrawer(typeof(ProjectileConfig))]
public class ProjectileConfigDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw foldout
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            float yPos = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Hitbox (prefab, scale, layers, damage, weapon damage, knockback, pull, on-hit effects, feedback, life steal)
            SerializedProperty hitbox = property.FindPropertyRelative("hitbox");
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(hitbox, true)), hitbox, true);
            yPos += EditorGUI.GetPropertyHeight(hitbox, true) + EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty dealsDamageOverTime = property.FindPropertyRelative("dealsDamageOverTime");
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("dealsDamageOverTime"), position, yPos);

            if (dealsDamageOverTime.boolValue)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("damagePerTick"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("dotInterval"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("dotDuration"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("dotParticleEffectPrefab"), position, yPos);
                SerializedProperty dotParticleEffectPrefab = property.FindPropertyRelative("dotParticleEffectPrefab");

                if (dotParticleEffectPrefab.objectReferenceValue != null)
                {
                    yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("startParticlesFromFeet"), position, yPos);

                }
            }

            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("speed"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("useLifetime"), position, yPos);
            SerializedProperty useLifetime = property.FindPropertyRelative("useLifetime");
            if (useLifetime.boolValue)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("lifetime"), position, yPos);
            }
            else
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("maxRange"), position, yPos);
            }
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("behavior"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("targetingMode"), position, yPos);
            SerializedProperty behavior = property.FindPropertyRelative("behavior");
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("chargeDamageMultiplier"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("canCancelCharge"), position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("hasMultiShot"), position, yPos);
            SerializedProperty hasMultiShot = property.FindPropertyRelative("hasMultiShot");
            if (hasMultiShot.boolValue)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("projectileCount"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spreadAngle"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spreadAnglePerProjectile"), position, yPos);
            }
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("salvoSize"), position, yPos);
            SerializedProperty salvoSize = property.FindPropertyRelative("salvoSize");
            if (salvoSize.intValue > 1)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("salvoInterval"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("salvoAngle"), position, yPos);

            }
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("hasPierce"), position, yPos);
            SerializedProperty hasPierce = property.FindPropertyRelative("hasPierce");

            if (hasPierce.boolValue)
            {
                SerializedProperty pierceCount = property.FindPropertyRelative("pierceCount");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(pierceCount)), pierceCount);
                yPos += EditorGUI.GetPropertyHeight(pierceCount) + EditorGUIUtility.standardVerticalSpacing;
            }
            SerializedProperty hasChaining = property.FindPropertyRelative("hasChaining");
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("hasChaining"), position, yPos);

            if (hasChaining.boolValue)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("maxChains"), position, yPos);
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("chainRange"), position, yPos);
            }

            // Behavior-specific settings
            ProjectileBehavior behaviorValue = (ProjectileBehavior)behavior.enumValueIndex;

            // Homing Settings (only for Homing behavior)
            if (behaviorValue == ProjectileBehavior.Homing)
            {
                SerializedProperty homingStrength = property.FindPropertyRelative("homingStrength");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(homingStrength)), homingStrength);
                yPos += EditorGUI.GetPropertyHeight(homingStrength) + EditorGUIUtility.standardVerticalSpacing;
            }

            // Lobbed Settings (only for Lobbed behavior)
            if (behaviorValue == ProjectileBehavior.Lobbed)
            {
                yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("lobbedArcHeight"), position, yPos);
            }

            // Wave Settings (only for Wave behavior)
            if (behaviorValue == ProjectileBehavior.Wave)
            {
                SerializedProperty waveAmplitude = property.FindPropertyRelative("waveAmplitude");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(waveAmplitude)), waveAmplitude);
                yPos += EditorGUI.GetPropertyHeight(waveAmplitude) + EditorGUIUtility.standardVerticalSpacing;

                SerializedProperty waveFrequency = property.FindPropertyRelative("waveFrequency");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(waveFrequency)), waveFrequency);
                yPos += EditorGUI.GetPropertyHeight(waveFrequency) + EditorGUIUtility.standardVerticalSpacing;
            }

            // Spiral Settings (only for Spiral behavior)
            if (behaviorValue == ProjectileBehavior.Spiral)
            {

                SerializedProperty spiralRadius = property.FindPropertyRelative("spiralRadius");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(spiralRadius)), spiralRadius);
                yPos += EditorGUI.GetPropertyHeight(spiralRadius) + EditorGUIUtility.standardVerticalSpacing;

                SerializedProperty spiralSpeed = property.FindPropertyRelative("spiralSpeed");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(spiralSpeed)), spiralSpeed);
                yPos += EditorGUI.GetPropertyHeight(spiralSpeed) + EditorGUIUtility.standardVerticalSpacing;
            }

            // Boomerang Settings (only for Boomerang behavior)
            if (behaviorValue == ProjectileBehavior.Boomerang)
            {
                SerializedProperty boomerangDistanceCurve = property.FindPropertyRelative("boomerangDistanceCurve");
                if (boomerangDistanceCurve != null)
                {
                    EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(boomerangDistanceCurve)), boomerangDistanceCurve);
                    yPos += EditorGUI.GetPropertyHeight(boomerangDistanceCurve) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            // Rotation Settings
            SerializedProperty freezeRotation = property.FindPropertyRelative("freezeRotation");
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(freezeRotation, position, yPos);
            yPos = DrawerUtil.DrawPropertyAndAdvanceYPos(property.FindPropertyRelative("spinSpeed"), position, yPos);

            SerializedProperty allowOverride = property.FindPropertyRelative("allowOverride");
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(allowOverride)), allowOverride);
            yPos += EditorGUI.GetPropertyHeight(allowOverride) + EditorGUIUtility.standardVerticalSpacing;

            // Collision Layers
            SerializedProperty canPierceLayers = property.FindPropertyRelative("canPierceLayers");
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(canPierceLayers)), canPierceLayers);
            yPos += EditorGUI.GetPropertyHeight(canPierceLayers) + EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty destroyOnLayers = property.FindPropertyRelative("destroyOnLayers");
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(destroyOnLayers)), destroyOnLayers);
            yPos += EditorGUI.GetPropertyHeight(destroyOnLayers) + EditorGUIUtility.standardVerticalSpacing;

            // Destroy Effects
            SerializedProperty destroyVisualPrefab = property.FindPropertyRelative("destroyVisualPrefab");
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(destroyVisualPrefab)), destroyVisualPrefab);
            yPos += EditorGUI.GetPropertyHeight(destroyVisualPrefab) + EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty destroySound = property.FindPropertyRelative("destroySound");
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(destroySound)), destroySound);
            yPos += EditorGUI.GetPropertyHeight(destroySound) + EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty muzzleFlashPrefab = property.FindPropertyRelative("muzzleFlashPrefab");
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(muzzleFlashPrefab)), muzzleFlashPrefab);
            yPos += EditorGUI.GetPropertyHeight(muzzleFlashPrefab) + EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty muzzleFlashSound = property.FindPropertyRelative("muzzleFlashSound");
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(muzzleFlashSound)), muzzleFlashSound);
            yPos += EditorGUI.GetPropertyHeight(muzzleFlashSound) + EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty enableMuzzleLight = property.FindPropertyRelative("enableMuzzleLight");
            EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(enableMuzzleLight)), enableMuzzleLight);
            yPos += EditorGUI.GetPropertyHeight(enableMuzzleLight) + EditorGUIUtility.standardVerticalSpacing;

            if (enableMuzzleLight.boolValue)
            {
                EditorGUI.indentLevel++;

                SerializedProperty muzzleLightColor = property.FindPropertyRelative("muzzleLightColor");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(muzzleLightColor)), muzzleLightColor);
                yPos += EditorGUI.GetPropertyHeight(muzzleLightColor) + EditorGUIUtility.standardVerticalSpacing;

                SerializedProperty muzzleLightIntensity = property.FindPropertyRelative("muzzleLightIntensity");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(muzzleLightIntensity)), muzzleLightIntensity);
                yPos += EditorGUI.GetPropertyHeight(muzzleLightIntensity) + EditorGUIUtility.standardVerticalSpacing;

                SerializedProperty muzzleLightRange = property.FindPropertyRelative("muzzleLightRange");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(muzzleLightRange)), muzzleLightRange);
                yPos += EditorGUI.GetPropertyHeight(muzzleLightRange) + EditorGUIUtility.standardVerticalSpacing;

                SerializedProperty muzzleLightDuration = property.FindPropertyRelative("muzzleLightDuration");
                EditorGUI.PropertyField(new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(muzzleLightDuration)), muzzleLightDuration);
                yPos += EditorGUI.GetPropertyHeight(muzzleLightDuration) + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        // Get all property references
        SerializedProperty hitbox = property.FindPropertyRelative("hitbox");
        SerializedProperty dealsDamageOverTime = property.FindPropertyRelative("dealsDamageOverTime");
        SerializedProperty damagePerTick = property.FindPropertyRelative("damagePerTick");
        SerializedProperty dotInterval = property.FindPropertyRelative("dotInterval");
        SerializedProperty dotDuration = property.FindPropertyRelative("dotDuration");
        SerializedProperty dotParticleEffectPrefab = property.FindPropertyRelative("dotParticleEffectPrefab");
        SerializedProperty startParticlesFromFeet = property.FindPropertyRelative("startParticlesFromFeet");
        SerializedProperty speed = property.FindPropertyRelative("speed");
        SerializedProperty useLifetime = property.FindPropertyRelative("useLifetime");
        SerializedProperty lifetime = property.FindPropertyRelative("lifetime");
        SerializedProperty maxRange = property.FindPropertyRelative("maxRange");
        SerializedProperty behavior = property.FindPropertyRelative("behavior");
        SerializedProperty targetingMode = property.FindPropertyRelative("targetingMode");
        SerializedProperty chargeDamageMultiplier = property.FindPropertyRelative("chargeDamageMultiplier");
        SerializedProperty canCancelCharge = property.FindPropertyRelative("canCancelCharge");
        SerializedProperty hasMultiShot = property.FindPropertyRelative("hasMultiShot");
        SerializedProperty hasPierce = property.FindPropertyRelative("hasPierce");
        SerializedProperty projectileCount = property.FindPropertyRelative("projectileCount");
        SerializedProperty spreadAngle = property.FindPropertyRelative("spreadAngle");
        SerializedProperty spreadAnglePerProjectile = property.FindPropertyRelative("spreadAnglePerProjectile");
        SerializedProperty pierceCount = property.FindPropertyRelative("pierceCount");
        SerializedProperty hasChaining = property.FindPropertyRelative("hasChaining");
        SerializedProperty maxChains = property.FindPropertyRelative("maxChains");
        SerializedProperty chainRange = property.FindPropertyRelative("chainRange");
        SerializedProperty homingStrength = property.FindPropertyRelative("homingStrength");
        SerializedProperty waveAmplitude = property.FindPropertyRelative("waveAmplitude");
        SerializedProperty waveFrequency = property.FindPropertyRelative("waveFrequency");
        SerializedProperty spiralRadius = property.FindPropertyRelative("spiralRadius");
        SerializedProperty spiralSpeed = property.FindPropertyRelative("spiralSpeed");
        SerializedProperty allowOverride = property.FindPropertyRelative("allowOverride");
        SerializedProperty canPierceLayers = property.FindPropertyRelative("canPierceLayers");
        SerializedProperty destroyOnLayers = property.FindPropertyRelative("destroyOnLayers");
        SerializedProperty destroyVisualPrefab = property.FindPropertyRelative("destroyVisualPrefab");
        SerializedProperty destroySound = property.FindPropertyRelative("destroySound");

        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // Foldout

        // Hitbox
        height += EditorGUI.GetPropertyHeight(hitbox, true) + EditorGUIUtility.standardVerticalSpacing;

        height += EditorGUI.GetPropertyHeight(dealsDamageOverTime) + EditorGUIUtility.standardVerticalSpacing;

        if (dealsDamageOverTime.boolValue)
        {
            height += EditorGUI.GetPropertyHeight(damagePerTick) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(dotInterval) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(dotDuration) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(dotParticleEffectPrefab) + EditorGUIUtility.standardVerticalSpacing;

            if (dotParticleEffectPrefab.objectReferenceValue != null)
            {
                height += EditorGUI.GetPropertyHeight(startParticlesFromFeet) + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        // Movement Settings
        height += EditorGUI.GetPropertyHeight(speed) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(useLifetime) + EditorGUIUtility.standardVerticalSpacing;
        if (useLifetime.boolValue)
        {
            height += EditorGUI.GetPropertyHeight(lifetime) + EditorGUIUtility.standardVerticalSpacing;
        }
        else
        {
            height += EditorGUI.GetPropertyHeight(maxRange) + EditorGUIUtility.standardVerticalSpacing;
        }

        height += EditorGUI.GetPropertyHeight(behavior) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(targetingMode) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(chargeDamageMultiplier) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(canCancelCharge) + EditorGUIUtility.standardVerticalSpacing;

        // Feature Toggles
        height += EditorGUI.GetPropertyHeight(hasMultiShot) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(hasPierce) + EditorGUIUtility.standardVerticalSpacing;

        // Multi-Shot Settings (conditional)
        if (hasMultiShot.boolValue)
        {
            height += EditorGUI.GetPropertyHeight(projectileCount) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(spreadAngle) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(spreadAnglePerProjectile) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Salvo Settings
        SerializedProperty salvoSize = property.FindPropertyRelative("salvoSize");
        SerializedProperty salvoInterval = property.FindPropertyRelative("salvoInterval");
        SerializedProperty salvoAngle = property.FindPropertyRelative("salvoAngle");
        height += EditorGUI.GetPropertyHeight(salvoSize) + EditorGUIUtility.standardVerticalSpacing;
        if (salvoSize.intValue > 1)
        {
            height += EditorGUI.GetPropertyHeight(salvoInterval) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(salvoAngle) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Chaining
        height += EditorGUI.GetPropertyHeight(hasChaining) + EditorGUIUtility.standardVerticalSpacing;

        if (hasChaining.boolValue)
        {
            height += EditorGUI.GetPropertyHeight(maxChains) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(chainRange) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Behavior-specific settings
        ProjectileBehavior behaviorValue = (ProjectileBehavior)behavior.enumValueIndex;

        // Homing Settings
        if (behaviorValue == ProjectileBehavior.Homing)
        {
            height += EditorGUI.GetPropertyHeight(homingStrength) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Lobbed Settings
        if (behaviorValue == ProjectileBehavior.Lobbed)
        {
            SerializedProperty lobbedArcHeight = property.FindPropertyRelative("lobbedArcHeight");
            height += EditorGUI.GetPropertyHeight(lobbedArcHeight) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Wave Settings
        if (behaviorValue == ProjectileBehavior.Wave)
        {
            height += EditorGUI.GetPropertyHeight(waveAmplitude) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(waveFrequency) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Spiral Settings
        if (behaviorValue == ProjectileBehavior.Spiral)
        {
            height += EditorGUI.GetPropertyHeight(spiralRadius) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(spiralSpeed) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Boomerang Settings
        if (behaviorValue == ProjectileBehavior.Boomerang)
        {
            SerializedProperty boomerangDistanceCurve = property.FindPropertyRelative("boomerangDistanceCurve");
            if (boomerangDistanceCurve != null)
                height += EditorGUI.GetPropertyHeight(boomerangDistanceCurve) + EditorGUIUtility.standardVerticalSpacing;
        }

        // Rotation Settings
        SerializedProperty freezeRotation = property.FindPropertyRelative("freezeRotation");
        SerializedProperty spinSpeed = property.FindPropertyRelative("spinSpeed");
        height += EditorGUI.GetPropertyHeight(freezeRotation) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(spinSpeed) + EditorGUIUtility.standardVerticalSpacing;

        // Collision Layers
        height += EditorGUI.GetPropertyHeight(canPierceLayers) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(destroyOnLayers) + EditorGUIUtility.standardVerticalSpacing;

        // Destroy Effects
        height += EditorGUI.GetPropertyHeight(destroyVisualPrefab) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(destroySound) + EditorGUIUtility.standardVerticalSpacing;

        // Muzzle Flash Effects
        SerializedProperty muzzleFlashPrefab = property.FindPropertyRelative("muzzleFlashPrefab");
        SerializedProperty muzzleFlashSound = property.FindPropertyRelative("muzzleFlashSound");
        SerializedProperty enableMuzzleLight = property.FindPropertyRelative("enableMuzzleLight");
        SerializedProperty muzzleLightColor = property.FindPropertyRelative("muzzleLightColor");
        SerializedProperty muzzleLightIntensity = property.FindPropertyRelative("muzzleLightIntensity");
        SerializedProperty muzzleLightRange = property.FindPropertyRelative("muzzleLightRange");
        SerializedProperty muzzleLightDuration = property.FindPropertyRelative("muzzleLightDuration");

        height += EditorGUI.GetPropertyHeight(muzzleFlashPrefab) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(muzzleFlashSound) + EditorGUIUtility.standardVerticalSpacing;

        height += EditorGUI.GetPropertyHeight(enableMuzzleLight) + EditorGUIUtility.standardVerticalSpacing;

        if (enableMuzzleLight.boolValue)
        {
            height += EditorGUI.GetPropertyHeight(muzzleLightColor) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(muzzleLightIntensity) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(muzzleLightRange) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(muzzleLightDuration) + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }
}
