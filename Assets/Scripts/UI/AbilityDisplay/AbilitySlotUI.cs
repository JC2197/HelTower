using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class AbilitySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private TextMeshProUGUI chargeCountText;
    [SerializeField] private TextMeshProUGUI keybindText;
    [SerializeField] private TextMeshProUGUI levelText;

    
    private AbilityReference abilityReference;
    private Ability abilityComponent;
    private bool hasAbility;
    private CharacterTraitManager traitManager;
    private bool isTooltipShowing;
    
    void Awake()
    {
       
        if (cooldownText != null)
        {
            cooldownText.enabled = false;
        }
        
        if (chargeCountText != null)
        {
            chargeCountText.enabled = false;
        }
    }
    
    public void SetAbility(AbilityReference reference, Ability ability = null)
    {
        abilityReference = reference;
        abilityComponent = ability;        
        if (reference != null)
        {
            Debug.Log($"[AbilitySlotUI] Reference.Config is null: {reference.Config == null}");

        }
        
        if (reference != null && reference.Config != null)
        {
            hasAbility = true;
            AbilityConfig config = reference.Config;
            
            // Set icon — prefer trait-overridden icon from DataDrivenAbility
            if (iconImage != null)
            {
                Sprite icon = config.abilityIcon;
                if (ability is DataDrivenAbility dda && dda.EffectiveAbilityIcon != null)
                    icon = dda.EffectiveAbilityIcon;

                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                    cooldownOverlay.sprite = icon;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }
            
            // // Find LevelUpRewardDirector and display ability level
            // CacheLevelUpRewardDirector();
            // UpdateLevelDisplay();
        }
        else
        {
            hasAbility = false;
            
            // Empty slot - hide icon
            if (iconImage != null)
                iconImage.enabled = false;
            
            if (levelText != null)
                levelText.enabled = false;
        }
    }

    /// <summary>
    /// Set the keybind display text (e.g., "LMB", "Space", "1", "2", etc.)
    /// </summary>
    public void SetKeybindText(string keybind)
    {
        if (keybindText != null)
        {
            keybindText.text = keybind;
            keybindText.enabled = !string.IsNullOrEmpty(keybind);
        }
    }
    
    void Update()
    {
        if (hasAbility && abilityComponent != null)
        {
            UpdateCooldownDisplay();
        }
    }
    
    void UpdateCooldownDisplay()
    {
        DataDrivenAbility dataDrivenAbility = abilityComponent as DataDrivenAbility;
        if (dataDrivenAbility == null) return;
        
        var abilityConfig = dataDrivenAbility.GetAbilityConfig();
        if (abilityConfig != null && abilityConfig.isAttack)
        {
            if (chargeCountText != null)
                chargeCountText.enabled = false;

            if (cooldownText != null)
                cooldownText.enabled = false;

            if (cooldownOverlay != null)
            {
                float attackTimeRemaining = dataDrivenAbility.GetRemainingAttackTime();
                cooldownOverlay.enabled = attackTimeRemaining > 0f;
                cooldownOverlay.fillAmount = 1f - dataDrivenAbility.GetAttackProgress();
            }

            return;
        }
        
        float remainingCooldown = dataDrivenAbility.GetRemainingCooldown();
        bool hasCharges = dataDrivenAbility.MaxCharges > 0;
        
        // Update charge counter if ability uses charges
        if (hasCharges)
        {
            // Only show charge count text if max charges > 1
            if (chargeCountText != null)
            {
                bool showCount = dataDrivenAbility.MaxCharges > 1;
                chargeCountText.enabled = showCount;
                if (showCount)
                    chargeCountText.text = dataDrivenAbility.CurrentCharges.ToString();
            }
            
            // Show cooldown animation when recharging (currentCharges < maxCharges)
            bool isRecharging = dataDrivenAbility.CurrentCharges < dataDrivenAbility.MaxCharges;
            
            if (isRecharging)
            {
                // Show cooldown overlay while recharging - use StaminaRechargeProgress for accurate fill
                if (cooldownOverlay != null)
                {
                    cooldownOverlay.enabled = true;
                    cooldownOverlay.fillAmount = 1f - dataDrivenAbility.ChargeRechargeProgress;
                }
                
                // Show countdown text
                if (cooldownText != null)
                {
                    cooldownText.enabled = true;
                    cooldownText.text = remainingCooldown.ToString("F1");
                }
            }
            else
            {
                // Hide cooldown indicators when not recharging or cooldown finished
                if (cooldownOverlay != null)
                {
                    cooldownOverlay.enabled = false;
                }
                
                if (cooldownText != null)
                {
                    cooldownText.enabled = false;
                }
            }
        }
        else
        {
            // No charges - hide charge counter
            if (chargeCountText != null)
            {
                chargeCountText.enabled = false;
            }
            
            // Regular cooldown behavior for non-charge abilities
            if (remainingCooldown > 0)
            {
                // Show cooldown overlay
                if (cooldownOverlay != null)
                {
                    cooldownOverlay.enabled = true;
                    // Fill from bottom to top (1 = full covered, 0 = no cover)
                    // As cooldown decreases, the overlay should "rise up" (decrease fill amount)
                    float cooldownPercent = dataDrivenAbility.GetCooldownPercentage();
                    cooldownOverlay.fillAmount = 1f - cooldownPercent; // Inverse so it rises
                }
                
                // Show countdown text
                if (cooldownText != null)
                {
                    cooldownText.enabled = true;
                    cooldownText.text = remainingCooldown.ToString("F1");
                }
            }
            else
            {
                // Hide cooldown indicators
                if (cooldownOverlay != null)
                {
                    cooldownOverlay.enabled = false;
                }
                
                if (cooldownText != null)
                {
                    cooldownText.enabled = false;
                }
            }
        }
    }
    

    public void OnPointerEnter(PointerEventData eventData)
    {
                isTooltipShowing = true;
        RefreshTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isTooltipShowing = false;
        if (AbilityTooltip.Instance != null)
        {
            AbilityTooltip.Instance.HideTooltip();
        }
    }

    private void RefreshTooltip()
    {
        if (!hasAbility || abilityReference == null || abilityReference.Config == null || AbilityTooltip.Instance == null)
            return;

        AbilityDataConfig dataConfig = abilityReference.Config as AbilityDataConfig;
        DataDrivenAbility dda = abilityComponent as DataDrivenAbility;

        // Use effective (trait-modified) values when available
        float energyCost = dda != null ? dda.EnergyCost : (dataConfig != null ? dataConfig.energyCost : 0f);
        float cooldown = dda != null ? dda.CooldownTime : (dataConfig != null ? dataConfig.cooldownTime : 0f);

        string description = abilityReference.Config.abilityDescription;
        if (dataConfig != null)
            description = AbilityDescriptionBuilder.FormatDescription(description, dataConfig, dda);

        AbilityTooltip.Instance.ShowTooltip(
            abilityReference.Config.abilityName,
            description,
            energyCost,
            cooldown
        );
    }

    private void HandleTraitsChanged()
    {
        if (isTooltipShowing)
            RefreshTooltip();
    }

    void OnDestroy()
    {
        if (traitManager != null)
            traitManager.OnTraitsChanged -= HandleTraitsChanged;
    }

    // private void CacheLevelUpRewardDirector()
    // {
    //     if (rewardDirector != null) return;

    //     PlayerController player = PlayerController.GetLocalPlayer();
    //     if (player != null)
    //     {
    //         rewardDirector = player.GetComponent<LevelUpRewardDirector>();
    //         if (rewardDirector != null)
    //             rewardDirector.OnAbilityLevelChanged += HandleAbilityLevelChanged;

    //         if (traitManager == null)
    //         {
    //             traitManager = player.GetComponent<CharacterTraitManager>();
    //             if (traitManager != null)
    //                 traitManager.OnTraitsChanged += HandleTraitsChanged;
    //         }
    //     }
    // }

    // private void HandleAbilityLevelChanged(AbilityRewardProgression progression)
    // {
    //     if (!hasAbility || abilityReference?.Config == null) return;
        
    //     string abilityID = abilityReference.Config.name;
    //     if (string.Equals(progression.abilityID, abilityID, System.StringComparison.OrdinalIgnoreCase))
    //     {
    //         UpdateLevelDisplay();
    //     }
    // }

    // private void UpdateLevelDisplay()
    // {
    //     if (levelText == null) return;

    //     if (!hasAbility || abilityReference?.Config == null)
    //     {
    //         levelText.enabled = false;
    //         return;
    //     }

    //     int level = 1;
    //     if (rewardDirector != null && rewardDirector.CharacterData != null)
    //     {
    //         string abilityID = abilityReference.Config.name;
    //         var progressionList = rewardDirector.CharacterData.AbilityRewardProgressionList;
    //         if (progressionList != null)
    //         {
    //             var record = progressionList.Find(r =>
    //                 string.Equals(r.abilityID, abilityID, System.StringComparison.OrdinalIgnoreCase));
    //             if (record != null)
    //                 level = Mathf.Max(1, record.level);
    //         }
    //     }

    //     levelText.text = $"{level}";
    //     levelText.enabled = true;
    // }
}
