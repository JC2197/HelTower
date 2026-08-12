using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using FishNet.Managing;

public class PauseMenuManager : MonoBehaviour
{
    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenuPanel;

    [SerializeField] private GameObject firstPanel;
    [Header("Menu Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button changeCharacterButton;
    [SerializeField] private Button quitToMainMenuButton;
    [SerializeField] private Button quitGameButton;
    [Header("Character Switching")]
    [SerializeField] private CharacterSelectionConfig characterSelectionConfig;
    [Tooltip("Container that holds the generated class buttons (toggled by 'Change Character').")]
    [SerializeField] private GameObject classListPanel;
    [Tooltip("Button template instantiated once per available class.")]
    [SerializeField] private Button classButtonPrefab;
    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Input")]
    [Tooltip("Input actions asset containing the menu-toggle action. Defaults to HeltowerInputs.")]
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Name of the action (bound to ESC) that opens/closes the pause menu.")]
    [SerializeField] private string menuActionName = "Menu";

    private InputAction menuAction;
    private bool isPaused = false;
    private bool _pausedTimeScale = false;
    private bool _classButtonsBuilt = false;
    private bool _registeredWithCursorManager = false;
    private bool _pauseToggleArmed = true;

    private void Awake()
    {
        BindMenuAction();

        // Wire up ALL buttons in code so they work even if Inspector isn't configured
        // if (hostButton != null)
        //     hostButton.onClick.AddListener(OnHostButtonClicked);
        // if (joinButton != null)
        //     joinButton.onClick.AddListener(OnJoinButtonClicked);
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeButtonClicked);
        if (changeCharacterButton != null)
            changeCharacterButton.onClick.AddListener(OnChangeCharacterClicked);
        if (quitToMainMenuButton != null)
            quitToMainMenuButton.onClick.AddListener(OnQuitToMainMenuClicked);
        if (quitGameButton != null)
            quitGameButton.onClick.AddListener(OnQuitGameClicked);

        ResetMenuView();

        if (pauseMenuPanel != null && pauseMenuPanel != gameObject)
            pauseMenuPanel.SetActive(false);
    }

    private void OnEnable()
    {
        PlayerController.OnPlayerSpawned += HandlePlayerSpawned;
    }

    private void OnDisable()
    {
        PlayerController.OnPlayerSpawned -= HandlePlayerSpawned;
    }

    private void HandlePlayerSpawned(PlayerController newPlayer)
    {
        BindMenuAction();
    }

    private void BindMenuAction()
    {
        if (inputActions == null)
        {
#if UNITY_EDITOR
            inputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/HeltowerInputs.inputactions");
#endif
            if (inputActions == null)
            {
                Debug.LogWarning("[PauseMenuManager] No InputActionAsset assigned \u2014 cannot bind menu action.");
                return;
            }
        }

        InputAction resolved = inputActions.FindAction(menuActionName);
        if (resolved == null)
        {
            Debug.LogWarning($"[PauseMenuManager] Action '{menuActionName}' not found in {inputActions.name}.");
            return;
        }

        if (menuAction == resolved)
            return;

        if (menuAction != null)
        {
            menuAction.started -= OnPauseStarted;
            menuAction.canceled -= OnPauseCanceled;
        }

        menuAction = resolved;
        menuAction.started += OnPauseStarted;
        menuAction.canceled += OnPauseCanceled;
        menuAction.Enable();
        Debug.Log($"[PauseMenuManager] Bound pause toggle to '{menuActionName}' action.");
    }

    private void OnDestroy()
    {
        if (menuAction != null)
        {
            menuAction.started -= OnPauseStarted;
            menuAction.canceled -= OnPauseCanceled;
        }

        UnregisterPausePanel();
    }

    private void OnPauseStarted(InputAction.CallbackContext context)
    {
        if (!_pauseToggleArmed)
            return;

        _pauseToggleArmed = false;

        Debug.Log("ESC key pressed!");

        
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    private void OnPauseCanceled(InputAction.CallbackContext context)
    {
        _pauseToggleArmed = true;
    }

    private void PauseGame()
    {
        // Only disable player input - game continues, enemies still act
        PlayerController.InputEnabled = false;

        ResetMenuView();

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }

        RegisterPausePanel();

        // Freeze time only in single-player (no active network session)
        if (!IsNetworkActive())
        {
            Time.timeScale = 0f;
            _pausedTimeScale = true;
        }

        isPaused = true;

        Debug.Log("Pause menu opened - Player input disabled, enemies still active");
    }

    private void ResumeGame()
    {
        // Re-enable player input
        PlayerController.InputEnabled = true;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        if (classListPanel != null)
            classListPanel.SetActive(false);

        if (firstPanel != null)
            firstPanel.SetActive(true);

        UnregisterPausePanel();

        // Restore time scale if we froze it
        if (_pausedTimeScale)
        {
            Time.timeScale = 1f;
            _pausedTimeScale = false;
        }

        isPaused = false;

        Debug.Log("Pause menu closed - Player input enabled");
    }

    public void OnResumeButtonClicked()
    {
        ResumeGame();
    }


    // ===========================
    // CHARACTER SWITCHING
    // ===========================

    private void OnChangeCharacterClicked()
    {
        if (classListPanel == null)
        {
            Debug.LogWarning("[PauseMenuManager] No classListPanel assigned \u2014 cannot show class list.");
            return;
        }
        firstPanel.SetActive(false);
        bool show = !classListPanel.activeSelf;
        if (show)
            BuildClassButtons();

        classListPanel.SetActive(show);
    }

    private void BuildClassButtons()
    {
        if (_classButtonsBuilt)
            return;

        if (characterSelectionConfig == null || classButtonPrefab == null)
        {
            Debug.LogWarning("[PauseMenuManager] Missing characterSelectionConfig or classButtonPrefab \u2014 cannot build class buttons.");
            return;
        }

        ClassData[] classes = characterSelectionConfig.availableClasses;
        if (classes == null || classes.Length == 0)
        {
            Debug.LogWarning("[PauseMenuManager] CharacterSelectionConfig has no available classes.");
            return;
        }

        Transform parent = classListPanel.transform;
        foreach (ClassData classData in classes)
        {
            if (classData == null)
                continue;

            Button button = Instantiate(classButtonPrefab, parent);
            button.gameObject.SetActive(true);

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = classData.className;

            ClassData captured = classData;
            button.onClick.AddListener(() => SwitchToClass(captured));
        }

        // Hide the source template so it isn't shown next to the generated copies.
        classButtonPrefab.gameObject.SetActive(false);
        _classButtonsBuilt = true;
    }

    private void SwitchToClass(ClassData classData)
    {
        if (classData == null)
            return;

        PlayerController player = PlayerController.GetLocalPlayer();
        if (player == null)
        {
            Debug.LogWarning("[PauseMenuManager] No local player to switch class on.");
            return;
        }

        if (!player.ApplyClassAnimator(classData))
            return;

        ResumeGame();
        Debug.Log($"[PauseMenuManager] Switched to class '{classData.className}'.");
    }

    public void OnQuitToMainMenuClicked()
    {
        PlayerController.InputEnabled = true;
        Time.timeScale = 1f;
        _pausedTimeScale = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StopNetwork();
        CharacterSelectionManager.CleanupRuntimeCharacters();

        Debug.Log("[PauseMenuManager] Loading MainMenu scene...");
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Stubbed: command/arena return flow was removed.
    public void OnQuitToCommandClicked()
    {
        Debug.LogWarning("[PauseMenuManager] Quit to Command is not implemented — command/arena flow was removed.");
    }

    public void OnQuitGameClicked()
    {
        Debug.Log("Quitting game...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ===========================
    // HELPERS
    // ===========================

    private bool IsNetworkActive()
    {
        NetworkManager networkManager = FindFirstObjectByType<NetworkManager>();
        return networkManager != null && (networkManager.IsServerStarted || networkManager.IsClientStarted);
    }

    private void StopNetwork()
    {
        NetworkManager networkManager = FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
            return;

        if (networkManager.IsServerStarted)
            networkManager.ServerManager.StopConnection(true);
        if (networkManager.IsClientStarted)
            networkManager.ClientManager.StopConnection();
    }

    private void RegisterPausePanel()
    {
        if (_registeredWithCursorManager || CursorManager.Instance == null)
            return;

        CursorManager.Instance.PushPanel(ResumeGame);
        _registeredWithCursorManager = true;
    }

    private void UnregisterPausePanel()
    {
        if (!_registeredWithCursorManager || CursorManager.Instance == null)
            return;

        CursorManager.Instance.PopPanel();
        _registeredWithCursorManager = false;
    }

    private void ResetMenuView()
    {
        if (classListPanel != null)
            classListPanel.SetActive(false);

        if (firstPanel != null)
            firstPanel.SetActive(true);
    }
}