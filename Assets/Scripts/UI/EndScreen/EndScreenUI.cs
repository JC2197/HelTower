using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class EndScreenUI : MonoBehaviour
{
    public static EndScreenUI Instance { get; private set; }
    [SerializeField] private GameObject endScreenPanel;
    [SerializeField] private TextMeshProUGUI goldEarnedText;
    [SerializeField] private Button restartButton;

    private int goldEarned;
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
        endScreenPanel.SetActive(false);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
    private void OnEnable()
    {
        if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
        // Tree content is loaded via Initialize(); tabs just switch data when clicked.
    }

    private void RestartGame()
    {
        PlayerController localPlayer = PlayerController.GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.LogWarning("[EndScreenUI] No local player is available to return to Camp.");
            return;
        }

        localPlayer.ServerRpcReturnToCamp();
        HideEndScreen();
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
    {
        if (activeScene.name == "MainMenu")
            Destroy(gameObject);
    }
    public void ShowEndScreen(int goldEarned)
    {
        PlayerController localPlayer = PlayerController.GetLocalPlayer();
        this.goldEarned = localPlayer != null ? localPlayer.BagGold : goldEarned;

        if (endScreenPanel != null)
            endScreenPanel.SetActive(true);

        if (goldEarnedText != null)
            goldEarnedText.text = $"Gold Earned: {this.goldEarned}";
    }

    public void HideEndScreen()
    {
        if (endScreenPanel != null)
            endScreenPanel.SetActive(false);
    }
}