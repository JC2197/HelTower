using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages loading screen display between arena transitions.
/// Shows while managers initialize and synchronize.
/// Lives in its own additive scene loaded by UISceneManager at startup;
/// persists via DontDestroyOnLoad across all scene transitions.
/// RPC coverage is handled by ArenaTeleporter (ShowLoadingScreenRpc) and
/// ArenaManager (ArenaTransitionCompleteRpc) — no NetworkBehaviour needed here.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    private const string TransitionLogTag = "[MainMenu->Command]";
    private const string CircleCloseStateName = "CircleClose";
    private const string CircleOpenStateName = "CircleOpen";
    private const string IdleStateName = "LoadscreenIdle";
    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Animator anim;
    [Header("Loading Screen Settings")]
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float minimumDisplayTime = 5f;
    [SerializeField] private float sceneLoadHideFallbackDelay = 2f;
    private static LoadingScreen instance;
    private bool isLoading = false;
    private float loadingStartTime;
    private GameObject cachedHUD; // Store reference to HUD since Find() doesn't work on inactive objects
    private Coroutine sceneLoadFallbackCoroutine;
    private Coroutine fadeOutCoroutine;
    private bool isHideInProgress = false;
    private bool suppressSceneLoadFallback = false;
    private float lastHideRequestTime = -10f;
    private const float HIDE_REQUEST_DEBOUNCE_SECONDS = 0.1f;

    public static LoadingScreen Instance => instance;
    // Typewriter removed; always report complete so old wait loops fall through immediately.
    public static bool IsTypewriterComplete => true;

    public void EnsureVisible()
    {
        Canvas rootCanvas = transform.root.GetComponent<Canvas>();
        if (rootCanvas != null)
            rootCanvas.enabled = true;

        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private void DisableCanvas()
    {
        Canvas rootCanvas = transform.root.GetComponent<Canvas>();
        if (rootCanvas != null)
            rootCanvas.enabled = false;
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;

            // LoadingScreen lives in its own additive scene loaded by UISceneManager
            // at startup, before networking starts. DontDestroyOnLoad keeps it alive
            // across all scene transitions (character selection → CommandScene → GameScene).
            Transform root = transform.root;
            DontDestroyOnLoad(root.gameObject);
            Debug.Log($"[LoadingScreen] Set as singleton Instance with DontDestroyOnLoad (root: {root.name})");

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (anim != null)
            {
                // Keep circle transitions animating even if gameplay temporarily sets timeScale to 0.
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                if (anim.HasState(0, Animator.StringToHash(IdleStateName)))
                {
                    anim.Play(IdleStateName, 0, 0f);
                    anim.Update(0f);
                }
            }

            SceneManager.sceneLoaded += OnSceneLoaded;

            Hide(true); // Start hidden
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    /// <summary>
    /// Show the loading screen.
    /// </summary>
    public void Show(string location, string difficulty = "Standard", string objective = "Defeat all enemies")
    {
        Debug.Log($"[TIMING] [LoadingScreen] Show() called at {Time.realtimeSinceStartup:F3}s with location='{location}', difficulty='{difficulty}', objective='{objective}'");
        // Disable player input and enemy actions
        PlayerController.InputEnabled = false;
        Enemy.ActionsEnabled = false;

        // Cache HUD reference before we potentially hide it (can't Find inactive objects)
        if (cachedHUD == null)
        {
            cachedHUD = GameObject.Find("HUD");
            if (cachedHUD != null)
            {
                Debug.Log($"[LoadingScreen] Cached HUD reference: {cachedHUD.name}");
            }
        }

        isLoading = true;
        loadingStartTime = Time.realtimeSinceStartup;

        EnsureVisible();
        Debug.Log($"[TIMING] [LoadingScreen] Panel activated at {Time.realtimeSinceStartup:F3}s");

        if (anim != null)
        {
            anim.speed = 1f;
            anim.Play(CircleCloseStateName, 0, 0f);
            anim.Update(0f);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f; // Instant visibility
        }

        // Cancel in-flight hide for the new request.
        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }

        isHideInProgress = false;

        if (sceneLoadFallbackCoroutine != null)
        {
            StopCoroutine(sceneLoadFallbackCoroutine);
            sceneLoadFallbackCoroutine = null;
        }
    }

    /// <summary>
    /// Coroutine form used by SceneTransitioner: plays the close animation and waits for it to finish.
    /// </summary>
    public IEnumerator Show()
    {
        suppressSceneLoadFallback = true;
        Show("", "", "");
        yield return PlayAndWaitForState(CircleCloseStateName);
    }

    /// <summary>
    /// Coroutine form used by SceneTransitioner: plays the open animation and waits for it to finish.
    /// </summary>
    public IEnumerator Hide()
    {
        if (!isLoading || isHideInProgress)
        {
            Debug.Log($"[LoadingScreen] Animated hide ignored. isLoading={isLoading}, isHideInProgress={isHideInProgress}");
            yield break;
        }

        isHideInProgress = true;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        if (anim != null)
        {
            anim.speed = 1f;
            anim.Play(CircleOpenStateName, 0, 0f);
            anim.Update(0f);
        }

        isLoading = false;

        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }

        if (sceneLoadFallbackCoroutine != null)
        {
            StopCoroutine(sceneLoadFallbackCoroutine);
            sceneLoadFallbackCoroutine = null;
        }

        yield return PlayAndWaitForState(CircleOpenStateName);

        if (anim != null && anim.HasState(0, Animator.StringToHash(IdleStateName)))
        {
            anim.Play(IdleStateName, 0, 0f);
            anim.Update(0f);
        }

        suppressSceneLoadFallback = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        DisableCanvas();

        if (cachedHUD != null)
        {
            cachedHUD.SetActive(true);
        }

        if (Time.timeScale < 1f)
        {
            Time.timeScale = 1f;
        }

        PlayerController.InputEnabled = true;
        Enemy.ActionsEnabled = true;
    }

    /// <summary>
    /// Waits out the current animator clip length (unscaled) so callers can await the visual transition.
    /// </summary>
    private IEnumerator PlayAndWaitForState(string stateName)
    {
        if (anim == null)
            yield break;

        float waitStartedAt = Time.realtimeSinceStartup;
        Debug.Log($"{TransitionLogTag} LoadingScreen: wait start for '{stateName}' at t={waitStartedAt:F3}s");

        int targetHash = Animator.StringToHash(stateName);
        float enterTimeout = 2f;
        float enterElapsed = 0f;

        while (enterElapsed < enterTimeout)
        {
            AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
            if (!anim.IsInTransition(0) && currentState.shortNameHash == targetHash)
            {
                break;
            }

            enterElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        AnimatorStateInfo enteredState = anim.GetCurrentAnimatorStateInfo(0);
        Debug.Log($"{TransitionLogTag} LoadingScreen: entered state '{stateName}'={enteredState.shortNameHash == targetHash}, length={enteredState.length:F3}, normalizedTime={enteredState.normalizedTime:F3}");

        // First guarantee the visual duration based on the clip length.
        float clipDuration = Mathf.Max(enteredState.length, 0.2f);
        yield return new WaitForSecondsRealtime(clipDuration);

        // Then give Animator a short grace window to report normalized completion.
        float completionGrace = 0.5f;
        float stateElapsed = 0f;
        while (stateElapsed < completionGrace)
        {
            AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
            if (!anim.IsInTransition(0) && currentState.shortNameHash == targetHash && currentState.normalizedTime >= 1f)
            {
                Debug.Log($"{TransitionLogTag} LoadingScreen: wait complete for '{stateName}' at t={Time.realtimeSinceStartup:F3}s (elapsed={(Time.realtimeSinceStartup - waitStartedAt):F3}s)");
                yield break;
            }

            stateElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning($"{TransitionLogTag} LoadingScreen: state '{stateName}' did not report normalized completion after guaranteed clip duration ({clipDuration:F3}s).");
    }

    /// <summary>
    /// Hide the loading screen
    /// </summary>
    public void Hide(bool immediate = false)
    {
        Debug.Log($"[LoadingScreen] Hide() called. isLoading={isLoading}, immediate={immediate}");

        if (!immediate)
        {
            float now = Time.realtimeSinceStartup;
            if (!isLoading)
            {
                Debug.Log("[LoadingScreen] Hide ignored (already not loading).");
                return;
            }

            if (isHideInProgress || now - lastHideRequestTime < HIDE_REQUEST_DEBOUNCE_SECONDS)
            {
                Debug.Log("[LoadingScreen] Hide request ignored (hide already in progress / debounced).");
                return;
            }

            isHideInProgress = true;
            lastHideRequestTime = now;
        }

        if (anim != null)
        {
            anim.speed = 1f;
            anim.Play(CircleOpenStateName, 0, 0f); // Optional: play cutoff animation if assigned
        }

        isLoading = false;

        if (sceneLoadFallbackCoroutine != null)
        {
            StopCoroutine(sceneLoadFallbackCoroutine);
            sceneLoadFallbackCoroutine = null;
        }

        if (immediate)
        {
            if (fadeOutCoroutine != null)
            {
                StopCoroutine(fadeOutCoroutine);
                fadeOutCoroutine = null;
            }

            GameObject hudRoot = GameObject.Find("HUD");
            if (hudRoot != null)
            {
                hudRoot.SetActive(true);
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            DisableCanvas();

            if (anim != null && anim.HasState(0, Animator.StringToHash(IdleStateName)))
            {
                anim.Play(IdleStateName, 0, 0f);
                anim.Update(0f);
            }

            isHideInProgress = false;
        }
        else
        {
            fadeOutCoroutine = StartCoroutine(FadeOutAfterMinimumTime());
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isLoading)
            return;

        if (suppressSceneLoadFallback)
        {
            Debug.Log($"{TransitionLogTag} LoadingScreen: suppressing scene-load fallback for '{scene.name}' while SceneTransitioner owns the transition.");
            return;
        }

        // Fallback: if the normal hide path fails, guarantee hide when entering gameplay/hub scenes.
        if (scene.name == "CommandScene" || scene.name == "GameScene")
        {
            if (sceneLoadFallbackCoroutine != null)
                StopCoroutine(sceneLoadFallbackCoroutine);

            sceneLoadFallbackCoroutine = StartCoroutine(HideAfterSceneLoadFallback(scene.name));
        }
    }

    private IEnumerator HideAfterSceneLoadFallback(string sceneName)
    {
        yield return new WaitForSecondsRealtime(sceneLoadHideFallbackDelay);

        if (!isLoading)
            yield break;

        Debug.LogWarning($"[LoadingScreen] Fallback hide triggered after loading '{sceneName}'.");
        HideLoading();
        sceneLoadFallbackCoroutine = null;
    }

    private IEnumerator FadeOutAfterMinimumTime()
    {
        Debug.Log($"[LoadingScreen] FadeOutAfterMinimumTime() started at {Time.realtimeSinceStartup:F3}s");

        // Ensure minimum display time
        float elapsedTime = Time.realtimeSinceStartup - loadingStartTime;
        float remainingTime = minimumDisplayTime - elapsedTime;

        Debug.Log($"[LoadingScreen] elapsedTime={elapsedTime:F3}s, minimumDisplayTime={minimumDisplayTime}s, remainingTime={remainingTime:F3}s");

        if (remainingTime > 0)
        {
            Debug.Log($"[LoadingScreen] Waiting additional {remainingTime:F3}s for minimum display time");
            yield return new WaitForSecondsRealtime(remainingTime);
        }

        Debug.Log($"[LoadingScreen] Starting fade out at {Time.realtimeSinceStartup:F3}s, canvasGroup null: {canvasGroup == null}");

        // Fade out
        if (canvasGroup != null)
        {
            while (canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha -= Time.unscaledDeltaTime * fadeSpeed;
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        Debug.Log($"[LoadingScreen] Fade complete at {Time.realtimeSinceStartup:F3}s, deactivating panel");

        DisableCanvas();

        // Show player HUD using cached reference
        if (cachedHUD != null)
        {
            cachedHUD.SetActive(true);
        }

        // Ensure timeScale is 1 (in case TraitRollerUI or something else left it at 0)
        if (Time.timeScale < 1f)
        {
            Debug.LogWarning($"[LoadingScreen] Time.timeScale was {Time.timeScale}, resetting to 1");
            Time.timeScale = 1f;
        }

        // Re-enable player input and enemy actions
        PlayerController.InputEnabled = true;
        Enemy.ActionsEnabled = true;

        fadeOutCoroutine = null;
        isHideInProgress = false;

        Debug.Log($"[LoadingScreen] FadeOutAfterMinimumTime() COMPLETE at {Time.realtimeSinceStartup:F3}s - Input re-enabled, timeScale={Time.timeScale}");
    }

    /// <summary>
    /// Static convenience method to show loading with typewriter effect
    /// </summary>
    public static void ShowLoading(string location, string difficulty = "Standard", string objective = "Defeat all enemies")
    {
        if (instance != null)
        {
            instance.StartCoroutine(instance.Show());
        }
    }

    /// <summary>
    /// Static convenience method to hide loading
    /// </summary>
    public static void HideLoading()
    {
        Debug.Log($"[LoadingScreen] HideLoading() called. Instance is null: {instance == null}");
        if (instance != null)
        {
            instance.StartCoroutine(instance.Hide());
        }
        else
        {
            Debug.LogError("[LoadingScreen] HideLoading() called but Instance is NULL!");
        }
    }

    /// <summary>
    /// Ensures a LoadingScreen instance exists. If missing, loads the configured
    /// loading-screen scene additively and waits briefly for initialization.
    /// </summary>
    public static IEnumerator EnsureInstanceReady(float timeoutSeconds = 3f)
    {
        instance = FindAnyExistingInstance();
        if (instance != null)
            yield break;

        //string loadingSceneName = ResolveLoadingSceneName();
        string loadingSceneName = "LoadingScreen";
        Scene loadingScene = SceneManager.GetSceneByName(loadingSceneName);

        if (!loadingScene.isLoaded)
        {
            Debug.LogWarning($"[LoadingScreen] Instance missing. Loading scene '{loadingSceneName}' additively.");
            SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Additive);
        }

        float start = Time.realtimeSinceStartup;
        while (instance == null && (Time.realtimeSinceStartup - start) < timeoutSeconds)
        {
            instance = FindAnyExistingInstance();
            yield return null;
        }

        if (instance == null)
        {
            Debug.LogError($"[LoadingScreen] Failed to create instance within {timeoutSeconds:F1}s.");
        }
    }

    private static LoadingScreen FindAnyExistingInstance()
    {
        if (instance != null)
            return instance;

        LoadingScreen[] allScreens = Resources.FindObjectsOfTypeAll<LoadingScreen>();
        foreach (LoadingScreen screen in allScreens)
        {
            if (screen == null)
                continue;
            if (!screen.gameObject.scene.isLoaded)
                continue;
            return screen;
        }

        return null;
    }

    // private static string ResolveLoadingSceneName()
    // {
    //     SceneConfiguration config = Resources.Load<SceneConfiguration>("SceneConfig");
    //     if (config != null && !string.IsNullOrEmpty(config.loadingScreenSceneName))
    //         return config.loadingScreenSceneName;

    //     return "LoadingScreen";
    // }
}
