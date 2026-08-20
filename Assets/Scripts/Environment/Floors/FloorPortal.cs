using UnityEngine;
using System.Collections;
using FishNet;
using FishNet.Object;
using FishNet.Connection;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>
/// Teleporter that triggers floor transitions when the player interacts with it.
/// Plays an activation animation and hides the player at the correct frame.
// In multiplayer mode, only the server can trigger transitions and they affect all players.
/// SERVER-AUTHORITATIVE: Only server triggers floor loading, all clients show loading screen via RPC.
/// </summary>
[RequireComponent(typeof(Animator))]
public class FloorPortal : Interactable
{
    [Header("Animation Settings")]
    [SerializeField] private string activateAnimationName = "Activate";
    [SerializeField] private string idleAnimationName = "Idle";
    [SerializeField] private int frameRate = 12;
    [SerializeField] private int totalFrames = 16;
    [SerializeField] private int playerHideFrame = 9; // Frame at which player disappears

    [Header("Interaction")]
    [SerializeField] private bool startEnabled = false;
    [Tooltip("If true, teleporter is interactable from the start (for CommandScene). If false, requires floorClearWatcher to enable it.")]

    [Header("floor Transition")]
    [SerializeField] private bool loadRandomfloor = true;
    [SerializeField] private Floor specificfloor; // Optional: load a specific floor instead of random

    [Header("Audio")]
    [SerializeField] private AudioClip activationSound;

    private new Animator animator;
    private bool isActivating = false;
    private FloorManager floorManager;
    // Set by MapDeviceUI when player confirms destination in CommandScene.
    /// <summary>
    /// Queues a destination selected from the map UI.
    /// Consumed on the next CommandScene teleporter activation.
    /// </summary>


    protected override void Awake()
    {
        base.Awake();
        floorManager = FloorManager.Instance;

        // Teleporters should always be controlled by floor clear events
        controlledByFloorClear = true;

        animator = GetComponent<Animator>();

        // Force idle state at start to prevent auto-activation
        if (animator != null)
        {
            animator.Play(idleAnimationName, 0, 0f);
        }

        // Setup audio source if we have a sound
        if (activationSound != null)
        {
            AudioManager.Instance.PlaySpatialSound(activationSound, transform.position, 1f, Random.Range(0.9f, 1.1f));

        }

        // Set interaction state based on startEnabled flag
        SetInteractable(startEnabled);
        SetVisible(startEnabled);

        // Set default interaction message
        if (string.IsNullOrEmpty(interactionMessage))
        {
            interactionMessage = "Teleport to next floor";
        }
    }

    public void Enable()
    {
        SetInteractable(true);
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        gameObject.GetComponent<SpriteRenderer>().enabled = visible;
        gameObject.GetComponent<Collider2D>().enabled = visible;
    }

    #region Interactable Implementation

    public override void OnInteract(GameObject player)
    {
        if (!CanInteract()) return;
        if (!IsServerStarted)
        {
            Debug.Log($"[floorTeleporter] Client interacted, but only server can trigger floor transitions");
            return;
        }

        Debug.Log($"[floorTeleporter] Server: Player interacted with teleporter");

        FloorManager.Instance.TransitionToRandomFloor();
    }

    public override bool CanInteract()
    {
        return base.CanInteract() && !isActivating;
    }

    #endregion


    #region Network RPCs



    /// <summary>
    /// RPC to hide interaction prompt on all clients
    /// </summary>
    [ObserversRpc]
    private void HideInteractionPromptRpc()
    {
        InteractionPromptUI.Hide();
    }

    #endregion
}
