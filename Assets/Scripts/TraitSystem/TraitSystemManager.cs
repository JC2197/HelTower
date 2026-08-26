using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Main manager for the trait system. Coordinates between UI and character trait management.
/// Attach this to a persistent game object or UI canvas.
/// </summary>
public class TraitSystemManager : MonoBehaviour
{
    /// <summary>Scene-wide singleton — set on Awake, cleared on destroy.</summary>
    public static TraitSystemManager Instance { get; private set; }
    [Header("References")]
    [SerializeField] private TraitTreeUI traitTreeUI;

    private CharacterTraitManager currentCharacterTraitManager;
    private TraitTree currentTree;
    private List<TraitTree> currentAvailableTrees;
    private int availableGold;
    private SaveFileData currentSaveFile; // Meta progression owner of gold + unlocked nodes

    public System.Action<TraitData> OnTraitUnlocked;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Open trait tree for a specific character
    /// </summary>
    public void OpenTraitTree(GameObject characterObject, string saveFileName)
    {

        // Auto-find TraitTreeUI if not assigned
        if (traitTreeUI == null)
        {
            traitTreeUI = FindFirstObjectByType<TraitTreeUI>(FindObjectsInactive.Include);
            if (traitTreeUI != null)
                Debug.Log($"[TraitSystemManager] Found TraitTreeUI via FindFirstObjectByType: {traitTreeUI.gameObject.name}");
            else
                Debug.LogWarning("[TraitSystemManager] FindFirstObjectByType<TraitTreeUI> returned null — no TraitTreeUI exists in any loaded scene.");
        }

        // Fallback: search inside TraitTreeSceneManager's traitTreeCanvas reference
        if (traitTreeUI == null)
        {
            if (TraitTreeSceneManager.Instance == null)
                Debug.LogWarning("[TraitSystemManager] TraitTreeSceneManager.Instance is null — is the TraitTreeSceneManager in the scene?");
            else
            {
                traitTreeUI = TraitTreeSceneManager.Instance.GetTraitTreeUI();
                if (traitTreeUI != null)
                    Debug.Log($"[TraitSystemManager] Found TraitTreeUI via TraitTreeSceneManager canvas: {traitTreeUI.gameObject.name}");
                else
                    Debug.LogWarning("[TraitSystemManager] TraitTreeSceneManager.GetTraitTreeUI() returned null — is 'Trait Tree Canvas' assigned in the TraitTreeSceneManager Inspector, and does it contain a TraitTreeUI component?");
            }
        }

        // Get or add trait manager to character
        currentCharacterTraitManager = characterObject.GetComponent<CharacterTraitManager>();
        if (currentCharacterTraitManager == null)
        {
            currentCharacterTraitManager = characterObject.AddComponent<CharacterTraitManager>();
        }

        // Prefer the save file already held by CTM so TSM and CTM always mutate the same object.
        PlayerController localPlayer = characterObject.GetComponent<PlayerController>();
        currentSaveFile = currentCharacterTraitManager.GetSaveFileData();

        if (currentSaveFile == null)
            currentSaveFile = localPlayer != null ? localPlayer.GetCurrentSaveFileData() : null;
        if (currentSaveFile == null)
            currentSaveFile = SaveFileSelectionManager.ActiveSaveFile;

        // Keep CTM synced to whatever we resolved
        if (currentCharacterTraitManager.GetSaveFileData() == null && currentSaveFile != null)
        {
            currentCharacterTraitManager.SetSaveFileData(currentSaveFile);
        }
        // Tabs come from the equipped class's available trait trees; nodes unlocked in the
        // save file are cross-referenced against whichever tree actually defines them.
        ClassData classData = localPlayer != null ? localPlayer.GetCurrentCharacterData()?.GetClassData() : null;
        currentAvailableTrees = classData != null && classData.availableTraitTrees != null && classData.availableTraitTrees.Count > 0
            ? classData.availableTraitTrees
            : null;

        currentTree = currentAvailableTrees != null && currentAvailableTrees.Count > 0 ? currentAvailableTrees[0] : null;

        if (currentTree != null)
        {
            Debug.Log($"[TraitSystemManager] Loaded trait tree '{currentTree.name}' with {currentTree.nodes.Count} nodes for save file '{saveFileName}'");
        }
        else
        {
            Debug.LogError($"[TraitSystemManager] No trait tree available — class '{classData?.className ?? "null"}' has no availableTraitTrees assigned.");
            currentSaveFile = null;
            return;
        }

        // Initialize UI
        if (traitTreeUI != null)
        {
            Debug.Log($"[TraitSystemManager] Initializing TraitTreeUI at: {traitTreeUI.gameObject.name}");
            traitTreeUI.SetAvailableTrees(currentAvailableTrees);
            traitTreeUI.Initialize(currentTree, currentCharacterTraitManager);
            traitTreeUI.OnTraitUnlockRequested += OnTraitUnlockRequested;
        }
        else
        {
            Debug.LogError($"[TraitSystemManager] TraitTreeUI is null!");
        }

        // Show UI — activate parent canvas first (mirrors WeaponCraftingSystemManager pattern)
        if (traitTreeUI != null)
        {
            Canvas parentCanvas = traitTreeUI.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null)
                parentCanvas.gameObject.SetActive(true);
            traitTreeUI.gameObject.SetActive(true);
        }

        // Disable player input
        PlayerController.InputEnabled = false;

        // Switch to UI cursor and register ESC close handler
        if (CursorManager.Instance != null)
            CursorManager.Instance.PushPanel(CloseTraitTree);
    }

    /// <summary>
    /// Close the trait tree UI
    /// </summary>
    public void CloseTraitTree()
    {
        if (traitTreeUI != null)
        {
            traitTreeUI.OnTraitUnlockRequested -= OnTraitUnlockRequested;
            traitTreeUI.gameObject.SetActive(false);

            // Deactivate parent canvas (mirrors WeaponCraftingSystemManager pattern)
            Canvas parentCanvas = traitTreeUI.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null)
                parentCanvas.gameObject.SetActive(false);
        }

        // Re-enable player input
        PlayerController.InputEnabled = true;

        // Deregister from ESC stack and switch back to gameplay cursor
        if (CursorManager.Instance != null)
            CursorManager.Instance.PopPanel();

        currentCharacterTraitManager = null;
        currentTree = null;
        currentAvailableTrees = null;
        currentSaveFile = null;
    }

    /// <summary>
    /// Handle a node purchase request from the UI. Gold is the only currency: the node's
    /// goldCost is deducted from the save file, then the trait is unlocked and persisted.
    /// </summary>
    private void OnTraitUnlockRequested(TraitNode node)
    {
        if (currentCharacterTraitManager == null ||
        node == null ||
         node.traitData == null ||
          string.IsNullOrEmpty(node.nodeID))
            return;

        // Re-derive from CTM so TSM and CTM never operate on diverged objects.
        SaveFileData fresh = currentCharacterTraitManager.GetSaveFileData();
        if (fresh != null) currentSaveFile = fresh;

        if (currentSaveFile == null)
        {
            Debug.LogWarning($"[TraitSystemManager] Cannot unlock '{node.nodeID}' — no save file to charge.");
            return;
        }
        int currentLevel = currentCharacterTraitManager.GetTraitLevel(node.nodeID);

        int maxLevel = node.traitData.maxLevel;
            if (currentLevel >= maxLevel)
    {
        Debug.LogWarning(
            $"[TraitSystemManager] '{node.nodeID}' is already maxed."
        );
        return;
    }
        int cost = currentCharacterTraitManager.GetTraitGoldCost(node);
        
        if (!currentSaveFile.SpendGold(cost))
        {
            return;
        }
        if (!currentCharacterTraitManager.UnlockTrait(node.nodeID, node.traitData))
        {
            currentSaveFile.AddGold(cost);
            return;
        }

        availableGold = currentSaveFile.totalGold;
        currentSaveFile.SetUnlockedNodes(currentCharacterTraitManager.GetUnlockedNodeIDs());
        SaveFilePersistence.SaveFile(currentSaveFile);
        OnTraitUnlocked?.Invoke(node.traitData);
        PlayerController.GetLocalPlayer()?.RequestStatsRecalculation();

    }


    /// <summary>
    /// Resolve the authoritative save file: the one opened in the tree, then the local player's,
    /// then the globally active selection.
    /// </summary>
    private SaveFileData ResolveSaveFile()
    {
        if (currentSaveFile != null)
            return currentSaveFile;

        if (currentCharacterTraitManager != null)
        {
            SaveFileData fromManager = currentCharacterTraitManager.GetSaveFileData();
            if (fromManager != null)
                return fromManager;
        }

        PlayerController localPlayer = PlayerController.GetLocalPlayer();
        return localPlayer != null && localPlayer.GetCurrentSaveFileData() != null
            ? localPlayer.GetCurrentSaveFileData()
            : SaveFileSelectionManager.ActiveSaveFile;
    }

    /// <summary>
    /// Get the gold currently available to spend in the trait tree
    /// </summary>
    public int GetAvailableGold()
    {
        return availableGold;
    }

    /// <summary>
    /// Reset all traits for the active save file and refund the gold spent on them.
    /// </summary>
    public void ResetAllTraits()
    {
        if (currentCharacterTraitManager == null)
            return;

        int refund = GetSpentGold(currentCharacterTraitManager.GetUnlockedNodeIDs());
        currentCharacterTraitManager.ResetAllTraits();

        if (currentSaveFile != null)
        {
            currentSaveFile.AddGold(refund);
            currentSaveFile.ClearTraitProgress();
            SaveFilePersistence.SaveFile(currentSaveFile);

            availableGold = currentSaveFile.totalGold;

            PlayerController.GetLocalPlayer()?.RequestStatsRecalculation();
        }

        Debug.Log($"[TraitSystemManager] Reset traits, refunded {refund} gold, total: {availableGold}");
    }

    /// <summary>
    /// Total goldCost of the supplied unlocked nodes across every tree the equipped class exposes
    /// (a node's tab may not be the currently active one).
    /// </summary>
    private int GetSpentGold(HashSet<string> unlockedNodeIDs)
    {
        if (currentAvailableTrees == null || unlockedNodeIDs == null)
            return 0;

        int spent = 0;
        foreach (TraitTree tree in currentAvailableTrees)
        {
            if (tree?.nodes == null) continue;
            foreach (TraitNode node in tree.nodes)
            {
                if (node != null && unlockedNodeIDs.Contains(node.nodeID))
                    spent += currentCharacterTraitManager.GetTraitGoldCost(node);
            }
        }

        return spent;
    }

}
