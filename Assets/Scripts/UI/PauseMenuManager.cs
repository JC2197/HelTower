// using UnityEngine;
// using UnityEngine.SceneManagement;
// using UnityEngine.InputSystem;
// using UnityEngine.EventSystems;
// using UnityEngine.UI;
// using TMPro;
// using FishNet.Managing;

// public class PauseMenuManager : MonoBehaviour
// {
//     [Header("Pause Menu")]
//     [SerializeField] private GameObject pauseMenuPanel;
//     [Header("Multiplayer Buttons")]
//     [SerializeField] private Button hostButton;
//     [SerializeField] private Button joinButton;
//     [SerializeField] private TextMeshProUGUI lobbyStatusText;
//     [SerializeField] private GameObject joinInput;
//     [Header("Menu Buttons")]
//     [SerializeField] private Button resumeButton;
//     [SerializeField] private Button returnToCharacterSelectionButton;
//     [SerializeField] private Button quitToMainMenuButton;
//     [SerializeField] private Button quitToCommandButton;
//     [SerializeField] private Button quitGameButton;
//     [Header("Scene Names")]
//     [SerializeField] private string characterSelectionSceneName = "CharacterSelection";

//     private PlayerInput playerInput;
//     private InputAction pauseAction;
//     private bool isPaused = false;
//     private bool _pausedTimeScale = false;

//     private void Awake()
//     {
        
//         // Try to find existing player
//         FindPlayerInput();
        
//         // Wire up ALL buttons in code so they work even if Inspector isn't configured
//         if (hostButton != null)
//             hostButton.onClick.AddListener(OnHostButtonClicked);
//         if (joinButton != null)
//             joinButton.onClick.AddListener(OnJoinButtonClicked);
//         if (resumeButton != null)
//             resumeButton.onClick.AddListener(OnResumeButtonClicked);
//         if (returnToCharacterSelectionButton != null)
//             returnToCharacterSelectionButton.onClick.AddListener(OnReturnToCharacterSelectionClicked);
//         if (quitToMainMenuButton != null)
//             quitToMainMenuButton.onClick.AddListener(OnQuitToMainMenuClicked);
//         if (quitToCommandButton != null)
//             quitToCommandButton.onClick.AddListener(OnQuitToCommandClicked);
//         if (quitGameButton != null)
//             quitGameButton.onClick.AddListener(OnQuitGameClicked);
//     }
    
//     private void OnEnable()
//     {
//         PlayerController.OnPlayerSpawned += HandlePlayerSpawned;
//     }
    
//     private void OnDisable()
//     {
//         PlayerController.OnPlayerSpawned -= HandlePlayerSpawned;
//     }
    
//     private void HandlePlayerSpawned(PlayerController newPlayer)
//     {
//         FindPlayerInput();
//         Debug.Log("PauseMenuManager: Player spawned, finding PlayerInput");
//     }
    
//     private void FindPlayerInput()
//     {
//         // Find the PlayerInput component in the scene (it's on the Player GameObject)
//         playerInput = FindFirstObjectByType<PlayerInput>();
        
//         if (playerInput == null)
//         {
//             Debug.LogWarning("PauseMenuManager: Could not find PlayerInput component yet");
//             return;
//         }
        
//         // Unsubscribe from old pause action if it exists
//         if (pauseAction != null)
//         {
//             pauseAction.performed -= OnPausePerformed;
//         }
        
//         if (playerInput.actions != null)
//         {
//             pauseAction = playerInput.actions.FindAction("Pause");
//             if (pauseAction != null)
//             {
//                 pauseAction.performed += OnPausePerformed;
//                 pauseAction.Enable();
//                 Debug.Log("PauseMenuManager: Pause action successfully bound to ESC");
//             }
//             else
//             {
//                 Debug.LogWarning("PauseMenuManager: Pause action not found in PlayerInput actions.");
//             }
//         }
//     }
    
//     private void OnDestroy()
//     {
//         if (pauseAction != null)
//         {
//             pauseAction.performed -= OnPausePerformed;
//         }
//     }
    
//     private void OnPausePerformed(InputAction.CallbackContext context)
//     {
//         Debug.Log("ESC key pressed!");

//         // If any UI panel is open, close the topmost one instead of toggling pause
//         if (CursorManager.Instance == null)
//         {
//             Debug.LogWarning("[PauseMenuManager] CursorManager.Instance is null — cannot check panel stack. Falling through to pause toggle.");
//         }
//         else
//         {
//             int stackCount = CursorManager.Instance.PanelStackCount;
//             Debug.Log($"[PauseMenuManager] CursorManager panel stack count: {stackCount}");
//             if (CursorManager.Instance.TryCloseTopPanel())
//             {
//                 Debug.Log("[PauseMenuManager] ESC consumed by panel stack — pause menu suppressed.");
//                 return;
//             }
//             Debug.Log("[PauseMenuManager] Panel stack empty — proceeding to toggle pause.");
//         }

//         if (isPaused)
//             ResumeGame();
//         else
//             PauseGame();
//     }
    
//     private void PauseGame()
//     {
//         // Only disable player input - game continues, enemies still act
//         PlayerController.InputEnabled = false;
        
//         if (pauseMenuPanel != null)
//         {
//             pauseMenuPanel.SetActive(true);
//         }

//         // In single-player (offline) mode, also freeze time
//         if (BootstrapManager.IsOffline)
//         {
//             Time.timeScale = 0f;
//             _pausedTimeScale = true;
//         }
        
//         isPaused = true;

//         Debug.Log("Pause menu opened - Player input disabled, enemies still active");
//     }
    
//     private void ResumeGame()
//     {
//         // Re-enable player input
//         PlayerController.InputEnabled = true;

//         if (pauseMenuPanel != null)
//         {
//             pauseMenuPanel.SetActive(false);
//         }

//         // Restore time scale if we froze it
//         if (_pausedTimeScale)
//         {
//             Time.timeScale = 1f;
//             _pausedTimeScale = false;
//         }

//         isPaused = false;

//         Debug.Log("Pause menu closed - Player input enabled");
//     }

//     private void ClosePauseMenuForTransition()
//     {
//         if (pauseMenuPanel != null)
//             pauseMenuPanel.SetActive(false);

//         Time.timeScale = 1f;
//         _pausedTimeScale = false;
//         isPaused = false;
//         PlayerController.InputEnabled = false;
//         Enemy.ActionsEnabled = false;

//         if (LoadingScreen.Instance != null)
//             LoadingScreen.Instance.EnsureVisible();
//     }
    
//     public void OnResumeButtonClicked()
//     {
//         ResumeGame();
//     }

//     public void OnHostButtonClicked()
//     {
//         if (BootstrapManager.Instance == null)
//         {
//             Debug.LogError("[PauseMenuManager] BootstrapManager not found!");
//             return;
//         }
        
//         if (BootstrapManager.Instance.IsHosting)
//         {
//             Debug.LogWarning("[PauseMenuManager] Already hosting!");
//             UpdateLobbyStatus("Already hosting!");
//             return;
//         }
        
//         Debug.Log("[PauseMenuManager] Host button clicked - starting hosting via BootstrapManager");
//         BootstrapManager.Instance.StartHosting();
//         UpdateLobbyStatus("Creating lobby...");
//     }

//     public void OnJoinButtonClicked()
//     {
//         joinInput.SetActive(true);
//         // if (BootstrapManager.Instance == null)
//         // {
//         //     Debug.LogError("[PauseMenuManager] BootstrapManager not found!");
//         //     return;
//         // }
        
//         // if (BootstrapManager.Instance.IsHosting || BootstrapManager.Instance.IsClient)
//         // {
//         //     Debug.LogWarning("[PauseMenuManager] Already connected!");
//         //     UpdateLobbyStatus("Already connected!");
//         //     return;
//         // }
        
//         // Debug.Log("[PauseMenuManager] Join button clicked - searching for lobby via BootstrapManager");
//         // BootstrapManager.Instance.StartClient();
//         // UpdateLobbyStatus("Searching for lobby...");
//     }

//     private void UpdateLobbyStatus(string message)
//     {
//         if (lobbyStatusText != null)
//             lobbyStatusText.text = message;
//     }

//     public void OnReturnToCharacterSelectionClicked()
//     {
//         // Show and unlock the cursor for UI navigation
//         Cursor.visible = true;
//         Cursor.lockState = CursorLockMode.None;
        
//         Debug.Log("Returning to character selection...");
//         SceneManager.LoadScene(characterSelectionSceneName);
//     }

//     public void OnQuitToMainMenuClicked()
//     {
//         StartCoroutine(QuitToMainMenuRoutine());
//     }

//     private System.Collections.IEnumerator QuitToMainMenuRoutine()
//     {
//         Debug.Log("[PauseMenuManager] ========== Quitting to Main Menu - Full Cleanup ==========");
        
//         // Re-enable player input before leaving scene
//         PlayerController.InputEnabled = true;
        
//         // Show and unlock the cursor for UI navigation
//         Cursor.visible = true;
//         Cursor.lockState = CursorLockMode.None;
        
//         // CRITICAL: Full network and player cleanup before returning to menu
//         CleanupNetworkAndPlayers();

//         NetworkManager networkManager = BootstrapManager.Instance != null
//             ? BootstrapManager.Instance.NetworkManager
//             : FindFirstObjectByType<NetworkManager>();

//         float timeout = 5f;
//         float elapsed = 0f;
//         while (networkManager != null && (networkManager.IsServerStarted || networkManager.IsClientStarted) && elapsed < timeout)
//         {
//             elapsed += Time.unscaledDeltaTime;
//             yield return null;
//         }

//         if (networkManager != null && (networkManager.IsServerStarted || networkManager.IsClientStarted))
//         {
//             Debug.LogError($"[PauseMenuManager] Network did not stop within {timeout:F1}s. Server={networkManager.IsServerStarted}, Client={networkManager.IsClientStarted}");
//             yield break;
//         }

//         SceneTransitioner.Instance?.CancelTransition();
        
//         // Load Main Menu
//         Debug.Log("[PauseMenuManager] Loading MainMenu scene...");
//         SceneManager.LoadScene("MainMenu");
//     }
    
//     /// <summary>
//     /// Comprehensive cleanup of network state and player objects
//     /// Ensures a fresh start when returning to Main Menu
//     /// </summary>
//     private void CleanupNetworkAndPlayers()
//     {
//         NetworkManager networkManager = BootstrapManager.Instance != null 
//             ? BootstrapManager.Instance.NetworkManager 
//             : FindFirstObjectByType<NetworkManager>();
        
//         // Step 1: Despawn and destroy all player objects (must happen while server is still running)
//         Debug.Log("[PauseMenuManager] Step 1: Destroying all player objects...");
//         PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
//         foreach (PlayerController player in allPlayers)
//         {
//             if (player != null)
//             {
//                 Debug.Log($"[PauseMenuManager] Destroying player: {player.gameObject.name}");
                
//                 // Try to despawn if networked
//                 if (networkManager != null && networkManager.IsServerStarted)
//                 {
//                     var networkObject = player.GetComponent<FishNet.Object.NetworkObject>();
//                     if (networkObject != null && networkObject.IsSpawned)
//                     {
//                         try
//                         {
//                             networkManager.ServerManager.Despawn(player.gameObject);
//                         }
//                         catch (System.Exception e)
//                         {
//                             Debug.LogWarning($"[PauseMenuManager] Failed to despawn player: {e.Message}");
//                         }
//                     }
//                 }
                
//                 Destroy(player.gameObject);
//             }
//         }
        
//         // Step 2: Stop network + leave Steam lobby via BootstrapManager
//         Debug.Log("[PauseMenuManager] Step 2: Stopping network via BootstrapManager...");
//         if (BootstrapManager.Instance != null)
//         {
//             BootstrapManager.Instance.StopHosting();
//         }
//         else if (networkManager != null)
//         {
//             if (networkManager.IsServerStarted)
//                 networkManager.ServerManager.StopConnection(true);
//             if (networkManager.IsClientStarted)
//                 networkManager.ClientManager.StopConnection();
//         }
        
        
//         // Destroy ArenaManager if it exists (should start fresh)
//         ArenaManager arenaManager = FindFirstObjectByType<ArenaManager>();
//         if (arenaManager != null)
//         {
//             Debug.Log("[PauseMenuManager] Destroying ArenaManager");
//             Destroy(arenaManager.gameObject);
//         }
        
//         // Step 3: Clear stale static state so a new session can start clean
//         Debug.Log("[PauseMenuManager] Step 3: Cleaning up static state...");
//         CharacterSelectionManager.CleanupRuntimeCharacters(); // clears SelectedCharacter + runtime instances
//         NetworkSpawner.ClearCharacterRegistry();
        
//         Debug.Log("[PauseMenuManager] ========== Cleanup Complete ==========");
//     }

//     public void OnQuitToCommandClicked()
//     {
//         ClosePauseMenuForTransition();

//         PlayerController player = PlayerController.GetLocalPlayer();
//         if (player != null)
//         {
//             CharacterTraitManager traitManager = player.GetComponent<CharacterTraitManager>();
//             if (traitManager != null)
//             {
//                 traitManager.ResetAllTraits();
//                 Debug.Log("[PauseMenuManager] Cleared all run traits before returning to CommandScene");
//             }

//             // Quitting a round should NOT keep acquired run gear/inventory.
//             player.ExecuteReturnToCommandScene(keepAcquiredInventoryAndGear: false);
//             return;
//         }

//         // Stop network before loading CommandScene so the new player can initialize properly
//         // NOTE: Network is intentionally NOT stopped here — the player persists via
//         // PlayerManager (DontDestroyOnLoad) and returns to CommandScene alive.
//         // Network is only stopped when quitting to Main Menu.
        
//         // Re-enable player input before leaving scene
//         PlayerController.InputEnabled = true;
        
//         // Show and unlock the cursor for UI navigation
//         Cursor.visible = true;
//         Cursor.lockState = CursorLockMode.None;
        
//         Debug.Log("[PauseMenuManager] No local player found, loading CommandScene directly...");
//         SceneManager.LoadScene("CommandScene");
//     }
//     public void OnQuitGameClicked()
//     {
//         Debug.Log("Quitting game...");
//         Application.Quit();

//     #if UNITY_EDITOR
//         UnityEditor.EditorApplication.isPlaying = false;
//     #endif
//     }

//     /// <summary>
//     /// Stop FishNet networking before transitioning scenes.
//     /// Without this, the new PlayerController.Awake sees IsServerStarted=true
//     /// and defers character loading to OnStartNetwork, which never fires for a
//     /// locally-instantiated (non-network-spawned) player.
//     /// </summary>
//     private void StopNetworkForSceneTransition()
//     {
//         if (BootstrapManager.Instance != null)
//         {
//             BootstrapManager.Instance.StopHosting();
//             Debug.Log("[PauseMenuManager] Network stopped via BootstrapManager");
//         }
//         else
//         {
//             FishNet.Managing.NetworkManager networkManager = FindFirstObjectByType<FishNet.Managing.NetworkManager>();
//             if (networkManager != null)
//             {
//                 if (networkManager.IsServerStarted)
//                     networkManager.ServerManager.StopConnection(true);
//                 if (networkManager.IsClientStarted)
//                     networkManager.ClientManager.StopConnection();
//                 Debug.Log("[PauseMenuManager] Network stopped directly");
//             }
//         }
//     }

//     /// <summary>
//     /// Reset statContainer back to baseStatContainer values and re-apply conversions.
//     /// This undoes all level-up bonuses so the character starts clean.
//     /// </summary>
//     private void ResetStatsToBase(CharacterData characterData)
//     {
//         if (characterData.baseStatContainer == null)
//         {
//             Debug.LogWarning("[PauseMenuManager] No baseStatContainer — cannot reset stats");
//             return;
//         }

//         // Copy base stats back to statContainer
//         var baseStats = characterData.baseStatContainer.GetAllStats();
//         foreach (var stat in baseStats)
//         {
//             characterData.statContainer.SetStat(stat.statID, stat.currentValue);
//         }

//         // Re-apply stat conversions (Vigor→MaxHealth, etc.) on the now-clean base
//         CharacterStatConverter.ApplyConversions(characterData);

//         Debug.Log("[PauseMenuManager] statContainer reset to base values with conversions");
//     }


// }