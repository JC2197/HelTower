using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom inspector for AbilityDataConfig with conditional drawers
/// Shows/hides sections based on boolean flags
/// </summary>
[CustomEditor(typeof(AbilityDataConfig))]
public class AbilityDataConfigEditor : Editor
{
    private SerializedProperty abilityName;
    private SerializedProperty abilityIcon;
    private SerializedProperty abilityDescription;
    private SerializedProperty abilityTags;

    // Weapon Requirements
    private SerializedProperty requiredWeaponTypes;

    // Mechanical properties
    private SerializedProperty disableCast;
    private SerializedProperty isAttack;
    private SerializedProperty attackSpeed;
    private SerializedProperty cooldownTime;
    private SerializedProperty energyCost;
    private SerializedProperty baseCritChance;
    private SerializedProperty baseCritDamageMultiplier;
    private SerializedProperty autocast;
    private SerializedProperty retaliationCast;
    private SerializedProperty castAtFeet;
    private SerializedProperty autocastRange;
    private SerializedProperty autocastTargets;
    private SerializedProperty disablesMovementDuringCast;
    private SerializedProperty movementBlockDuration;
    private SerializedProperty unlockWeaponDirections;
    private SerializedProperty rotationLockDuration;
    private SerializedProperty continueRotatingDuringUnlock;
    private SerializedProperty flipYOnLeftFacing;
    private SerializedProperty flipXOnLeftFacing;
    private SerializedProperty characterAnimationName;
    private SerializedProperty characterAnimationUp;
    private SerializedProperty mainhandAnimationName;
    private SerializedProperty offhandAnimationName;
    private SerializedProperty weaponIdleAnimationName;
    private SerializedProperty hasPrecast;
    private SerializedProperty preAnimationName;
    private SerializedProperty activateOnButtonRelease;
    private SerializedProperty holdAnimationName;
    private SerializedProperty holdChargeConfig;
    private SerializedProperty hasCharges;
    private SerializedProperty maxCharges;
    private SerializedProperty chargeRechargeTime;
    private SerializedProperty isCombo;
    private SerializedProperty comboAbilities;
    private SerializedProperty comboStepDelays;
    private SerializedProperty comboInputWindow;
    private SerializedProperty movementSpeedMultiplierDuringCast;
    // Type flags
    private SerializedProperty isProjectileAbility;
    private SerializedProperty isAreaAbility;
    private SerializedProperty isConstructAbility;
    private SerializedProperty isTrapAbility;
    private SerializedProperty isMovementAbility;
    private SerializedProperty areaFollowsProjectile;
    private SerializedProperty isChanneled;
    private SerializedProperty isBeamAbility;
    private SerializedProperty isMeleeAbility;
    private SerializedProperty isExplosionAbility;
    private SerializedProperty isSummonAbility;
    private SerializedProperty isPassiveAbility;
    // Ammo properties
    private SerializedProperty usesAmmo;

    // Data properties
    private SerializedProperty weaponData;
    private SerializedProperty projectileConfig;
    private SerializedProperty onHitEffects;
    private SerializedProperty onKillEffects;
    private SerializedProperty areaConfig;
    private SerializedProperty beamConfig;
    private SerializedProperty channelConfig;
    private SerializedProperty meleeConfig;
    private SerializedProperty constructConfig;
    private SerializedProperty trapConfig;
    private SerializedProperty explosionConfig;
    private SerializedProperty passiveConfig;
    private SerializedProperty summonConfig;
    private SerializedProperty movementConfig;
    private SerializedProperty onEnterEffects;
    private SerializedProperty lingeringEffects;
    private SerializedProperty onExitEffects;
    private SerializedProperty castEffects;
    private SerializedProperty timedParticles;

    // Hit Visual properties
    private SerializedProperty hitVisualPrefab;
    private SerializedProperty hitVisualSound;
    private SerializedProperty hitFlashColor;
    // Foldout states
    private bool showBaseSettings = true;
    private bool showTypeFlags = true;
    private bool showWeaponConfig = true;
    private bool showProjectileConfig = true;
    private bool showAreaConfig = true;
    private bool showConstructConfig = true;
    private bool showTrapConfig = true;
    private bool showMeleeConfig = true;
    private bool showExplosionConfig = true;
    private bool showSummonConfig = true;
    private bool showMovementConfig = true;
    private bool showChannelConfig = true;
    private bool showCastEffects = true;
    private bool showHitVisuals = true;
    private bool showPassiveConfig = true;


    private void OnEnable()
    {
        // UI properties (from base AbilityConfig)
        abilityName = serializedObject.FindProperty("abilityName");
        abilityIcon = serializedObject.FindProperty("abilityIcon");
        abilityDescription = serializedObject.FindProperty("abilityDescription");
        abilityTags = serializedObject.FindProperty("abilityTags");

        // Weapon Requirements
        requiredWeaponTypes = serializedObject.FindProperty("requiredWeaponTypes");

        // Mechanical properties (from AbilityDataConfig)
        disableCast = serializedObject.FindProperty("disableCast");
        isAttack = serializedObject.FindProperty("isAttack");
        attackSpeed = serializedObject.FindProperty("attackSpeed");
        cooldownTime = serializedObject.FindProperty("cooldownTime");
        energyCost = serializedObject.FindProperty("energyCost");
        baseCritChance = serializedObject.FindProperty("baseCritChance");
        baseCritDamageMultiplier = serializedObject.FindProperty("baseCritDamageMultiplier");
        autocast = serializedObject.FindProperty("autocast");
        retaliationCast = serializedObject.FindProperty("retaliationCast");
        castAtFeet = serializedObject.FindProperty("castAtFeet");
        autocastRange = serializedObject.FindProperty("autocastRange");
        autocastTargets = serializedObject.FindProperty("autocastTargets");
        disablesMovementDuringCast = serializedObject.FindProperty("disablesMovementDuringCast");
        movementBlockDuration = serializedObject.FindProperty("movementBlockDuration");
        unlockWeaponDirections = serializedObject.FindProperty("unlockWeaponDirections");
        rotationLockDuration = serializedObject.FindProperty("rotationLockDuration");
        continueRotatingDuringUnlock = serializedObject.FindProperty("continueRotatingDuringUnlock");
        flipYOnLeftFacing = serializedObject.FindProperty("flipYOnLeftFacing");
        flipXOnLeftFacing = serializedObject.FindProperty("flipXOnLeftFacing");
        characterAnimationName = serializedObject.FindProperty("characterAnimationName");
        characterAnimationUp = serializedObject.FindProperty("characterAnimationUp");
        mainhandAnimationName = serializedObject.FindProperty("mainhandAnimationName");
        offhandAnimationName = serializedObject.FindProperty("offhandAnimationName");
        hasPrecast = serializedObject.FindProperty("hasPrecast");
        weaponIdleAnimationName = serializedObject.FindProperty("weaponIdleAnimationName");
        preAnimationName = serializedObject.FindProperty("preAnimationName");
        activateOnButtonRelease = serializedObject.FindProperty("activateOnButtonRelease");
        holdAnimationName = serializedObject.FindProperty("holdAnimationName");
        holdChargeConfig = serializedObject.FindProperty("holdChargeConfig");
        hasCharges = serializedObject.FindProperty("hasCharges");
        maxCharges = serializedObject.FindProperty("maxCharges");
        chargeRechargeTime = serializedObject.FindProperty("chargeRechargeTime");
        isCombo = serializedObject.FindProperty("isCombo");
        comboAbilities = serializedObject.FindProperty("comboAbilities");
        comboStepDelays = serializedObject.FindProperty("comboStepDelays");
        comboInputWindow = serializedObject.FindProperty("comboInputWindow");
        movementSpeedMultiplierDuringCast = serializedObject.FindProperty("movementSpeedMultiplierDuringCast");
        // Type flags
        isProjectileAbility = serializedObject.FindProperty("isProjectileAbility");
        isAreaAbility = serializedObject.FindProperty("isAreaAbility");
        isConstructAbility = serializedObject.FindProperty("isConstructAbility");
        isTrapAbility = serializedObject.FindProperty("isTrapAbility");
        isMovementAbility = serializedObject.FindProperty("isMovementAbility");
        areaFollowsProjectile = serializedObject.FindProperty("areaFollowsProjectile");
        isChanneled = serializedObject.FindProperty("isChanneled");
        isBeamAbility = serializedObject.FindProperty("isBeamAbility");
        isMeleeAbility = serializedObject.FindProperty("isMeleeAbility");
        isExplosionAbility = serializedObject.FindProperty("isExplosionAbility");
        isSummonAbility = serializedObject.FindProperty("isSummonAbility");
        isPassiveAbility = serializedObject.FindProperty("isPassiveAbility");
        // Ammo properties
        usesAmmo = serializedObject.FindProperty("usesAmmo");

        // Data
        weaponData = serializedObject.FindProperty("weaponData");
        projectileConfig = serializedObject.FindProperty("projectileConfig");
        onHitEffects = serializedObject.FindProperty("onHitEffects");
        onKillEffects = serializedObject.FindProperty("onKillEffects");
        areaConfig = serializedObject.FindProperty("areaConfig");
        beamConfig = serializedObject.FindProperty("beamConfig");
        channelConfig = serializedObject.FindProperty("channelConfig");
        meleeConfig = serializedObject.FindProperty("meleeConfig");
        constructConfig = serializedObject.FindProperty("constructConfig");
        trapConfig = serializedObject.FindProperty("trapConfig");
        explosionConfig = serializedObject.FindProperty("explosionConfig");
        summonConfig = serializedObject.FindProperty("summonConfig");
        passiveConfig = serializedObject.FindProperty("passiveConfig");
        movementConfig = serializedObject.FindProperty("movementConfig");
        onEnterEffects = serializedObject.FindProperty("onEnterEffects");
        lingeringEffects = serializedObject.FindProperty("lingeringEffects");
        onExitEffects = serializedObject.FindProperty("onExitEffects");
        castEffects = serializedObject.FindProperty("castEffects");
        timedParticles = serializedObject.FindProperty("timedParticles");

        // Hit Visuals
        hitVisualPrefab = serializedObject.FindProperty("hitVisualPrefab");
        hitVisualSound = serializedObject.FindProperty("hitVisualSound");
        hitFlashColor = serializedObject.FindProperty("hitFlashColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(5);

        // ── Keybind Classification Banner ──────────────────────────────────────
        DrawKeybindBanner();

        EditorGUILayout.Space(5);

        // Base Settings
        DrawBaseSettings();

        EditorGUILayout.Space(10);

        // Type Flags
        DrawTypeFlags();

        EditorGUILayout.Space(10);

        // Hide all type-specific configurations when combo is enabled
        if (isCombo.boolValue)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (isProjectileAbility.boolValue)
        {
            DrawProjectileConfiguration();
            EditorGUILayout.Space(10);
        }

        if (isAreaAbility.boolValue)
        {
            DrawAreaConfiguration();
            EditorGUILayout.Space(10);
        }

        if (isConstructAbility.boolValue)
        {
            DrawConstructConfiguration();
            EditorGUILayout.Space(10);
        }

        if (isTrapAbility.boolValue)
        {
            DrawTrapConfiguration();
            EditorGUILayout.Space(10);
        }

        if (isMovementAbility.boolValue)
        {
            DrawMovementConfiguration();
            EditorGUILayout.Space(10);
        }

        if (isBeamAbility.boolValue)
        {
            DrawBeamConfiguration();
            EditorGUILayout.Space(10);
        }

        if (isChanneled.boolValue)
        {
            DrawChannelConfiguration();
            EditorGUILayout.Space(10);
        }

        if (isMeleeAbility.boolValue)
        {
            DrawMeleeConfiguration();
            EditorGUILayout.Space(10);
        }

        if (isExplosionAbility.boolValue)
        {
            DrawExplosionConfiguration();
            EditorGUILayout.Space(10);
        }

        if (isSummonAbility.boolValue)
        {
            DrawSummonConfiguration();
            EditorGUILayout.Space(10);
        }
        if (isPassiveAbility.boolValue)
        {
            DrawPassiveConfiguration();
            EditorGUILayout.Space(10);
        }
        // Only show cast effects if not a combo (each ability in chain has its own effects)
        if (!isCombo.boolValue)
        {
            DrawCastEffects();
        }

        // Hit Visuals (always shown — applies to all ability types)
        DrawHitVisuals();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawKeybindBanner()
    {
        bool auraEnabled = areaConfig?.FindPropertyRelative("isAura")?.boolValue == true;
        bool isAutocast = autocast.boolValue;
        bool isRetaliation = retaliationCast.boolValue;
        bool isPassive = isPassiveAbility.boolValue;
        bool castingDisabled = disableCast.boolValue;

        string label;
        MessageType msgType;

        if (auraEnabled)
        {
            label = "PASSIVE \u2014 No keybind. Activates automatically via PlayerAuraManager. Does not appear in the HUD.";
            msgType = MessageType.None;
        }
        else if (isAutocast)
        {
            label = "AUTOCAST \u2014 No keybind. Fires automatically at the nearest valid target. Does not appear in the HUD.";
            msgType = MessageType.None;
        }
        else if (isRetaliation)
        {
            label = "RETALIATION \u2014 No keybind. Fires at whoever hits the owner when damage is taken. Does not appear in the HUD.";
            msgType = MessageType.None;
        }
        else if (isPassive)
        {
            label = "PASSIVE \u2014 No keybind. Always active. Does not appear in the HUD.";
            msgType = MessageType.None;
        }
        else if (castingDisabled)
        {
            label = "CAST DISABLED \u2014 No manual keybind. This ability can only run through system-driven behavior.";
            msgType = MessageType.None;
        }
        else
        {
            label = "ACTIVE \u2014 Requires a keybind. Weapon abilities use LMB; trait actives receive keys 1\u20136 in order.";
            msgType = MessageType.Info;
        }

        EditorGUILayout.HelpBox(label, msgType);
    }

    private void DrawBaseSettings()
    {
        showBaseSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showBaseSettings, "Base Ability Settings");
        if (showBaseSettings)
        {
            EditorGUI.indentLevel++;

            // UI Display (from base AbilityConfig)
            EditorGUILayout.LabelField("UI Display", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(abilityName);
            EditorGUILayout.PropertyField(abilityIcon);
            EditorGUILayout.PropertyField(abilityDescription);

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(abilityTags);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Weapon Requirements", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(requiredWeaponTypes, true);

            EditorGUILayout.Space(5);

            // Mechanical Properties (from AbilityDataConfig)
            EditorGUILayout.PropertyField(disableCast, new GUIContent("Disable Cast", "Prevents manual casting. Permanent auras and other system-driven behavior can still initialize."));
            EditorGUILayout.PropertyField(isAttack, new GUIContent("Is Attack (vs Spell)"));
            if (isAttack.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(attackSpeed, new GUIContent("Attack Speed (attacks/sec)"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(cooldownTime, new GUIContent("Cooldown"));
            EditorGUILayout.PropertyField(energyCost);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Crit", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(baseCritChance, new GUIContent("Base Crit Chance", "Base crit chance for this ability (fraction: 0.05 = 5%). Added to the character's CritChance stat and any trait CritChance modifiers."));
            EditorGUILayout.PropertyField(baseCritDamageMultiplier, new GUIContent("Base Crit Damage Multiplier", "Bonus crit damage added on top of the character's CritDamage stat when this ability crits (e.g. 0.5 = +50% crit damage)."));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Autocast", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autocast, new GUIContent("Autocast", "Automatically cast on valid enemies in range — no keybind assigned"));
            EditorGUILayout.PropertyField(castAtFeet, new GUIContent("Cast At Feet", "Cast at player position instead of enemy position"));
            if (autocast.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Autocast abilities have no keybind. They fire automatically at nearest valid target.", MessageType.Info);
                EditorGUILayout.PropertyField(autocastRange, new GUIContent("Autocast Range", "Range to search for enemies. Uses projectile maxRange if 0."));
                EditorGUILayout.PropertyField(autocastTargets, new GUIContent("Autocast Targets", "How many unique enemies to target per autocast cycle. Each gets one cast."));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(retaliationCast, new GUIContent("Retaliation Cast", "Cast at whoever just hit the owner when they take damage — no keybind assigned"));

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(disablesMovementDuringCast);
            if (disablesMovementDuringCast.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(movementBlockDuration, new GUIContent("Movement Block Duration (s)"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(weaponIdleAnimationName, new GUIContent("Weapon Idle Animation"));
            // Hide animations when combo is enabled (each ability in chain has its own)
            if (!isCombo.boolValue)
            {
                EditorGUILayout.PropertyField(characterAnimationName, new GUIContent("Character Animation"));
                EditorGUILayout.PropertyField(characterAnimationUp, new GUIContent("Character Animation Up"));
                EditorGUILayout.PropertyField(unlockWeaponDirections, new GUIContent("Unlock Weapon Directions"));
                if (unlockWeaponDirections.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(rotationLockDuration, new GUIContent("Rotation Lock Duration", "Duration to freeze weapon rotation after firing. Flipping still applies."));
                    EditorGUILayout.PropertyField(continueRotatingDuringUnlock, new GUIContent("Continue Rotation During Unlock", "Keep following live aim while the weapon is unlocked instead of freezing to the first unlocked angle."));
                    EditorGUILayout.PropertyField(flipYOnLeftFacing, new GUIContent("Flip Y On Left Facing", "Flip the Y-axis of the weapon sprite when facing left"));
                    EditorGUILayout.PropertyField(flipXOnLeftFacing, new GUIContent("Flip X On Left Facing", "Flip the X-axis of the weapon sprite when facing left"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(mainhandAnimationName, new GUIContent("Mainhand Animation"));
                EditorGUILayout.PropertyField(offhandAnimationName, new GUIContent("Offhand Animation"));
                EditorGUILayout.PropertyField(hasPrecast, new GUIContent("Has Pre-Cast Animation"));
                if (hasPrecast.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(preAnimationName, new GUIContent("Pre-Cast Animation"));
                    EditorGUILayout.PropertyField(activateOnButtonRelease, new GUIContent("Activate On Button Release", "Flow: precast -> hold animation (looping while held) -> release -> cast animation."));
                    if (activateOnButtonRelease.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(holdAnimationName, new GUIContent("Hold Animation", "Looping animation played on weapon while button is held."));
                        EditorGUILayout.PropertyField(holdChargeConfig, new GUIContent("Hold Charge Config", "Bar duration, overcharge bars, and per-bar field modifiers. Uses the same property paths as trait ability modifiers."), true);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Timed Particle Effects", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(timedParticles, new GUIContent("Particle Spawns"), true);
            }
            EditorGUILayout.PropertyField(hasCharges);
            if (hasCharges.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(maxCharges);
                EditorGUILayout.PropertyField(chargeRechargeTime);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(movementSpeedMultiplierDuringCast, new GUIContent("Movement Speed Multiplier During Cast", "Multiplier applied to movement speed while casting this ability."));
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(isCombo, new GUIContent("Has Combo Chain"));
            if (isCombo.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "This ability acts as a shell and casts Combo Abilities in order when used. " +
                    "Combo Step Delays are applied between steps.",
                    MessageType.Info
                );
                EditorGUILayout.PropertyField(comboAbilities, new GUIContent("Combo Abilities"), true);
                EditorGUILayout.PropertyField(comboStepDelays, new GUIContent("Combo Step Delays", "Time to wait after each combo step's animation completes before advancing to the next step (in seconds). Array length should match combo abilities length."), true);
                EditorGUILayout.PropertyField(comboInputWindow, new GUIContent("Combo Input Window", "How long the player has to trigger the next combo step after a step completes (seconds)."));
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawTypeFlags()
    {
        // Hide ability types when combo is enabled (each ability in chain has its own type)
        if (isCombo.boolValue)
        {
            return;
        }

        showTypeFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showTypeFlags, "Ability Type");
        if (showTypeFlags)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(isProjectileAbility, new GUIContent("Is Projectile Ability"));
            EditorGUILayout.PropertyField(isAreaAbility, new GUIContent("Is Area Ability"));
            EditorGUILayout.PropertyField(isConstructAbility, new GUIContent("Is Construct Ability"));
            EditorGUILayout.PropertyField(isTrapAbility, new GUIContent("Is Trap Ability"));
            EditorGUILayout.PropertyField(isMovementAbility, new GUIContent("Is Movement Ability"));
            if (isProjectileAbility.boolValue && isAreaAbility.boolValue)
            {
                EditorGUILayout.PropertyField(areaFollowsProjectile, new GUIContent("Area Follows Projectile"));
            }

            EditorGUILayout.PropertyField(isChanneled, new GUIContent("Is Channeled"));
            EditorGUILayout.PropertyField(isBeamAbility, new GUIContent("Is Beam Ability"));
            EditorGUILayout.PropertyField(isMeleeAbility, new GUIContent("Is Melee Ability"));
            EditorGUILayout.PropertyField(isExplosionAbility, new GUIContent("Is Explosion Ability"));
            EditorGUILayout.PropertyField(isSummonAbility, new GUIContent("Is Summon Ability"));
            EditorGUILayout.PropertyField(usesAmmo, new GUIContent("Uses Ammo"));
            if (usesAmmo.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Magazine size, reload time, and ammo icon are configured on the WeaponConfig asset (Ammo System section).", MessageType.Info);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(isPassiveAbility, new GUIContent("Is Passive Ability (Always-On)"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawProjectileConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showProjectileConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showProjectileConfig, "PROJECTILE CONFIGURATION");

        if (showProjectileConfig)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(projectileConfig, new GUIContent("Projectile Config"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawAreaConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showAreaConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showAreaConfig, "AREA CONFIGURATION");

        if (showAreaConfig)
        {
            EditorGUI.indentLevel++;

            if (areaConfig.FindPropertyRelative("isAura").boolValue)
            {
                EditorGUILayout.HelpBox(
                    "This area is initialized once as a permanent aura. Size and shape come from the hitbox prefab/config.",
                    MessageType.Info
                );
            }

            EditorGUILayout.PropertyField(areaConfig, new GUIContent("Area Config"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawConstructConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showConstructConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showConstructConfig, "CONSTRUCT CONFIGURATION");

        if (showConstructConfig)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(constructConfig, new GUIContent("Construct Config"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawTrapConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showTrapConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showTrapConfig, "TRAP CONFIGURATION");

        if (showTrapConfig)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(trapConfig, new GUIContent("Trap Config"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawMovementConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showMovementConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showMovementConfig, "MOVEMENT CONFIGURATION");

        if (showMovementConfig)
        {
            EditorGUI.indentLevel++;

            SerializedProperty mc = movementConfig;
            EditorGUILayout.PropertyField(mc.FindPropertyRelative("movementType"), new GUIContent("Movement Type"));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(mc.FindPropertyRelative("towardMouse"), new GUIContent("Toward Mouse"));
            EditorGUILayout.PropertyField(mc.FindPropertyRelative("awayFromMouse"), new GUIContent("Away From Mouse"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Dash / Evade", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(mc.FindPropertyRelative("isDashing"), new GUIContent("Is Dashing", "Grants i-frames (evade) for the duration."));

            EditorGUILayout.Space(4);
            MovementType movementType = (MovementType)mc.FindPropertyRelative("movementType").intValue;
            switch (movementType)
            {
                case MovementType.DistanceOverTime:
                    EditorGUILayout.PropertyField(mc.FindPropertyRelative("distance"), new GUIContent("Dash Distance"));
                    EditorGUILayout.PropertyField(mc.FindPropertyRelative("duration"), new GUIContent("Dash Duration"));
                    EditorGUILayout.PropertyField(mc.FindPropertyRelative("lerp"), new GUIContent("Lerp"));
                    break;
                case MovementType.SpeedOverTime:
                    EditorGUILayout.PropertyField(mc.FindPropertyRelative("speed"), new GUIContent("Dash Speed"));
                    EditorGUILayout.PropertyField(mc.FindPropertyRelative("duration"), new GUIContent("Dash Duration"));
                    EditorGUILayout.PropertyField(mc.FindPropertyRelative("lerp"), new GUIContent("Lerp"));
                    break;
                case MovementType.Teleport:
                    EditorGUILayout.PropertyField(mc.FindPropertyRelative("distance"), new GUIContent("Teleport Distance"));
                    break;
            }

           

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private bool showBeamConfig = true;

    private void DrawBeamConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showBeamConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showBeamConfig, "BEAM CONFIGURATION");

        if (showBeamConfig)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(beamConfig, new GUIContent("Beam Config"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawChannelConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showChannelConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showChannelConfig, "CHANNEL CONFIGURATION");

        if (showChannelConfig)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox(
                "Channel abilities activate on button press and continue while held.\n" +
                "The channel object spawns at weapon tip and rotates toward cursor.\n" +
                "Energy is consumed and damage is dealt at configured tick rates.",
                MessageType.Info
            );

            EditorGUILayout.PropertyField(channelConfig, new GUIContent("Channel Config"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawMeleeConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showMeleeConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showMeleeConfig, "MELEE CONFIGURATION");

        if (showMeleeConfig)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox(
                "Melee abilities use animation events for frame-perfect hitbox activation.\n" +
                "Add 'ActivateHitbox' and 'DeactivateHitbox' events to your attack animation at the desired frames.",
                MessageType.Info
            );

            EditorGUILayout.PropertyField(meleeConfig, new GUIContent("Melee Config"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawExplosionConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showExplosionConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showExplosionConfig, "EXPLOSION CONFIGURATION");

        if (showExplosionConfig)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(explosionConfig, new GUIContent("Explosion Config"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }
    private void DrawPassiveConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showPassiveConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showPassiveConfig, "PASSIVE CONFIGURATION");

        if (showPassiveConfig)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(passiveConfig, new GUIContent("Passive Config"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }
    private void DrawSummonConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showSummonConfig = EditorGUILayout.BeginFoldoutHeaderGroup(showSummonConfig, "SUMMON CONFIGURATION");

        if (showSummonConfig)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox(
                "Summon abilities spawn a pet that follows the caster and autonomously attacks nearby enemies.\n" +
                "Configure the pet prefab, combat stats, sub-ability (Melee or Projectile), and lifetime below.",
                MessageType.Info
            );

            EditorGUILayout.PropertyField(summonConfig, new GUIContent("Summon Config"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawCastEffects()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showCastEffects = EditorGUILayout.BeginFoldoutHeaderGroup(showCastEffects, "ABILITY CAST EFFECTS");

        if (showCastEffects)
        {
            EditorGUI.indentLevel++;

            SerializedProperty grantsBuff = castEffects.FindPropertyRelative("grantsBuff");
            EditorGUILayout.PropertyField(grantsBuff);
            if (grantsBuff.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(castEffects.FindPropertyRelative("customBuffScript"));
                EditorGUI.indentLevel--;
            }

            SerializedProperty consumesHealth = castEffects.FindPropertyRelative("consumesHealth");
            EditorGUILayout.PropertyField(consumesHealth);
            if (consumesHealth.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(castEffects.FindPropertyRelative("healthCost"));
                EditorGUI.indentLevel--;
            }

            SerializedProperty appliesSelfDebuff = castEffects.FindPropertyRelative("appliesSelfDebuff");
            EditorGUILayout.PropertyField(appliesSelfDebuff);
            if (appliesSelfDebuff.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(castEffects.FindPropertyRelative("customDebuffScript"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawHitVisuals()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showHitVisuals = EditorGUILayout.BeginFoldoutHeaderGroup(showHitVisuals, "HIT VISUALS");

        if (showHitVisuals)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(hitVisualPrefab, new GUIContent("Hit Visual Prefab", "Spawned at the target position on every hit."));
            EditorGUILayout.PropertyField(hitVisualSound, new GUIContent("Hit Sound", "Played at the target position on every hit."));
            EditorGUILayout.PropertyField(hitFlashColor, new GUIContent("Hit Flash Color", "Flash color applied to the target sprite on hit."));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawEffectToggle(SerializedProperty parent, string toggleName, string configName, string label)
    {
        SerializedProperty toggle = parent.FindPropertyRelative(toggleName);
        EditorGUILayout.PropertyField(toggle, new GUIContent(label));

        if (toggle.boolValue)
        {
            EditorGUI.indentLevel++;
            SerializedProperty config = parent.FindPropertyRelative(configName);
        }
        else
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
