using UnityEngine;
using TMPro;

/// <summary>
/// Displays the character's custom name floating above/below the character in world space.
/// Attach this to a Canvas with RenderMode = World Space, then parent to player character.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class FloatingCharacterName : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nameText;
    
    [Header("Display Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, -0.8f, 0);
    [SerializeField] private float fontSize = 24f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private bool useShadow = true;
    [SerializeField] private bool useOutline = false;
    
    [Header("Billboard Settings")]
    [SerializeField] private bool alwaysFaceCamera = true;
    
    private Transform playerTransform;
    private Canvas canvas;
    private Camera mainCamera;
    private CharacterData currentCharacterData;
    private PlayerController playerController; // Store reference to get synced name
    private Coroutine refreshCoroutine;
    
    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        // Do NOT override renderMode or localScale here.
        // Configure the Canvas component and RectTransform directly on the prefab.
    }
    
    private void Start()
    {
        mainCamera = Camera.main;
        
        // Find player and get character data
        FindPlayerAndSetup();
        
        // Setup text appearance
        SetupTextAppearance();
    }
    
    private void LateUpdate()
    {
        // Position is driven by the prefab's local offset — no runtime override needed
        // when this object is a child of the player.
        // Only apply billboard rotation (for 3D/perspective cameras).
        if (alwaysFaceCamera && mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        }
    }
    
    private void OnEnable()
    {
        PlayerController.OnPlayerSpawned += HandlePlayerSpawned;
    }
    
    private void OnDisable()
    {
        PlayerController.OnPlayerSpawned -= HandlePlayerSpawned;

        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }
    }
    
    private void HandlePlayerSpawned(PlayerController newPlayer)
    {
        FindPlayerAndSetup();
    }
    
    private void FindPlayerAndSetup()
    {
        // Find the PlayerController on this GameObject's parent hierarchy
        // Since this component is part of the PlayerCharacter prefab
        PlayerController player = GetComponentInParent<PlayerController>();
        
        if (player == null)
        {
            // Fallback: search for local player only
            PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                bool isNetworkActive = p.IsServerStarted || p.IsClientStarted;
                bool isLocalPlayer = !isNetworkActive || p.IsOwner;
                if (isLocalPlayer)
                {
                    player = p;
                    break;
                }
            }
        }
        
        if (player != null)
        {
            playerTransform = player.transform;
            playerController = player; // Store reference for synced name access
            currentCharacterData = player.GetCurrentCharacterData();
            
            // Parent directly to the player root (not inside PlayerWorldCanvas) so
            // this canvas keeps its own world-space scale and billboard logic.
            bool alreadyChildOfPlayer = transform.IsChildOf(playerTransform);
            if (!alreadyChildOfPlayer || transform.parent != playerTransform)
            {
                transform.SetParent(playerTransform, false);
                // Restore world-space scale after reparenting (0.01 = 1 world-unit = 100px)
                transform.localScale = Vector3.one * 0.01f;
            }
            
            // Update the display
            UpdateNameDisplay();

            // If sync data is still catching up, keep trying quietly for a short period.
            if (refreshCoroutine != null)
            {
                StopCoroutine(refreshCoroutine);
            }
            refreshCoroutine = StartCoroutine(RetryNameDisplay());
        }
        else
        {
            Debug.LogWarning("[FloatingCharacterName] Player not found!");
        }
    }
    
    private void SetupTextAppearance()
    {
        if (nameText == null)
        {
            Debug.LogWarning("[FloatingCharacterName] Name text is not assigned!");
            return;
        }
        
        nameText.fontSize = fontSize;
        nameText.color = textColor;
        nameText.alignment = TextAlignmentOptions.Center;
        
        // Add shadow for better visibility
        if (useShadow)
        {
            nameText.fontSharedMaterial = new Material(nameText.fontSharedMaterial);
            nameText.fontSharedMaterial.EnableKeyword("UNDERLAY_ON");
        }
        
        // Add outline for better visibility
        if (useOutline)
        {
            nameText.outlineWidth = 0.2f;
            nameText.outlineColor = Color.black;
        }
    }
    
    private void UpdateNameDisplay()
    {
        if (nameText == null)
            return;
        
        string characterName = "";
        
        // Priority 1: Use currentCharacterData if available (local/owner player)
        if (currentCharacterData != null)
        {
            characterName = currentCharacterData.characterName;
        }
        // Priority 2: Use synced character name for remote players
        else if (playerController != null)
        {
            characterName = playerController.GetSyncedCharacterName();

            // Fallback to assigned/saved character name during sync window
            if (string.IsNullOrEmpty(characterName))
                characterName = playerController.GetCharacterSaveName();
        }
        
        if (string.IsNullOrEmpty(characterName))
        {
            return;
        }
        
        // Display the character's custom name (unique characterName, not the class displayName)
        nameText.text = characterName;
        
        Debug.Log($"[FloatingCharacterName] Set name to: {characterName}");
    }

    private System.Collections.IEnumerator RetryNameDisplay()
    {
        const int maxAttempts = 30;
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            UpdateNameDisplay();
            if (nameText != null && !string.IsNullOrEmpty(nameText.text))
                yield break;

            attempts++;
            yield return new WaitForSeconds(0.1f);
        }

        Debug.LogWarning("[FloatingCharacterName] Name data unavailable after retries (currentCharacterData/sync still missing)");
    }
    
    // Public methods for customization
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
    
    public void SetTextColor(Color color)
    {
        textColor = color;
        if (nameText != null)
        {
            nameText.color = color;
        }
    }
    
    public void SetFontSize(float size)
    {
        fontSize = size;
        if (nameText != null)
        {
            nameText.fontSize = size;
        }
    }
    
    public void SetBillboardEnabled(bool enabled)
    {
        alwaysFaceCamera = enabled;
    }
}
