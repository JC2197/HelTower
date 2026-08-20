using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single-scene main menu navigator.
/// Manages panel show/hide state across MainMenu, PlayOptions,
/// CharacterOptions, NewCharacter, and Settings groups.
/// No scene loads occur until the player actually enters gameplay.
/// </summary>
public class MainMenu : MonoBehaviour
{
    private const string TransitionLogTag = "[MainMenu->Command]";

    // ── Panels ────────────────────────────────────────────────────────────────

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    // ── Main Menu Panel ───────────────────────────────────────────────────────

    [Header("Main Menu Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;

    // ── Settings Panel ────────────────────────────────────────────────────────

    [Header("Settings Panel")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Button settingsBackButton;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (Time.timeScale != 1f)
        {
            Debug.LogWarning($"[MainMenu] Time.timeScale was {Time.timeScale}, resetting to 1 for menu/transition flow");
            Time.timeScale = 1f;
        }

        // UI needs a visible, unlocked cursor.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetupButtons();
        SetupAudio();
        ShowPanel(mainMenuPanel);
    }
    

    private void SetupButtons()
    {
        // Main menu
        playButton?.onClick.AddListener(OnPlay);
        optionsButton?.onClick.AddListener(OnOptions);
        quitButton?.onClick.AddListener(QuitGame);
        // Settings
        settingsBackButton?.onClick.AddListener(BackToMainMenu);
    }

    private void SetupAudio()
    {
        if (volumeSlider != null)
        {
            volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            AudioListener.volume = volumeSlider.value;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    // ── Panel Navigation ──────────────────────────────────────────────────────

    private void ShowPanel(GameObject target)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(mainMenuPanel == target);
        if (settingsPanel != null) settingsPanel.SetActive(settingsPanel == target);

    }

    // Main menu → Play options
    public void OnPlay()
    {
        // start game
    }

    // Main menu → Settings
    public void OnOptions()
    {
        ShowPanel(settingsPanel);
    }

    // Back paths
    public void BackToMainMenu() => ShowPanel(mainMenuPanel);


    // ── Settings ──────────────────────────────────────────────────────────────

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("MasterVolume", volume);
        PlayerPrefs.Save();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}