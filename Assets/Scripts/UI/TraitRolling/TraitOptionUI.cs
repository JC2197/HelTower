using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// UI component for a single trait option in the TraitRollerUI.
/// 
/// Prefab structure (user creates layout):
/// - TraitOption (this component + Button on root — entire option is clickable)
///   - OutlineImage (Image) — colored by colorTheme
///   - TraitName (TMP_Text)
///   - TraitDescription (TMP_Text)
///   - TraitTypeLabel (TMP_Text) — shows "Stat" / "Ability" / "Ultimate"
///   - AbilityBox (GameObject) — only visible for Ability-type traits
///     - AbilityBoxOutline (Image) — colored by ability theme
///     - AbilityBoxIcon (Image) — trait icon
/// </summary>
public class TraitOptionUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Text Fields")]
    [SerializeField] private TMP_Text traitNameText;
    [SerializeField] private TMP_Text traitDescriptionText;
    [SerializeField] private TMP_Text traitTypeLabel;
    [SerializeField] private TMP_Text traitTagsLabel; // New field for displaying tags
    
    [Header("Outline")]
    [Tooltip("Main outline image — gets tinted from trait's color theme")]
    [SerializeField] private Image outlineImage;
    
    [Header("Ability Box")]
    [Tooltip("Root object for ability display — hidden for non-Ability traits")]
    [SerializeField] private GameObject abilityBox;
    [SerializeField] private Image abilityBoxOutline;
    [SerializeField] private Image abilityBoxIcon;
    
    private TraitRollerUI parentRollerUI;
    private int optionIndex;
    private TraitData currentTrait;
    private Vector3 baseScale;

    [Header("Hover")]
    [Tooltip("Additional scale applied on hover. 0.25 means 1.25x base scale.")]
    [SerializeField] private float hoverScaleBonus = 0.25f;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip selectSound;
    private void Awake()
    {
        parentRollerUI = GetComponentInParent<TraitRollerUI>();
        baseScale = transform.localScale;
        
        // The entire option is clickable — add/find Button on root
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnSelectClicked);
        }
        
        // Determine index based on sibling position for click callback
        optionIndex = transform.GetSiblingIndex();
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
    }
    
    /// <summary>
    /// Populate this option with trait data.
    /// Called by TraitRollerUI when traits are rolled.
    /// </summary>
    public void Populate(TraitData trait)
    {
        currentTrait = trait;
        
        // ── Name & Description ──────────────────────────────────────────
        if (traitNameText != null)
        {

                traitNameText.text = trait.displayName;

        }
        
        if (traitDescriptionText != null)
        {
            // Use dynamic description builder to show actual scaled values
            string dynamicDescription = TraitDescriptionBuilder.BuildDynamicDescription(trait);
            traitDescriptionText.text = dynamicDescription;
        }
        
        // ── Trait Type Label ────────────────────────────────────────────
        if (traitTypeLabel != null)
        {
            switch (trait.traitType)
            {
                case TraitType.General:
                    traitTypeLabel.text = "General";
                    break;
                case TraitType.Ability:
                    traitTypeLabel.text = "Ability";
                    break;
                case TraitType.AbilityUpgrade:
                    traitTypeLabel.text = "Ability Upgrade";
                    break;
                case TraitType.Keystone:
                    traitTypeLabel.text = "Keystone";
                    break;
                default:
                    traitTypeLabel.text = trait.traitType.ToString();
                    break;
            }
        }
        
        // ── Trait Tags Display ──────────────────────────────────────────
        PopulateTagsLabel(trait);
        
        // ── Outline Color ───────────────────────────────────────────
        // ApplyOutlineColor(trait);
        
        // ── Ability Box ─────────────────────────────────────────────────
        bool showAbilityBox = trait.traitIcon != null;
        
        if (abilityBox != null)
        {
            abilityBox.SetActive(showAbilityBox);
            
            if (showAbilityBox)
            {
                // TagDatabase tagDB = TagDatabase.Instance;
                
                // // Color the outline using the trait's primary color theme
                // if (abilityBoxOutline != null)
                // {
                //     if (tagDB != null && !string.IsNullOrEmpty(trait.colorTheme))
                //         abilityBoxOutline.color = tagDB.GetPrimaryColor(trait.colorTheme);
                //     else
                //         abilityBoxOutline.color = Color.white;
                // }
                
                // Set trait icon
                if (abilityBoxIcon != null)
                {
                    abilityBoxIcon.sprite = trait.traitIcon;
                    abilityBoxIcon.enabled = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Apply the color theme to the outline image.
    /// </summary>
    // private void ApplyOutlineColor(TraitData trait)
    // {
    //     if (outlineImage == null) return;
        
    //     TagDatabase tagDB = TagDatabase.Instance;
    //     if (tagDB == null || string.IsNullOrEmpty(trait.colorTheme))
    //     {
    //         outlineImage.color = Color.white;
    //         return;
    //     }
        
    //     outlineImage.color = tagDB.GetPrimaryColor(trait.colorTheme);
    // }
    
    /// <summary>
    /// Populate the tags label with the trait's tags.
    /// Shows core tag first, then specialized tags, formatted with colors.
    /// </summary>
    private void PopulateTagsLabel(TraitData trait)
    {
        if (traitTagsLabel == null) return;
        
        List<string> tagsToShow = new List<string>();
        
        // // Add specialized tags
        // if (!string.IsNullOrEmpty(trait.specializedTraitTag1))
        //     tagsToShow.Add(trait.specializedTraitTag1);
        // if (!string.IsNullOrEmpty(trait.specializedTraitTag2))
        //     tagsToShow.Add(trait.specializedTraitTag2);
        // if (!string.IsNullOrEmpty(trait.specializedTraitTag3))
        //     tagsToShow.Add(trait.specializedTraitTag3);
        
        // Add weapon tags
        if (!string.IsNullOrEmpty(trait.weaponTraitTag))
            tagsToShow.Add(trait.weaponTraitTag);
        
        if (tagsToShow.Count == 0)
        {
            traitTagsLabel.text = "";
            return;
        }
        
        // // Build formatted tag string with colors from TagDatabase
        // TagDatabase tagColors = TagDatabase.Instance;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        for (int i = 0; i < tagsToShow.Count; i++)
        {
            string tag = tagsToShow[i];
            
            // // Get color directly from TagDatabase
            // if (tagColors != null)
            // {
            //     Color tagColor = tagColors.GetPrimaryColor(tag);
            //     string hexColor = ColorUtility.ToHtmlStringRGB(tagColor);
            //     sb.Append($"<color=#{hexColor}>{tag}</color>");
            // }
            // else
            // {
            //     sb.Append(tag);
            // }
            
            // Add separator between tags
            if (i < tagsToShow.Count - 1)
                sb.Append(" • ");
        }
        
        traitTagsLabel.text = sb.ToString();
    }
    
    private void OnSelectClicked()
    {
        if (parentRollerUI != null)
        {
            parentRollerUI.OnTraitSelected(optionIndex);
            AudioManager.Instance.Play2DSound(selectSound);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = baseScale * (1f + hoverScaleBonus);
        AudioManager.Instance.Play2DSound(hoverSound);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = baseScale;
    }
}
