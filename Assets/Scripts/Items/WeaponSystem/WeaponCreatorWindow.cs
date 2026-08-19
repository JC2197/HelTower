using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using FishNet.Object;
using FishNet.Component.Transforming;
using FishNet.Component.Animating;

/// <summary>
/// Editor tool that creates a WeaponConfig ScriptableObject and a properly structured
/// weapon prefab with all required networking components, child hierarchy, materials,
/// and visual defaults copied from the weapon-type template (e.g. MakeshiftRifle for Rifles).
///
/// Created prefab structure (Rifle):
///   [WeaponName] (root)
///     ├─ NetworkObject
///     ├─ NetworkTransform
///     ├─ WeaponSprite (child)
///     │   ├─ SpriteRenderer  (weapon material)
///     │   ├─ Animator
///     │   ├─ NetworkTransform
///     │   ├─ NetworkAnimator
///     │   ├─ HandHolder  (grandchild) — SpriteRenderer (hand material)
///     │   └─ HandHolder2 (grandchild) — SpriteRenderer (hand material)
///     └─ LaunchZone (child) — LaunchZone component
/// </summary>
public class WeaponCreatorWindow : EditorWindow
{
    // === Weapon type dropdown (dynamic from WeaponTypeList) ===
    private int selectedWeaponTypeIndex = 0;
    private string[] weaponTypeOptions;

    // === Custom fields ===
    private string weaponName = "";
    private string outputFolder = "Assets/Gameplay/Characters";
    private Sprite weaponSprite;
    private Sprite treeSprite;
    private int damageMin = 3;
    private int damageMax = 4;
    private string selectedDamageType = "Piercing";
    private GameObject projectilePrefabOverride;
    private bool usesAmmo = true;
    private bool ammoDependsOnAmmo = true;
    private int magazineSize = 1;
    private float reloadTime = 2.5f;
    private bool includeAnimatorOverride = false;
    private Texture2D animatorOverrideSprite;
    private AbilityConfig grantedPrimaryAbility;
    private AbilityConfig grantedSecondaryAbility;
    private RuntimeAnimatorController animatorController;
    private int handHolderCount = 2;
    private bool launchZoneVertical = false;
    private Vector2 launchZoneOffset = new Vector2(0.0f, 0.0f);
    private bool addSortingGroup = false;

    // === Internal state ===
    private Vector2 scrollPosition;
    private string statusMessage = "";
    private MessageType statusType = MessageType.None;
    private string[] damageTypeNames;

    // --- Shared material path ---
    private const string GLOW_ZERO_MAT_PATH = "Assets/Resources/Materials/GlowZero.mat";
    private const string ANIMATOR_CONTROLLER_GUID = "887c9925feebd034baf9e6b5993dc0ec";

    [MenuItem("Tools/Weapon Creator")]
    public static void ShowWindow()
    {
        var window = GetWindow<WeaponCreatorWindow>("Weapon Creator");
        window.minSize = new Vector2(420, 520);
        window.Show();
    }

    private void OnEnable()
    {
        LoadDamageTypes();
        LoadWeaponTypes();
    }

    private void LoadWeaponTypes()
    {
        WeaponTypeList list = WeaponTypeList.GetInstance();
        if (list != null && list.weaponTypes.Count > 0)
        {
            weaponTypeOptions = list.weaponTypes.ToArray();
        }
        else
        {
            weaponTypeOptions = new string[] { "Configure weapon types in WeaponTypeList (Resources/WeaponTypeList.asset)" };
        }
        if (selectedWeaponTypeIndex >= weaponTypeOptions.Length)
            selectedWeaponTypeIndex = 0;
    }

    private void LoadDamageTypes()
    {
        DamageTypeDatabase database = DamageTypeDatabase.Instance;
        if (database != null)
        {
            damageTypeNames = database.GetDamageTypeNames();
        }
        else
        {
            damageTypeNames = new string[]
            {
                "Configure damage types in DamageTypeDatabase (Resources/DamageTypeDatabase.asset)",
            };
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Weapon Creator", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "Creates a new weapon with:\n" +
            "• WeaponConfig ScriptableObject (.asset)\n" +
            "• Weapon Prefab with networking components (.prefab)\n" +
            "Visual settings (offsets, flipping, aiming, sorting) are copied from the weapon-type template.",
            MessageType.Info);

        EditorGUILayout.Space(10);

        // --- Weapon Type ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Weapon Type", EditorStyles.boldLabel);
        selectedWeaponTypeIndex = EditorGUILayout.Popup("Type", selectedWeaponTypeIndex, weaponTypeOptions);

        // Show whether a WeaponTypeConfig exists for the selected type
        string selectedType = weaponTypeOptions[selectedWeaponTypeIndex];
        var typeConfig = WeaponTypeConfig.EditorGetConfigForType(selectedType);
        if (typeConfig != null)
        {
            EditorGUILayout.HelpBox($"Will inherit positioning from WeaponTypeConfig: \"{selectedType}\"", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox($"No WeaponTypeConfig found for \"{selectedType}\". Create one in Resources/Weapons/ to set default positioning.", MessageType.Warning);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // --- Custom Fields ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Weapon Details", EditorStyles.boldLabel);

        weaponName = EditorGUILayout.TextField("Weapon Name", weaponName);
        outputFolder = EditorGUILayout.TextField(new GUIContent("Output Folder",
            "Project-relative folder under Assets. A folder named after the weapon will be created here."), outputFolder);
        weaponSprite = (Sprite)EditorGUILayout.ObjectField("Weapon Sprite", weaponSprite, typeof(Sprite), false);
        treeSprite = (Sprite)EditorGUILayout.ObjectField("Crafting Tree Icon", treeSprite, typeof(Sprite), false);

        EditorGUILayout.Space(5);
        damageMin = EditorGUILayout.IntField("Damage Min (Tier I)", damageMin);
        damageMax = EditorGUILayout.IntField("Damage Max (Tier I)", damageMax);

        // Damage type dropdown
        if (damageTypeNames != null && damageTypeNames.Length > 0)
        {
            int currentIdx = System.Array.IndexOf(damageTypeNames, selectedDamageType);
            if (currentIdx < 0) currentIdx = 0;
            int newIdx = EditorGUILayout.Popup("Damage Type", currentIdx, damageTypeNames);
            selectedDamageType = damageTypeNames[newIdx];
        }
        else
        {
            selectedDamageType = EditorGUILayout.TextField("Damage Type", selectedDamageType);
        }

        EditorGUILayout.Space(5);
        projectilePrefabOverride = (GameObject)EditorGUILayout.ObjectField(
            "Projectile Override", projectilePrefabOverride, typeof(GameObject), false);

        EditorGUILayout.Space(5);
        animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
            "Animation Base", animatorController, typeof(RuntimeAnimatorController), false);
        handHolderCount = EditorGUILayout.IntPopup("Hand Holders", handHolderCount,
            new string[] { "1 (rear only)", "2 (rear + front)" }, new int[] { 1, 2 });
        launchZoneVertical = EditorGUILayout.Toggle("LaunchZone Vertical", launchZoneVertical);
        launchZoneOffset = EditorGUILayout.Vector2Field("LaunchZone Offset", launchZoneOffset);
        addSortingGroup = EditorGUILayout.Toggle(new GUIContent("Add Sorting Group",
            "Adds a SortingGroup component to the WeaponSprite child. Required for weapons " +
            "that have multiple sibling renderers (e.g. bows with an Arrow child) so the " +
            "whole group sorts as one unit against the character."), addSortingGroup);
        EditorGUILayout.Space(5);
        grantedPrimaryAbility = (AbilityConfig)EditorGUILayout.ObjectField(
            "Primary Ability", grantedPrimaryAbility, typeof(AbilityConfig), false);
        EditorGUILayout.Space(5);
        grantedSecondaryAbility = (AbilityConfig)EditorGUILayout.ObjectField(
            "Secondary Ability", grantedSecondaryAbility, typeof(AbilityConfig), false);
        EditorGUILayout.Space(5);
        usesAmmo = EditorGUILayout.Toggle("Uses Ammo", usesAmmo);
        if (usesAmmo)
        {
            EditorGUI.indentLevel++;
            ammoDependsOnAmmo = EditorGUILayout.Toggle("Depends On Ammo", ammoDependsOnAmmo);
            magazineSize = EditorGUILayout.IntField("Magazine Size", magazineSize);
            reloadTime = EditorGUILayout.FloatField("Reload Time", reloadTime);
            EditorGUI.indentLevel--;
        }

        includeAnimatorOverride = EditorGUILayout.Toggle("Include Animator Override", includeAnimatorOverride);
        if (includeAnimatorOverride)
        {
            EditorGUI.indentLevel++;
            animatorOverrideSprite = (Texture2D)EditorGUILayout.ObjectField("Animator Override Sprite", animatorOverrideSprite, typeof(Texture2D), false);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- Output preview ---
        if (!string.IsNullOrEmpty(weaponName))
        {
            string safeName = weaponName.Trim();
            string sanitized = SanitizeName(safeName);
            string basePath = GetOutputFolder(sanitized);

            EditorGUILayout.LabelField("Files to be created:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Config: {basePath}/{safeName}.asset");
            EditorGUILayout.LabelField($"Prefab: {basePath}/{sanitized}.prefab");
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);

        // --- Create button ---
        EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(weaponName));
        var prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);

        if (GUILayout.Button("Create Weapon", GUILayout.Height(40)))
        {
            CreateWeapon();
        }

        GUI.backgroundColor = prevColor;
        EditorGUI.EndDisabledGroup();

        // --- Status ---
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        EditorGUILayout.EndScrollView();
    }

    // ================================================================
    //  Helpers
    // ================================================================

    private string GetOutputFolder(string sanitizedName)
    {
        return $"{outputFolder.Trim().TrimEnd('/', '\\').Replace('\\', '/')}/{sanitizedName}";
    }

    private bool TryGetOutputFolder(string sanitizedName, out string folderPath)
    {
        folderPath = GetOutputFolder(sanitizedName);
        if (folderPath == $"/{sanitizedName}" ||
            (!folderPath.StartsWith("Assets/", System.StringComparison.Ordinal) && folderPath != $"Assets/{sanitizedName}"))
        {
            statusMessage = "Output Folder must be Assets or a folder under Assets.";
            statusType = MessageType.Error;
            return false;
        }

        return true;
    }

    private string SanitizeName(string name)
    {
        var sb = new System.Text.StringBuilder();
        bool capitalizeNext = true;
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(capitalizeNext ? char.ToUpper(c) : c);
                capitalizeNext = false;
            }
            else if (c == ' ')
            {
                capitalizeNext = true;
            }
        }
        return sb.ToString();
    }

    // ================================================================
    //  Main creation flow
    // ================================================================

    private void CreateWeapon()
    {
        string safeName = weaponName.Trim();
        string sanitized = SanitizeName(safeName);


        if (string.IsNullOrEmpty(sanitized))
        {
            statusMessage = "Please enter a valid weapon name.";
            statusType = MessageType.Error;
            return;
        }

        if (!TryResolveBaseController(out RuntimeAnimatorController baseController, out string controllerSource))
        {
            statusMessage = "No base animator controller found. Set Animation Base or add a type-matching base controller.";
            statusType = MessageType.Error;
            return;
        }
        if (includeAnimatorOverride && animatorOverrideSprite == null)
        {
            statusMessage = "Animator Override is enabled but no override sprite/sheet is assigned.";
            statusType = MessageType.Error;
            return;
        }

        if (!TryGetOutputFolder(sanitized, out string folderPath))
        {
            return;
        }

        string prefabPath = $"{folderPath}/{sanitized}.prefab";
        string configPath = $"{folderPath}/{safeName}.asset";

        EnsureFolderExists(folderPath);

        // Check for existing assets
        if (AssetDatabase.LoadAssetAtPath<Object>(prefabPath) != null ||
            AssetDatabase.LoadAssetAtPath<Object>(configPath) != null)
        {
            if (!EditorUtility.DisplayDialog("Overwrite?",
                $"Assets already exist at:\n{prefabPath}\n{configPath}\n\nOverwrite?",
                "Overwrite", "Cancel"))
            {
                return;
            }
        }

        try
        {
            // 1. Copy sprite into the weapon folder
            Sprite localSprite = CopySpriteToFolder(weaponSprite, folderPath);

            // 2. Create prefab
            GameObject prefab = CreateWeaponPrefab(sanitized, prefabPath, localSprite, baseController);
            if (prefab == null) return;

            // 3. Create config
            WeaponConfig config = CreateWeaponConfig(safeName, configPath, prefab, localSprite);

            // 4. Register in database
            WeaponConfigDatabase database = Resources.Load<WeaponConfigDatabase>("WeaponConfigDatabase");
            if (database != null)
            {
                database.AddConfig(config);
            }
            else
            {
                Debug.LogWarning("[WeaponCreator] WeaponConfigDatabase not found in Resources. " +
                                 "Add the new config manually or run 'Find All WeaponConfigs' on the database.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            statusMessage = $"Successfully created weapon '{safeName}'!\n" +
                           $"Config: {configPath}\n" +
                           $"Prefab: {prefabPath}";
            statusType = MessageType.Info;

            Debug.Log($"[WeaponCreator] Created weapon '{safeName}' — Config: {configPath}, Prefab: {prefabPath}");
        }
        catch (System.Exception e)
        {
            statusMessage = $"Error creating weapon: {e.Message}";
            statusType = MessageType.Error;
            Debug.LogError($"[WeaponCreator] Error: {e}");
        }
    }

    // ================================================================
    //  Prefab creation — mirrors MakeshiftRifle hierarchy
    //
    //  Root (NetworkObject, NetworkTransform)
    //    WeaponSprite (SpriteRenderer, Animator, NetworkTransform, NetworkAnimator)
    //      HandHolder  (SpriteRenderer)
    //      HandHolder2 (SpriteRenderer)
    //    LaunchZone    (LaunchZone)
    // ================================================================

    private GameObject CreateWeaponPrefab(string sanitizedName, string prefabPath, Sprite localSprite, RuntimeAnimatorController baseController)
    {
        // Load shared assets
        Material glowZeroMat = AssetDatabase.LoadAssetAtPath<Material>(GLOW_ZERO_MAT_PATH);
        RuntimeAnimatorController animCtrl = baseController;

        // --- Root ---
        GameObject root = new GameObject(sanitizedName);
        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkTransform>();

        // --- WeaponSprite ---
        GameObject weaponSpriteObj = new GameObject("WeaponSprite");
        weaponSpriteObj.transform.SetParent(root.transform, false);
        weaponSpriteObj.transform.localPosition = Vector3.zero;

        SpriteRenderer weaponSR = weaponSpriteObj.AddComponent<SpriteRenderer>();
        if (glowZeroMat != null) weaponSR.material = glowZeroMat;
        if (localSprite != null) weaponSR.sprite = localSprite;

        Animator animator = weaponSpriteObj.AddComponent<Animator>();
        if (animCtrl != null && !includeAnimatorOverride)
        {
            animator.runtimeAnimatorController = animCtrl;
        }
        else
        {
            if (animCtrl != null && includeAnimatorOverride && animatorOverrideSprite != null)
            {
                AnimatorOverrideController overrideController = new AnimatorOverrideController();
                //add asset to folder so it can be saved and used in the prefab
                AssetDatabase.CreateAsset(overrideController, $"{prefabPath.Replace(".prefab", "")}_AnimatorOverride.controller");
                overrideController.runtimeAnimatorController = animCtrl;
                foreach (AnimationClip clip in animCtrl.animationClips)
                {
                    AnimationClip overrideClip = new AnimationClip();
                    overrideClip.name = sanitizedName + "_" + clip.name;
                    AssetDatabase.CreateAsset(overrideClip, $"{prefabPath.Replace(".prefab", "")}_{clip.name}.anim");
                    overrideController[clip.name] = overrideClip;
                }
                animator.runtimeAnimatorController = overrideController;
            }
            else
            {
                Debug.LogWarning("[WeaponCreator] Animator controller not assigned. The weapon will have no animations.");
            }
        }

        weaponSpriteObj.AddComponent<NetworkTransform>();
        weaponSpriteObj.AddComponent<NetworkAnimator>();

        if (addSortingGroup)
            weaponSpriteObj.AddComponent<SortingGroup>();

        // --- HandHolder (rear hand) ---
        GameObject handHolder = new GameObject("HandHolder");
        handHolder.transform.SetParent(weaponSpriteObj.transform, false);
        handHolder.transform.localPosition = new Vector3(-0.2590565f, -0.0107498765f, 0f);
        SpriteRenderer handSR = handHolder.AddComponent<SpriteRenderer>();
        if (glowZeroMat != null) handSR.material = glowZeroMat;

        // --- HandHolder2 (front hand) ---
        if (handHolderCount >= 2)
        {
            GameObject handHolder2 = new GameObject("HandHolder2");
            handHolder2.transform.SetParent(weaponSpriteObj.transform, false);
            handHolder2.transform.localPosition = new Vector3(0.3190685f, 0.0048751235f, 0f);
            SpriteRenderer hand2SR = handHolder2.AddComponent<SpriteRenderer>();
            if (glowZeroMat != null) hand2SR.material = glowZeroMat;
        }

        // --- LaunchZone (child of WeaponSprite) ---
        GameObject launchZone = new GameObject("LaunchZone");
        launchZone.transform.SetParent(weaponSpriteObj.transform, false);
        launchZone.transform.localPosition = new Vector3(launchZoneOffset.x, launchZoneOffset.y, 0f);
        launchZone.transform.localRotation = Quaternion.Euler(0f, 0f, launchZoneVertical ? 90f : 0f);
        launchZone.AddComponent<LaunchZone>();

        // Save prefab
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);

        if (prefabAsset == null)
        {
            Debug.LogError($"[WeaponCreator] Failed to save prefab at {prefabPath}");
        }

        return prefabAsset;
    }

    private RuntimeAnimatorController ResolveWeaponTypeAnimatorController(string weaponType)
    {
        if (!string.IsNullOrWhiteSpace(weaponType))
        {
            string compactType = weaponType.Replace(" ", string.Empty);
            string[] candidateNames = new string[]
            {
                $"{weaponType}Base",
                $"{compactType}Base"
            };

            foreach (string candidate in candidateNames)
            {
                string[] guids = AssetDatabase.FindAssets($"{candidate} t:RuntimeAnimatorController");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                    if (controller != null && controller.name == candidate)
                    {
                        return controller;
                    }
                }
            }
        }

        // Fallback to the historical default controller used by WeaponCreator.
        return LoadAssetByGuid<RuntimeAnimatorController>(ANIMATOR_CONTROLLER_GUID);
    }

    private bool TryResolveBaseController(out RuntimeAnimatorController controller, out string source)
    {
        controller = null;
        source = "None";

        // 1) Explicit field in the window
        if (animatorController != null)
        {
            controller = animatorController;
            source = "Animation Base field";
            return true;
        }

        // 2) Weapon-type match
        string weaponType = weaponTypeOptions[selectedWeaponTypeIndex];
        controller = ResolveWeaponTypeAnimatorController(weaponType);
        if (controller != null)
        {
            source = $"Weapon type '{weaponType}'";
            return true;
        }

        // 3) Historical fallback GUID
        controller = LoadAssetByGuid<RuntimeAnimatorController>(ANIMATOR_CONTROLLER_GUID);
        if (controller != null)
        {
            source = "Fallback GUID";
            return true;
        }

        return false;
    }

    // ================================================================
    //  Config creation — applies visual defaults per weapon type
    // ================================================================

    private WeaponConfig CreateWeaponConfig(string displayName, string configPath, GameObject prefab, Sprite localSprite)
    {
        WeaponConfig config = ScriptableObject.CreateInstance<WeaponConfig>();

        // --- Custom per-weapon fields ---
        config.weaponName = displayName;
        config.inventorySprite = localSprite;
        config.worldSprite = localSprite;
        config.weaponDamageMin = damageMin;
        config.weaponDamageMax = damageMax;
        config.weaponDamageType = selectedDamageType;
        config.weaponPrefab = prefab;

        // Ability
        config.grantedPrimaryAbility = grantedPrimaryAbility;
        config.grantedSecondaryAbility = grantedSecondaryAbility;
        // Projectile override
        if (projectilePrefabOverride != null)
        {
            config.projectilePrefabOverride = projectilePrefabOverride;
        }

        // --- Weapon type string (matches WeaponTypeList entries) ---
        config.weaponType = weaponTypeOptions[selectedWeaponTypeIndex];

        // Positioning: inherit from WeaponTypeConfig (no override needed)
        // If a WeaponTypeConfig exists, WeaponConfig.Positioning will resolve to it automatically.
        config.overridePositioning = false;

        // Ammo
        config.usesAmmo = usesAmmo;
        config.ammoConfig = new AmmoConfig
        {
            dependsOnAmmo = ammoDependsOnAmmo,
            magazineSize = magazineSize,
            reloadTime = reloadTime
        };

        AssetDatabase.CreateAsset(config, configPath);
        return config;
    }

    // ================================================================
    //  Utility
    // ================================================================

    private T LoadAssetByGuid<T>(string guid) where T : Object
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    /// <summary>
    /// Copies the sprite's source texture into the weapon folder and returns the
    /// corresponding Sprite from the copy. If the sprite is already in the target
    /// folder, returns it as-is.
    /// </summary>
    private static Sprite CopySpriteToFolder(Sprite sprite, string destFolder)
    {
        if (sprite == null) return null;

        string srcPath = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(srcPath)) return null;

        string fileName = System.IO.Path.GetFileName(srcPath);
        string destPath = $"{destFolder}/{fileName}";

        // Already in the right place
        if (srcPath == destPath) return sprite;

        if (!AssetDatabase.CopyAsset(srcPath, destPath))
        {
            Debug.LogWarning($"[WeaponCreator] Failed to copy sprite to {destPath}, using original.");
            return sprite;
        }

        AssetDatabase.Refresh();

        // If the source texture is a multi-sprite sheet, find the matching sub-sprite by name
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(destPath);
        foreach (Object asset in assets)
        {
            if (asset is Sprite s && s.name == sprite.name)
                return s;
        }

        // Fallback: single-sprite texture
        Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
        return single != null ? single : sprite;
    }

    private static void EnsureFolderExists(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(path);

        EnsureFolderExists(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
