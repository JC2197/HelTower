using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    
    [Header("Scene Names")]
    [SerializeField] private string settingsSceneName = "Settings";
    
    private void Start()
    {
        SetupButtons();
    }
    
    private void SetupButtons()
    {
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
            
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
            
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
    }
    
    private void OnPlayClicked()
    {
        if (BootstrapManager.Instance != null)
            BootstrapManager.Instance.LoadCamp();
        else
            Debug.LogError("[MainMenuManager] BootstrapManager is unavailable — cannot load Camp.");
    }
    
    private void OnSettingsClicked()
    {
        // Load settings scene if you have one
        if (!string.IsNullOrEmpty(settingsSceneName))
        {
            SceneManager.LoadScene(settingsSceneName);
        }
        else
        {
            Debug.Log("Settings scene not configured");
        }
    }
    
    private void OnQuitClicked()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    public void LoadGame()
    {
        OnPlayClicked();
    }
}