using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
public class PlayerHUD : MonoBehaviour
{
    [Header("Resource Bars")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Image energyFillImage;
    [SerializeField] private Image goldIcon;
    [SerializeField] private TextMeshProUGUI totalGold;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private GameObject BossHealthPanel;
    private PlayerController player;
    
    void Start()
    {
        FindAndInitializePlayer();
    }
    
    void OnEnable()
    {
        Organism.OnHealthChanged += HandleHealthChanged;
        Organism.OnEnergyChanged += HandleEnergyChanged;
        StatContainer.OnAnyStatChanged += HandleStatsChanged;
        PlayerController.OnPlayerSpawned += HandlePlayerSpawned;
        PlayerController.OnLocalPlayerSceneChanged += HandlePlayerSceneChanged;
        PlayerController.OnBagGoldChanged += HandleBagGoldChanged;
        SaveFileData.OnGoldChanged += HandleGoldChanged;
        // Find player immediately when enabled (important for scene transitions)
        if (player == null)
        {
            player = PlayerController.GetLocalPlayer();
        }
        
        RefreshAllDisplays();
    }
    private void HandleGoldChanged(int newTotalGold)
    {
        if (totalGold != null && !IsInGameScene())
        {
            totalGold.text = newTotalGold.ToString();
        }
    }

    private void HandleBagGoldChanged(PlayerController changedPlayer, int newBagGold)
    {
        if (changedPlayer == player && IsInGameScene())
            UpdateGoldDisplay(newBagGold);
    }
    private void HandlePlayerSpawned(PlayerController newPlayer)
    {
        // Only update HUD for local player
        bool isNetworkActive = newPlayer.IsServerStarted || newPlayer.IsClientStarted;
        bool isLocalPlayer = !isNetworkActive || newPlayer.IsOwner;
        
        if (!isLocalPlayer) return;
        
        player = newPlayer;
        RefreshAllDisplays();
    }

    private void HandlePlayerSceneChanged(PlayerController movedPlayer)
    {
        player = movedPlayer;
        RefreshAllDisplays();
        UpdateAbilities(player.GetCurrentCharacterData());
    }
    
    void FindAndInitializePlayer()
    {
        player = PlayerController.GetLocalPlayer();
        
        if (player != null)
        {
            // Initialize with current values
            UpdateHealthDisplay(player);
            UpdateEnergyDisplay(player);
        }
        else
        {
            // Retry after a short delay
            Invoke(nameof(FindAndInitializePlayer), 0.1f);
        }
    }
    
    void OnDisable()
    {
        Organism.OnHealthChanged -= HandleHealthChanged;
        Organism.OnEnergyChanged -= HandleEnergyChanged;
        StatContainer.OnAnyStatChanged -= HandleStatsChanged;
        PlayerController.OnPlayerSpawned -= HandlePlayerSpawned;
        PlayerController.OnLocalPlayerSceneChanged -= HandlePlayerSceneChanged;
        PlayerController.OnBagGoldChanged -= HandleBagGoldChanged;
        SaveFileData.OnGoldChanged -= HandleGoldChanged;
    }
    
    void HandleHealthChanged(Organism organism, float newHealth)
    {
        if (organism == player)
        {
            UpdateHealthDisplay(organism);
        }
    }
    
    void HandleEnergyChanged(Organism organism, float newEnergy)
    {
        if (organism == player)
        {
            UpdateEnergyDisplay(organism);
        }
    }
    
    void HandleStatsChanged()
    {
        if (player != null)
        {
            UpdateHealthDisplay(player);
            UpdateEnergyDisplay(player);
        }
    }
    
    void UpdateHealthDisplay(Organism organism)
    {
        if (healthFillImage != null)
            healthFillImage.fillAmount = organism.GetHealthPercentage();
        
        if (healthText != null)
            healthText.text = $"{organism.CurrentHealth:F0}/{organism.MaxHealth:F0}";
    }
    
    void UpdateEnergyDisplay(Organism organism)
    {
        if (energyFillImage != null)
            energyFillImage.fillAmount = organism.GetEnergyPercentage();
        
        if (energyText != null)
            energyText.text = $"{organism.CurrentEnergy:F0}/{organism.MaxEnergy:F0}";
    }
    

    public void UpdateAbilities(CharacterData characterData)
    {
        // This will be called to update ability slots
        // Implementation handled by AbilitySlotUI components
    }

    public void UpdateGoldDisplay(int newTotalGold)
    {
        if (totalGold != null)
        {
            totalGold.text = newTotalGold.ToString();
        }
    }
    
    /// <summary>
    /// Refresh all HUD displays (health, energy, force field). Call this after stat changes.
    /// </summary>
    public void RefreshAllDisplays()
    {
        if (player != null)
        {
            UpdateHealthDisplay(player);
            UpdateEnergyDisplay(player);
            SaveFileData saveFileData = player.GetCurrentSaveFileData();
            UpdateGoldDisplay(IsInGameScene() ? player.BagGold : saveFileData != null ? saveFileData.totalGold : 0);
        }
    }

    private static bool IsInGameScene()
    {
        return SceneManager.GetActiveScene().name == "GameScene";
    }
}