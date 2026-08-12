using UnityEngine;
using TMPro;

public class DamageFloaterManager : MonoBehaviour
{
    public static DamageFloaterManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private DamageFloaterConfig config;

    [Header("Prefab")]
    [SerializeField] private GameObject damageFloaterPrefab;

    private Canvas worldCanvas;
    private RectTransform canvasRect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Find or create world canvas
        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null)
        {
            Debug.LogError("DamageFloaterManager needs a Canvas component!");
            return;
        }

        canvasRect = worldCanvas.GetComponent<RectTransform>();

        // Set canvas to World Space for natural world positioning
        if (worldCanvas.renderMode != RenderMode.WorldSpace)
        {
            worldCanvas.renderMode = RenderMode.WorldSpace;

            // Scale down the canvas for proper world space sizing
            // 0.01 scale makes 100 units in canvas = 1 world unit
            worldCanvas.transform.localScale = Vector3.one * 0.01f;

            Debug.Log("DamageFloater canvas set to World Space mode.");
        }

        if (config == null)
        {
            Debug.LogError("DamageFloaterConfig not assigned!");
        }

        Organism.OnEnergySpent += HandleEnergySpent;
    }

    private void OnDestroy()
    {
        Organism.OnEnergySpent -= HandleEnergySpent;
    }

    private void HandleEnergySpent(Organism organism, float amount)
    {
        if (organism == null) return;
        ShowEnergySpent(organism.transform.position, amount, organism.transform);
    }

    /// <summary>
    /// Shows damage floater with directional movement based on attacker position
    /// </summary>
    public void ShowDamage(Vector3 targetPosition, float damage, string damageType = "Physical", bool isCritical = false, Vector3? attackerPosition = null, Transform targetTransform = null)
    {
        if (damageFloaterPrefab == null || config == null)
        {
            Debug.LogWarning("DamageFloater prefab or config not assigned!");
            return;
        }

        if (Camera.main == null)
        {
            Debug.LogError("Main camera not found!");
            return;
        }

        // Always float straight up regardless of attacker position
        Vector2 direction = Vector2.up;

        GameObject floaterObj = Instantiate(damageFloaterPrefab, worldCanvas.transform);

        // Set world position directly (canvas is in world space)
        floaterObj.transform.position = targetPosition;

        DamageFloater floater = floaterObj.GetComponent<DamageFloater>();
        if (floater != null)
        {
            Color color = GetColorForDamageType(damageType);
            if (isCritical) color = config.criticalDamageColor;

            // Apply custom font BEFORE Initialize
            if (config.customFont != null)
            {
                float size = isCritical ? config.fontSize * config.criticalFontSizeMultiplier : config.fontSize;
                floater.SetFont(config.customFont, size);
            }

            floater.Initialize(config, damage, color, direction, isCritical, targetTransform);

            // Apply outline settings
            if (config.enableOutline)
            {
                floater.SetOutline(config.outlineColor, config.outlineThickness);
            }
        }
    }

    public void ShowText(Vector3 worldPosition, string text, Color color, Vector2? direction = null, Transform targetTransform = null)
    {
        if (damageFloaterPrefab == null || config == null) return;

        if (Camera.main == null)
        {
            Debug.LogError("Main camera not found!");
            return;
        }

        // Use world position directly without offset
        Vector3 spawnPosition = worldPosition;

        GameObject floaterObj = Instantiate(damageFloaterPrefab, worldCanvas.transform);
        // Use direct world-position assignment — same as ShowDamage — so it works
        // correctly with a WorldSpace canvas regardless of worldCamera assignment.
        floaterObj.transform.position = worldPosition;

        DamageFloater floater = floaterObj.GetComponent<DamageFloater>();
        if (floater != null)
        {
            Vector2 dir = direction ?? Vector2.up;
            floater.Initialize(config, text, color, dir, false, targetTransform);

            // Apply outline settings
            if (config.enableOutline)
            {
                floater.SetOutline(config.outlineColor, config.outlineThickness);
            }

            // Apply custom font if set
            if (config.customFont != null)
            {
                floater.SetFont(config.customFont, config.fontSize);
            }
        }
    }

    public void ShowHealing(Vector3 worldPosition, float amount, Transform targetTransform = null)
    {
        if (config == null) return;
        ShowText(worldPosition, $"+{Mathf.CeilToInt(amount)}", config.healingColor, null, targetTransform);
    }

    public void ShowEnergySpent(Vector3 worldPosition, float amount, Transform targetTransform = null)
    {
        if (config == null) return;
        ShowText(worldPosition, $"-{Mathf.CeilToInt(amount)}", config.energySpentColor, null, targetTransform);
    }

    private Color GetColorForDamageType(string damageType)
    {
        if (config == null) return Color.white;

        // Check if it's a magical/elemental type
        //switch cases would be cleaner but we want to allow for flexible naming conventions like "FireDamage", "IceBlast", etc.
        switch (damageType)
        {
            case string s when s.Contains("Fire"):
                return config.fireColor;
            case string s when s.Contains("Ice"):
                return config.iceColor;
            case string s when s.Contains("Lightning"):
                return config.lightningColor;
            case string s when s.Contains("Dark"):
                return config.darkColor;
            case string s when s.Contains("Light"):
                return config.lightColor;
            case string s when s.Contains("Nature"):
                return config.natureColor;
            case string s when s.Contains("Burning"):
                return config.burningColor;
            case string s when s.Contains("Poison"):
                return config.poisonColor;
            case string s when s.Contains("Bleeding"):
                return config.bleedingColor;
            default:
                return config.physicalDamageColor;
        }


        return config.physicalDamageColor;
    }
}