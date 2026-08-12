using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component for displaying reload progress above the player.
/// Activates during reload and fills from left to right.
/// Works in two modes:
///   • Standalone — owns its own WorldSpace Canvas (legacy setup).
///   • Shared     — lives inside a PlayerWorldCanvas; no own canvas required.
/// </summary>
public class ReloadBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private GameObject barContainer;

    [Header("Visual Settings")]
    [SerializeField] private Color fillColor = new Color(1f, 0.8f, 0f, 1f); // Yellow/gold
    [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, 0);

    [Header("Billboard Settings")]
    [SerializeField] private bool alwaysFaceCamera = true;

    [Header("Completion Settings")]
    [SerializeField] private Color completeColor = new Color(0f, 1f, 0f, 1f); // Green
    [SerializeField] private float fadeOutDuration = 0.3f;

    private Transform playerTransform;
    private Canvas canvas;
    private Camera mainCamera;
    private bool isActive = false;
    private bool isCompleting = false;
    private float reloadStartTime;
    private float reloadDuration;
    private Coroutine fadeCoroutine;
    private bool usingSharedCanvas; // true when nested inside a PlayerWorldCanvas
    private PlayerWorldCanvas playerWorldCanvas;

    private void Awake()
    {
        // Detect shared-canvas mode
        playerWorldCanvas = GetComponentInParent<PlayerWorldCanvas>();
        usingSharedCanvas = playerWorldCanvas != null;

        canvas = GetComponent<Canvas>();
        // Do NOT override renderMode, sortingOrder, or localScale here.
        // Configure the Canvas component and RectTransform directly on the prefab.

        if (fillImage != null)
        {
            fillImage.color = fillColor;
            fillImage.fillAmount = 0f;
        }

        // Start disabled
        if (barContainer != null)
        {
            barContainer.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (!isActive) return;

        if (!usingSharedCanvas)
        {
            // Standalone: update position to follow player with offset
            if (playerTransform != null)
                transform.position = playerTransform.position + offset;

            // Billboard effect
            if (alwaysFaceCamera && mainCamera != null)
                transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        }
        // Shared canvas mode: the PlayerWorldCanvas handles billboard; position is fixed in layout.

        UpdateFillAmount();
    }

    /// <summary>
    /// Start displaying the reload bar with specified duration
    /// </summary>
    public void StartReload(float duration, Transform player)
    {
        if (duration <= 0f)
        {
            Debug.LogWarning("[ReloadBar] Invalid reload duration");
            return;
        }

        playerTransform = player;
        reloadDuration  = duration;
        reloadStartTime = Time.time;
        isActive        = true;
        isCompleting    = false;

        // Stop any ongoing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (!usingSharedCanvas)
        {
            // Standalone: parent to player so it moves with them
            transform.SetParent(playerTransform, false);
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.color = fillColor; // Reset to original color
        }

        if (barContainer != null)
        {
            barContainer.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }
        Debug.Log($"[ReloadBar] StartReload - barContainer={(barContainer != null ? barContainer.name : "null")}, usingSharedCanvas={usingSharedCanvas}");
        playerWorldCanvas?.NotifySectionChanged();
    }

    /// <summary>
    /// Stop displaying the reload bar
    /// </summary>
    public void StopReload()
    {
        isActive = false;

        if (barContainer != null)
        {
            barContainer.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
        Debug.Log($"[ReloadBar] StopReload - barContainer={(barContainer != null ? barContainer.name : "null")}, usingSharedCanvas={usingSharedCanvas}");
        playerWorldCanvas?.NotifySectionChanged();
    }

    private void UpdateFillAmount()
    {
        if (fillImage == null || isCompleting) return;

        float elapsed = Time.time - reloadStartTime;
        float progress = Mathf.Clamp01(elapsed / reloadDuration);
        fillImage.fillAmount = progress;
        // Trigger complete effect when done
        if (progress >= 1f && !isCompleting)
        {
            fadeCoroutine = StartCoroutine(CompleteAndFadeOut());
            isCompleting = true;
            
        }
    }

    private System.Collections.IEnumerator CompleteAndFadeOut()
    {
        if (fillImage == null) yield break;

        // Flash green to indicate completion
        fillImage.color = completeColor;

        // Wait a brief moment at full green
        yield return new WaitForSeconds(0.1f);

        // Fade out
        float fadeTimer = 0f;
        Color startColor = completeColor;

        while (fadeTimer < fadeOutDuration)
        {
            fadeTimer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, fadeTimer / fadeOutDuration);
            fillImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        // Fully hide
        StopReload();
    }

    /// <summary>
    /// Set the fill color of the reload bar
    /// </summary>
    public void SetFillColor(Color color)
    {
        fillColor = color;
        if (fillImage != null)
        {
            fillImage.color = color;
        }
    }

    /// <summary>
    /// Set the position offset from player
    /// </summary>
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }

    /// <summary>
    /// Set the height offset above the player (legacy method)
    /// </summary>
    public void SetHeightOffset(float height)
    {
        offset = new Vector3(offset.x, height, offset.z);
    }

    /// <summary>
    /// Enable/disable billboard effect
    /// </summary>
    public void SetBillboardEnabled(bool enabled)
    {
        alwaysFaceCamera = enabled;
    }
}
