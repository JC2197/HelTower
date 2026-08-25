using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the trait tree UI - opening, closing, and input handling.
/// IMPORTANT: Attach this to a GameObject in your GAME SCENE alongside TraitSystemManager.
/// The TraitTreeUI should also be in the game scene (initially inactive).
/// </summary>
public class TraitTreeSceneManager : MonoBehaviour
{
    public static TraitTreeSceneManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private TraitSystemManager traitSystemManager;
    [SerializeField] private GameObject traitTreeCanvas;
    [SerializeField] private GameObject playerReference;
    [SerializeField] private GameObject hudCanvas; // HUD to disable during trait tree
    
    private bool isTraitTreeOpen = false;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Returns the TraitTreeUI found inside traitTreeCanvas, or null.</summary>
    public TraitTreeUI GetTraitTreeUI()
    {
        if (traitTreeCanvas == null) return null;
        return traitTreeCanvas.GetComponentInChildren<TraitTreeUI>(true);
    }

    private void OnEnable()
    {
        // Subscribe to player spawn event to get the correct player instance
        PlayerController.OnPlayerSpawned += OnPlayerSpawned;
    }
    
    private void OnDisable()
    {
        PlayerController.OnPlayerSpawned -= OnPlayerSpawned;
    }
    
    private void OnPlayerSpawned(PlayerController player)
    {
        // Only track the local/owning player — remote instances don't have CharacterData loaded
        // and would cause the trait tree to fail with "Unknown" character name.
        if (!player.IsOwner && (player.IsServerStarted || player.IsClientStarted))
        {
            Debug.Log($"[TraitTreeSceneManager] Ignoring remote player spawn (instance {player.gameObject.GetInstanceID()})");
            return;
        }

        Debug.Log($"[TraitTreeSceneManager] Local player spawned! Setting player reference to instance: {player.gameObject.GetInstanceID()}");
        playerReference = player.gameObject;
    }
    
    private void Update()
    {
        // Check for Z key press using InputHelper
        if (InputHelper.GetKeyDown(Key.Z))
        {
            if (isTraitTreeOpen)
            {
                CloseTraitTree();
            }
            else
            {
                OpenTraitTree();
            }
        }
    }
    
    /// <summary>
    /// Open the trait tree UI
    /// </summary>
    public void OpenTraitTree()
    {
        Debug.Log("[TraitTreeSceneManager] OpenTraitTree called");
        
        if (isTraitTreeOpen)
        {
            Debug.Log("[TraitTreeSceneManager] Trait tree already open, ignoring");
            return;
        }
        
        // Find player if not assigned
        if (playerReference == null)
        {
            Debug.Log("[TraitTreeSceneManager] Player reference null, searching for Player tag...");
            PlayerController localPlayer = PlayerController.GetLocalPlayer();
            playerReference = localPlayer != null ? localPlayer.gameObject : null;
        }
        
        if (playerReference == null)
        {
            Debug.LogError("[TraitTreeSceneManager] Cannot open trait tree - no player found!");
            return;
        }
        Debug.Log($"[TraitTreeSceneManager] Player found: {playerReference.name} (Instance ID: {playerReference.GetInstanceID()})");
        
        // Find TraitSystemManager if not assigned
        if (traitSystemManager == null)
        {
            Debug.Log("[TraitTreeSceneManager] TraitSystemManager null, searching in scene...");
            traitSystemManager = FindFirstObjectByType<TraitSystemManager>();
        }
        
        if (traitSystemManager == null)
        {
            Debug.LogError("[TraitTreeSceneManager] TraitSystemManager not found in scene!");
            return;
        }
        Debug.Log($"[TraitTreeSceneManager] TraitSystemManager found: {traitSystemManager.name}");
        
        // Get the save file name — MainMenu doesn't have save-file selection input wired up yet,
        // so fall back through the player's loaded save file to the globally active selection.
        PlayerController playerController = playerReference.GetComponent<PlayerController>();
        Debug.Log($"[TraitTreeSceneManager] PlayerController found: {playerController != null}");

        string saveFileName = playerController != null ? playerController.GetCurrentSaveFileData()?.saveFileName : null;
        if (string.IsNullOrEmpty(saveFileName))
            saveFileName = SaveFileSelectionManager.ActiveSaveFile?.saveFileName;
        Debug.Log($"[TraitTreeSceneManager] Resolved save file name: '{saveFileName}'");

        // saveFileName is only used for logging/identification here — TraitSystemManager
        // independently resolves the authoritative SaveFileData, so an empty name (no
        // save-file selection UI yet) must not block opening the tree.
        if (string.IsNullOrEmpty(saveFileName))
            Debug.LogWarning("[TraitTreeSceneManager] Could not determine save file name — proceeding anyway.");
        
        isTraitTreeOpen = true;
        
        // Open trait tree — TraitSystemManager handles canvas activation, input, and cursor
        Debug.Log($"TraitTreeSceneManager: Opening trait tree for save file '{saveFileName}'");
        traitSystemManager.OpenTraitTree(playerReference, saveFileName);
    }
    
    /// <summary>
    /// Close trait tree and return to game
    /// </summary>
    public void CloseTraitTree()
    {
        if (!isTraitTreeOpen) return;
        
        isTraitTreeOpen = false;
        
        // Close trait tree UI — TraitSystemManager handles canvas deactivation, input, and cursor
        if (traitSystemManager != null)
        {
            traitSystemManager.CloseTraitTree();
        }
    }
    
    /// <summary>
    /// Call this from a UI button to close the trait tree
    /// </summary>
    public void OnCloseButtonPressed()
    {
        CloseTraitTree();
    }
}
