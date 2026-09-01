using UnityEngine;
using UnityEngine.UI;
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image forceFieldImage;
    [SerializeField] private float hideDelay = 3f;
    [SerializeField] private float yOffset = 1.5f;

    [Header("Shared Canvas (optional)")]
    [Tooltip("When set, visibility is controlled by toggling this container instead of enabling/disabling the canvas. " +
             "Assign the health section RectTransform from PlayerWorldCanvas here.")]
    [SerializeField] private GameObject contentContainer;

    [Header("Effect Icons")]    
    private Organism organism;
    private float hideTimer;
    private Camera mainCamera;
    private bool usingSharedCanvas; // true when nested inside a PlayerWorldCanvas
    private PlayerWorldCanvas playerWorldCanvas;
    
    void Awake()
    {
        organism = GetComponentInParent<Organism>();
        mainCamera = Camera.main;

        // Detect whether we are a child of a unified PlayerWorldCanvas
        var sharedCanvas = GetComponentInParent<PlayerWorldCanvas>();
        usingSharedCanvas = (sharedCanvas != null);
        playerWorldCanvas = sharedCanvas;

        // Auto-correct misconfigured contentContainer: if it's assigned to the PlayerWorldCanvas
        // root instead of the HealthSection child, reassign it to prevent disabling the whole canvas.
        if (usingSharedCanvas && contentContainer != null && contentContainer == sharedCanvas.gameObject)
        {
            Debug.LogWarning("[WorldHealthBar] contentContainer was incorrectly assigned to PlayerWorldCanvas root. Auto-correcting to HealthSection.");
            contentContainer = sharedCanvas.HealthSection != null ? sharedCanvas.HealthSection.gameObject : null;
        }

        // Fall back: if no canvas field assigned and we're not in a shared canvas, try to find one
        if (canvas == null && !usingSharedCanvas)
            canvas = GetComponentInParent<Canvas>();
    }

    void OnEnable()
    {
        Organism.OnHealthChanged += HandleHealthChanged;
    }
    
    void OnDisable()
    {
        Organism.OnHealthChanged -= HandleHealthChanged;
    }
    
    void HandleHealthChanged(Organism changedOrganism, float newHealth)
    {
        if (changedOrganism == organism)
        {
            UpdateHealthBar();

            if (newHealth < organism.MaxHealth)
            {
                // Damaged — show bar and start hide countdown
                SetVisible(true);
                hideTimer = hideDelay;
            }
            else if (organism.MaxHealth > 0 && hideTimer <= 0)
            {
                // At full health (including initial network sync) — start the hide countdown.
                // This fires whenever SyncVars reconcile after network spawn, so it is safe
                // regardless of character-stat RPC timing.
                hideTimer = hideDelay;
            }
        }
    }
    
    void Update()
    {
        // // Auto-hide after delay at full health
        // if (organism.CurrentHealth >= organism.MaxHealth && hideTimer > 0)
        // {
        //     hideTimer -= Time.deltaTime;
        //     if (hideTimer <= 0)
        //         SetVisible(false);
        // }
    }
    
    void LateUpdate()
    {
        // Only billboard when NOT inside a PlayerWorldCanvas (which handles it itself)
        if (!usingSharedCanvas && canvas != null && canvas.enabled && mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        }
    }

    /// <summary>
    /// Show or hide the health bar without disabling the shared canvas.
    /// </summary>
    private void SetVisible(bool visible)
    {
        Debug.Log($"[WorldHealthBar] SetVisible({visible}) - contentContainer={(contentContainer != null ? contentContainer.name : "null")}, usingSharedCanvas={usingSharedCanvas} (caller: {new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod()?.Name})");
        
        if (contentContainer != null)
        {
            contentContainer.SetActive(visible);
            playerWorldCanvas?.NotifySectionChanged();
        }
        else if (!usingSharedCanvas && canvas != null)
        {
            canvas.enabled = visible;
        }
        // Shared canvas with no contentContainer assigned: section stays visible;
        // caller should assign the healthSection GO as contentContainer in the prefab.
    }

    void UpdateHealthBar()
    {
        if (fillImage != null)
            fillImage.fillAmount = organism.GetHealthPercentage();
    }

    void UpdateForceField()
    {
        if (forceFieldImage != null)
        {
            if (organism.CurrentForceField > 0)
            {
                forceFieldImage.enabled = true;
                float forceFieldRatio = organism.CurrentForceField / organism.MaxHealth;
                if (forceFieldRatio >= 1f)
                {
                    forceFieldImage.fillAmount = 1f;
                }
                else
                {
                    forceFieldImage.fillAmount = forceFieldRatio;   
                }
            }
            else
            {
                forceFieldImage.enabled = false;
            }
        }
    }
}