using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component for displaying charge progress above the player.
/// Activates during ability charging and fills from left to right.
/// Works in two modes:
///   • Standalone — owns its own WorldSpace Canvas (legacy setup).
///   • Shared     — lives inside a PlayerWorldCanvas; no own canvas required.
/// </summary>
public class ChargeBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private GameObject barContainer;

    [Header("Visual Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, 0);

    [Header("Billboard Settings")]
    [SerializeField] private bool alwaysFaceCamera = true;

    [Header("Gradient Settings")]
    [Tooltip("Color gradient traversed from bar 1 (green) to final bar (red). Evaluated 0→1 across all bars.")]
    [SerializeField] private Gradient barGradient;

    [Header("Completion Settings")]
    [SerializeField] private float fadeOutDuration = 0.3f;

    private Transform playerTransform;
    private Canvas canvas;
    private Camera mainCamera;
    private bool isActive = false;
    private bool isCompleting = false;
    private float chargeStartTime;
    private float chargeDuration;
    private Coroutine fadeCoroutine;
    private bool usingSharedCanvas; // true when nested inside a PlayerWorldCanvas
    private PlayerWorldCanvas playerWorldCanvas;

    // Multi-bar state
    private int maxBars = 1;
    private bool inHoldPhase = false; // true after precast completes, during button-hold

    private void Awake()
    {
        // Detect shared-canvas mode
        playerWorldCanvas = GetComponentInParent<PlayerWorldCanvas>();
        usingSharedCanvas = playerWorldCanvas != null;

        canvas = GetComponent<Canvas>();

        InitGradient();

        if (fillImage != null)
        {
            fillImage.color = GetGradientColor(0f);
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

        UpdateFillAmount();
    }

    /// <summary>
    /// Start displaying the charge bar with specified duration.
    /// </summary>
    public void StartCharge(float duration, Transform player, int totalBars = 1)
    {
        if (duration <= 0f)
        {
            Debug.LogWarning("[ChargeBar] Invalid charge duration");
            return;
        }

        playerTransform  = player;
        chargeDuration   = duration;
        chargeStartTime  = Time.time;
        isActive         = true;
        isCompleting     = false;
        inHoldPhase      = false;
        maxBars          = Mathf.Max(1, totalBars);

        // Stop any ongoing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (!usingSharedCanvas)
        {
            transform.SetParent(playerTransform, false);
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.color = GetGradientColor(0f);
        }

        if (barContainer != null)
        {
            barContainer.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        playerWorldCanvas?.NotifySectionChanged();
    }

    /// <summary>
    /// Stop displaying the charge bar (call when charge fires or is cancelled).
    /// </summary>
    public void StopCharge()
    {
        isActive = false;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (barContainer != null)
        {
            barContainer.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        playerWorldCanvas?.NotifySectionChanged();
    }

    /// <summary>
    /// Called when the charge completes and the ability fires.
    /// Shows a brief flash then fades out.
    /// </summary>
    public void CompleteCharge()
    {
        if (!isActive) return;

        isCompleting = true;

        if (fillImage != null)
        {
            fillImage.fillAmount = 1f;
            fillImage.color = GetGradientColor(1f); // End of gradient (red)
        }

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(CompleteAndFadeOut());
    }

    private void UpdateFillAmount()
    {
        if (fillImage == null || isCompleting || inHoldPhase) return;

        float elapsed  = Time.time - chargeStartTime;
        float progress = Mathf.Clamp01(elapsed / chargeDuration);
        // Fill bar 1 from 0→1 (resets per bar)
        fillImage.fillAmount = progress;
        // Color: bar 1 lives at overall t=0→1/maxBars in the gradient
        fillImage.color = GetGradientColor(progress / maxBars);
    }

    /// <summary>
    /// Called every frame during the hold phase by WaitForChargeRelease.
    /// chargeLevel is 1.0 (bar 1 done) to maxTotalBars (all bars full).
    /// </summary>
    public void UpdateHoldPhase(float chargeLevel, int maxTotalBars)
    {
        if (fillImage == null || isCompleting) return;

        inHoldPhase = true;
        maxBars = Mathf.Max(1, maxTotalBars);

        // Fractional part within the current bar — resets to 0 at each new bar
        float barFraction = chargeLevel >= maxBars ? 1f : Mathf.Repeat(chargeLevel, 1f);
        fillImage.fillAmount = barFraction;

        // Smooth gradient traversal based on overall progress across all bars
        float overallT = Mathf.Clamp01(chargeLevel / maxBars);
        fillImage.color = GetGradientColor(overallT);
    }

    /// <summary>
    /// Returns the gradient color at t (0=first bar start, 1=last bar end).
    /// Falls back to green→yellow→red if gradient is unset or default.
    /// </summary>
    private Color GetGradientColor(float t)
    {
        if (barGradient != null && barGradient.colorKeys.Length >= 2)
            return barGradient.Evaluate(t);
        // Fallback: green → yellow → red
        return t < 0.5f
            ? Color.Lerp(Color.green, Color.yellow, t * 2f)
            : Color.Lerp(Color.yellow, Color.red, (t - 0.5f) * 2f);
    }

    private void InitGradient()
    {
        if (barGradient != null && barGradient.colorKeys.Length >= 2) return;
        barGradient = new Gradient();
        barGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.green,                    0.0f),
                new GradientColorKey(new Color(1f, 0.65f, 0f, 1f),  0.5f), // orange
                new GradientColorKey(Color.red,                      1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
    }

    private System.Collections.IEnumerator CompleteAndFadeOut()
    {
        if (fillImage == null) yield break;

        // fillImage.color already set to gradient end by CompleteCharge
        Color startColor = GetGradientColor(1f);

        yield return new WaitForSeconds(0.1f);

        // Fade out
        float fadeTimer = 0f;

        while (fadeTimer < fadeOutDuration)
        {
            fadeTimer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, fadeTimer / fadeOutDuration);
            fillImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        isActive = false;
        StopCharge();
    }
}
