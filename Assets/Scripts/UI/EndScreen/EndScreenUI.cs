using UnityEngine;
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
    }
    private void OnEnable()
    {
        if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
        // Tree content is loaded via Initialize(); tabs just switch data when clicked.
    }

    private void RestartGame()
    {
        HideEndScreen();
        
    }
    public void ShowEndScreen(int goldEarned)
    {
        SaveFilePersistence.SaveGoldEarned(goldEarned);
        this.goldEarned = goldEarned;

        if (endScreenPanel != null)
            endScreenPanel.SetActive(true);

        if (goldEarnedText != null)
            goldEarnedText.text = $"Gold Earned: {goldEarned}";
    }

    public void HideEndScreen()
    {
        if (endScreenPanel != null)
            endScreenPanel.SetActive(false);
    }
}