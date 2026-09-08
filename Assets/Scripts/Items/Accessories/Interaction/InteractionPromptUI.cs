using UnityEngine;
using TMPro;

/// <summary>
/// Simple UI to display interaction prompts above the player or at a fixed screen position
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TextMeshProUGUI promptText;
    
    [Header("Settings")]
    [SerializeField] private string interactKeyName = "F";
    [SerializeField] private bool followPlayer = false;
    [SerializeField] private Vector3 playerOffset = new Vector3(0, 1.5f, 0);
    
    private static InteractionPromptUI instance;
    private Transform playerTransform;
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Start hidden
        if (promptPanel != null)
        {
            promptPanel.SetActive(false);
        }
    }
    
    void Start()
    {
        // Find local player
        PlayerController player = PlayerController.GetLocalPlayer();
        if (player != null)
        {
            playerTransform = player.transform;
        }
        
        // Verify camera exists for screen positioning
        if (followPlayer && Camera.main == null)
        {
            Debug.LogWarning("[InteractionPromptUI] No main camera found! UI positioning may not work correctly.");
        }
    }
    
    void LateUpdate()
    {
        if (followPlayer && playerTransform != null && promptPanel != null && promptPanel.activeSelf)
        {
            // Convert world position to screen position
            Vector3 worldPos = playerTransform.position + playerOffset;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            
            // Only update if on screen
            if (screenPos.z > 0)
            {
                transform.position = screenPos;
            }
        }
    }
    
    public static void Show(string message)
    {
        if (instance == null)
        {
            Debug.LogWarning("[InteractionPromptUI] No instance found!");
            return;
        }
        
        instance.ShowPrompt(message);
    }
    
    public static void Hide()
    {
        if (instance == null) return;
        
        instance.HidePrompt();
    }
    
    private void ShowPrompt(string message)
    {
        if (promptPanel == null || promptText == null)
        {
            Debug.LogWarning("[InteractionPromptUI] UI elements not assigned!");
            return;
        }
        
        // Format the prompt text with the interaction key
        promptText.text = $"[{interactKeyName}] {message}";
        promptPanel.SetActive(true);
    }
    
    private void HidePrompt()
    {
        if (promptPanel != null)
        {
            promptPanel.SetActive(false);
        }
    }
}
