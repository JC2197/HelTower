using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Full-screen Trait Roller canvas shown on level-up.
/// Freezes the game via Time.timeScale = 0, starts a real-time countdown timer,
/// and displays 3 trait options (Left / Middle / Right).
/// Uses Physics2D raycasting (GraphicRaycaster) so clicks still register while paused.
/// The player selects one before the timer expires; if no selection is made,
/// a random trait is chosen automatically.
///
/// Prefab structure (user creates this):
/// - TraitRollerCanvas (Canvas, Screen Space – Overlay, covers entire screen)
///     Must have a GraphicRaycaster component for click detection while paused.
///   - TraitRollerPanel (root panel — this component)
///     - TraitRollerTimer (TMP_Text — countdown display)
///     - LeftTraitOption  (TraitOptionUI)
///     - MiddleTraitOption (TraitOptionUI)
///     - RightTraitOption  (TraitOptionUI)
///
/// Attach this to the TraitRollerPanel. Assign references in inspector.
/// traitOptions order: [0] = Left, [1] = Middle, [2] = Right
/// </summary>
public class TraitRollerUI : MonoBehaviour
{
    [Header("Trait Option Slots (Left / Middle / Right)")]
    [Tooltip("Assign 3 TraitOptionUI: [0]=Left, [1]=Middle, [2]=Right")]
    [SerializeField] private TraitOptionUI[] traitOptions = new TraitOptionUI[3];
    
    [Header("Timer")]
    [Tooltip("Text displaying the countdown timer")]
    [SerializeField] private TMP_Text traitRollerTimer;
    
    [Tooltip("Seconds the player has to pick a trait before auto-selection")]
    [SerializeField] private float selectionDuration = 10f;
    
    [Tooltip("Grace period in seconds before selection is enabled (prevents accidental clicks)")]
    [SerializeField] private float gracePeriod = 2f;
    
    [Header("Panel")]
    [Tooltip("The root panel that gets shown/hidden (this GameObject or a child)")]
    [SerializeField] private GameObject rollerPanel;

    [Header("Appearance")]
    [Tooltip("Seconds to wait after the level-up event before showing the trait rollers")]
    [SerializeField] private float traitRollerDelay = 1f;

    [Tooltip("Duration of the fade-in animation when the trait roller panel appears")]
    [SerializeField] private float fadeInDuration = 0.1f;
    
    private List<TraitData> currentRolledTraits;
    private bool selectionMade = false;
    private bool selectionEnabled = false;
    private bool isActive = false;
    private CanvasGroup rollerPanelCanvasGroup;
    private Coroutine _delayedShowCoroutine;
    private Coroutine _restoreInputCoroutine;
    private bool _previousInputEnabled;
    private bool _hasCapturedInputState;

    /// <summary>
    /// True while a trait-roller session is open (traits have been rolled and the player
    /// has not yet made a selection). Used by LevelUpSequencer to know when it is safe
    /// to fire the next queued reward round.
    /// </summary>
    public static bool IsSessionActive { get; private set; }

    private float timerRemaining;
    private float graceRemaining;
    private float previousTimeScale;
    
    private void OnEnable()
    {
        TraitRoller.OnTraitsRolled += HandleTraitsRolled;
    }
    
    private void OnDisable()
    {
        TraitRoller.OnTraitsRolled -= HandleTraitsRolled;
        if (_delayedShowCoroutine != null)
        {
            StopCoroutine(_delayedShowCoroutine);
            _delayedShowCoroutine = null;
        }
        if (_restoreInputCoroutine != null)
        {
            StopCoroutine(_restoreInputCoroutine);
            _restoreInputCoroutine = null;
        }
        _hasCapturedInputState = false;
        PlayerController.InputEnabled = true;
        // Ensure the static flag is cleared if this component is destroyed mid-session
        // so LevelUpSequencer isn't left waiting forever.
        IsSessionActive = false;
    }

    private void CaptureInputStateIfNeededAndDisable()
    {
        // Cascaded rolls can happen while input is already disabled by this UI.
        // Preserve the original pre-roll state until the full roll chain finishes.
        if (!_hasCapturedInputState)
        {
            _previousInputEnabled = PlayerController.InputEnabled;
            _hasCapturedInputState = true;
        }

        PlayerController.InputEnabled = false;
    }
    
    private void Awake()
    {
        // Hide panel on start
        if (rollerPanel != null)
        {
            rollerPanel.SetActive(false);
            rollerPanelCanvasGroup = rollerPanel.GetComponent<CanvasGroup>();
            if (rollerPanelCanvasGroup == null)
                rollerPanelCanvasGroup = rollerPanel.AddComponent<CanvasGroup>();
        }
    }
    
    /// <summary>
    /// Update runs even when Time.timeScale = 0 because we use Time.unscaledDeltaTime.
    /// Handles the countdown timer and raycast-based click detection.
    /// </summary>
    private void Update()
    {
        if (!isActive) return;
        
        // Count down using unscaled time (not affected by timeScale = 0)
        timerRemaining -= Time.unscaledDeltaTime;
        
        // Grace period countdown
        if (!selectionEnabled)
        {
            graceRemaining -= Time.unscaledDeltaTime;
            if (graceRemaining <= 0f)
                selectionEnabled = true;
        }
        
        if (traitRollerTimer != null)
        {
            int displaySeconds = Mathf.CeilToInt(Mathf.Max(0f, timerRemaining));
            traitRollerTimer.text = displaySeconds.ToString();
        }
        
        // Check for click via EventSystem raycasting (only after grace period)
        if (selectionEnabled && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleClick();
        }
        
        // Timer expired — auto-select
        if (timerRemaining <= 0f && !selectionMade)
        {
            Debug.Log("[TraitRollerUI] Timer expired — auto-selecting random trait");
            int randomIndex = Random.Range(0, currentRolledTraits != null ? currentRolledTraits.Count : 0);
            OnTraitSelected(randomIndex);
        }
    }
    
    /// <summary>
    /// Raycast through the EventSystem to find which TraitOptionUI was clicked.
    /// Works while Time.timeScale = 0 because UI raycasting is unscaled.
    /// </summary>
    private void HandleClick()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero
        };
        
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        
        foreach (RaycastResult result in results)
        {
            TraitOptionUI option = result.gameObject.GetComponentInParent<TraitOptionUI>();
            if (option != null)
            {
                // Find which index this option is
                for (int i = 0; i < traitOptions.Length; i++)
                {
                    if (traitOptions[i] == option)
                    {
                        OnTraitSelected(i);
                        return;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Called when TraitRoller fires OnTraitsRolled with the rolled trait list.
    /// For normal level-up rolls: waits traitRollerDelay seconds, then fades in the panel and pauses the game.
    /// For cascaded rolls (triggered immediately after another pick): shows instantly.
    /// </summary>
    private void HandleTraitsRolled(List<TraitData> rolledTraits)
    {
        // Detect if this is a cascaded roll (triggered during trait application while UI was already active)
        bool isCascadedRoll = isActive || selectionMade;
        
        currentRolledTraits = rolledTraits;
        selectionMade = false;
        
        if (rolledTraits == null || rolledTraits.Count == 0)
        {
            Debug.LogWarning("[TraitRollerUI] No traits to display");
            return;
        }

        // Mark the session as active immediately so LevelUpSequencer.WaitUntil does not
        // resolve during the ShowRollerAfterDelay coroutine and prematurely dequeue the
        // next level-up before this roll's UI has opened.
        IsSessionActive = true;
        
        // Populate each option slot (Left / Middle / Right)
        for (int i = 0; i < traitOptions.Length; i++)
        {
            if (traitOptions[i] == null) continue;
            
            if (i < rolledTraits.Count)
            {
                traitOptions[i].Populate(rolledTraits[i]);
                traitOptions[i].gameObject.SetActive(true);
            }
            else
            {
                traitOptions[i].gameObject.SetActive(false);
            }
        }
        
        if (isCascadedRoll)
        {
            // Cascaded roll: show panel immediately without delay or fade
            if (rollerPanelCanvasGroup != null) rollerPanelCanvasGroup.alpha = 1f;
            if (rollerPanel != null) rollerPanel.SetActive(true);

            CaptureInputStateIfNeededAndDisable();

            timerRemaining = selectionDuration;
            graceRemaining = 0f;
            selectionEnabled = true;
            isActive = true;
            IsSessionActive = true;
            Debug.Log($"[TraitRollerUI] Cascaded roll! Displaying {rolledTraits.Count} ability upgrade options — no grace period");
        }
        else
        {
            // Normal level-up roll: delay then fade in
            if (_delayedShowCoroutine != null)
                StopCoroutine(_delayedShowCoroutine);
            _delayedShowCoroutine = StartCoroutine(ShowRollerAfterDelay(rolledTraits.Count));
        }
    }

    private IEnumerator ShowRollerAfterDelay(int traitCount)
    {
        // Wait for level-up message to display, using real time (independent of timeScale)
        yield return new WaitForSecondsRealtime(traitRollerDelay);

        // Freeze the game
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        CaptureInputStateIfNeededAndDisable();

        // Show panel with alpha at 0
        if (rollerPanel != null)
        {
            if (rollerPanelCanvasGroup != null) rollerPanelCanvasGroup.alpha = 0f;
            rollerPanel.SetActive(true);
        }

        // Fade in using unscaled time (game is now paused)
        if (rollerPanelCanvasGroup != null && fadeInDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                rollerPanelCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            rollerPanelCanvasGroup.alpha = 1f;
        }
        else if (rollerPanelCanvasGroup != null)
        {
            rollerPanelCanvasGroup.alpha = 1f;
        }

        // Start countdown
        timerRemaining = selectionDuration;
        graceRemaining = gracePeriod;
        selectionEnabled = false;
        isActive = true;
        IsSessionActive = true;
        _delayedShowCoroutine = null;

        Debug.Log($"[TraitRollerUI] Level-up! Game paused. Displaying {traitCount} trait options — {selectionDuration}s to choose");
    }
    
    /// <summary>
    /// Called when the player clicks a trait option or when the timer expires.
    /// Unpauses the game and hides the panel.
    /// </summary>
    public void OnTraitSelected(int index)
    {
        if (selectionMade) return;
        if (currentRolledTraits == null || index < 0 || index >= currentRolledTraits.Count)
            return;
        
        selectionMade = true;
        isActive = false;
        IsSessionActive = false;
        
        TraitData selectedTrait = currentRolledTraits[index];
        Debug.Log($"[TraitRollerUI] Player selected trait: {selectedTrait.displayName}");
        
        // Apply the selected trait to the player
        // NOTE: This may trigger a new roll (e.g., ability upgrade roll when ability reaches level 5)
        ApplySelectedTrait(selectedTrait);
        
        // Check if a new roll came in during ApplySelectedTrait (e.g., ability upgrade roll)
        // If so, don't hide the panel or unpause — let the new roll take over
        if (isActive)
        {
            Debug.Log($"[TraitRollerUI] New roll detected during trait application — keeping UI active");
            return;
        }
        
        // Hide the panel
        if (rollerPanel != null)
            rollerPanel.SetActive(false);
        
        // Unpause the game
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        BeginRestoreInputAfterSelection();
        
        currentRolledTraits = null;
        selectionMade = false; // Clear so next fresh level-up roll isn't mistaken for a cascaded roll
    }

    private void BeginRestoreInputAfterSelection()
    {
        if (_restoreInputCoroutine != null)
            StopCoroutine(_restoreInputCoroutine);
        _restoreInputCoroutine = StartCoroutine(RestoreInputAfterSelectionClick());
    }

    private IEnumerator RestoreInputAfterSelectionClick()
    {
        // Prevent the same click that selected a trait from also triggering a weapon attack.
        while (Mouse.current != null && Mouse.current.leftButton.isPressed)
            yield return null;

        // Wait one extra frame so input callbacks for this click are fully drained.
        yield return null;
        PlayerController.InputEnabled = _previousInputEnabled;
        _hasCapturedInputState = false;
        _restoreInputCoroutine = null;
    }
    
    /// <summary>
    /// Find the node ID for this TraitData in the character's trait tree,
    /// then unlock it via CharacterTraitManager, which also triggers stat recalculation.
    /// </summary>
    private void ApplySelectedTrait(TraitData selectedTrait)
    {
        PlayerController player = PlayerController.GetLocalPlayer();
        if (player == null)
        {
            Debug.LogError("[TraitRollerUI] Cannot apply trait — no local player found");
            return;
        }
        
        CharacterData characterData = player.GetCurrentCharacterData();
        if (characterData == null)
        {
            Debug.LogError("[TraitRollerUI] Cannot apply trait — no character data");
            return;
        }
        string nodeID = null;       
        // If the trait isn't in the tree (rolled from global TraitDataList), use its traitID
        if (string.IsNullOrEmpty(nodeID))
        {
            nodeID = selectedTrait.traitID;
        }
        
        if (string.IsNullOrEmpty(nodeID))
        {
            Debug.LogError($"[TraitRollerUI] Could not determine nodeID for trait '{selectedTrait.displayName}'");
            return;
        }
        
        // Unlock trait via CharacterTraitManager (handles stat recalculation internally)
        CharacterTraitManager traitManager = player.GetComponent<CharacterTraitManager>();
        if (traitManager != null)
        {
            // Generate a unique node ID so the same trait can be stacked multiple times.
            // e.g. first Thorns uses "Thorns", second uses "Thorns_2", third uses "Thorns_3".
            string finalNodeID = nodeID;
            int stackIndex = 1;
            while (traitManager.IsNodeUnlocked(finalNodeID))
            {
                finalNodeID = $"{nodeID}_{++stackIndex}";
            }

            bool success = traitManager.UnlockTrait(finalNodeID, selectedTrait);
            if (success)
            {
                Debug.Log($"[TraitRollerUI] Successfully unlocked trait '{selectedTrait.displayName}' (node: {finalNodeID})");
                // Stats are recalculated automatically by UnlockTrait → RequestStatsRecalculation
            }
            else
            {
                Debug.LogWarning($"[TraitRollerUI] Failed to unlock trait '{selectedTrait.displayName}'");
            }
        }
        else
        {
            Debug.LogError("[TraitRollerUI] No CharacterTraitManager found on player");
        }
    }
}
