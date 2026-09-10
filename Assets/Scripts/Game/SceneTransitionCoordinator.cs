using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Owns the loading-screen handshake for asynchronous scene transitions.
/// The transition caller starts the load only after fade-in completes; the destination
/// calls MarkReady when its required gameplay systems are initialized.
/// </summary>
public sealed class SceneTransitionCoordinator : MonoBehaviour
{
    public static SceneTransitionCoordinator Instance { get; private set; }

    private bool transitionActive;
    private bool destinationReady;
    private bool fadeInComplete;
    private Coroutine completionCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public IEnumerator Begin(Action loadAction)
    {
        if (transitionActive)
            yield break;

        transitionActive = true;
        destinationReady = false;
        fadeInComplete = false;
        Debug.Log("[SceneTransitionCoordinator] Transition started.");

        yield return LoadingScreen.EnsureInstanceReady();
        if (LoadingScreen.Instance == null)
        {
            Debug.LogError("[SceneTransitionCoordinator] LoadingScreen could not be created.");
            transitionActive = false;
            yield break;
        }

        yield return LoadingScreen.Instance.ShowAndWait();
        fadeInComplete = true;
        Debug.Log("[SceneTransitionCoordinator] Fade-in complete; invoking load action.");
        loadAction?.Invoke();
    }

    public void ShowForRemoteTransition()
    {
        if (transitionActive)
            return;

        transitionActive = true;
        destinationReady = false;
        fadeInComplete = false;
        Debug.Log("[SceneTransitionCoordinator] Remote transition received.");
        StartCoroutine(ShowRemoteRoutine());
    }

    private IEnumerator ShowRemoteRoutine()
    {
        yield return LoadingScreen.EnsureInstanceReady();
        if (LoadingScreen.Instance != null)
            yield return LoadingScreen.Instance.ShowAndWait();

        fadeInComplete = true;
        Debug.Log("[SceneTransitionCoordinator] Remote fade-in complete.");
        if (destinationReady && completionCoroutine == null)
            completionCoroutine = StartCoroutine(CompleteWhenReady());
    }

    public void MarkReady()
    {
        if (!transitionActive)
        {
            Debug.Log("[SceneTransitionCoordinator] MarkReady ignored; no transition is active.");
            return;
        }

        destinationReady = true;
        Debug.Log($"[SceneTransitionCoordinator] Destination ready; fadeInComplete={fadeInComplete}.");
        if (completionCoroutine == null)
            completionCoroutine = StartCoroutine(CompleteWhenReady());
    }

    private IEnumerator CompleteWhenReady()
    {
        float waitStartedAt = Time.realtimeSinceStartup;
        Debug.Log($"[SceneTransitionCoordinator] Waiting for readiness: fadeInComplete={fadeInComplete}, destinationReady={destinationReady}.");
        while (!fadeInComplete || !destinationReady)
            yield return null;

        Debug.Log($"[SceneTransitionCoordinator] Readiness gates passed after {Time.realtimeSinceStartup - waitStartedAt:F3}s; beginning fade-out.");
        if (destinationReady && LoadingScreen.Instance != null)
            yield return LoadingScreen.Instance.HideAndWait();

        EndScreenUI.Instance?.HideEndScreen();

        Debug.Log("[SceneTransitionCoordinator] Transition complete.");
        destinationReady = false;
        transitionActive = false;
        completionCoroutine = null;
    }
}
