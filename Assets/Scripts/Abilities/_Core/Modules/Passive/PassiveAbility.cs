using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Configuration for custom passive effects. The shared <see cref="CustomPassiveEffectConfig"/> prefab is instantiated
/// at spawn time.
/// </summary>

public class PassiveAbility : MonoBehaviour, ISubAbility
{
    protected PassiveConfig passiveConfig;
    protected PassiveAbilityConfigBase passiveAbilityConfig;
    protected DataDrivenAbility sourceAbility;
    private GameObject owner;
    private GameObject statOwner;
    private string abilityName;
    private AbilityDataConfig parentConfig;
    private List<string> abilityTags;
    private Organism organism;
    private StatContainer statContainer;

    public virtual void Initialize(AbilityDataConfig abilityConfig, DataDrivenAbility source, PassiveConfig runtimePassiveConfig = null, PassiveAbilityConfigBase runtimePassiveAsset = null)
    {
        sourceAbility = source;
        passiveConfig = runtimePassiveConfig;
        passiveAbilityConfig = runtimePassiveAsset;

        // Spawn optional visuals from the passive authoring data.
        GameObject visualPrefab = runtimePassiveAsset != null
            ? runtimePassiveAsset.PassiveVisualsPrefab
            : runtimePassiveConfig != null
                ? runtimePassiveConfig.PassiveVisualsPrefab
                : null;

        if (visualPrefab != null)
        {
            GameObject visuals = Instantiate(visualPrefab, transform.position, Quaternion.identity, transform);
            visuals.name = $"{visualPrefab.name}_PassiveVisual";
        }
    }

    private void Awake()
    {
        // Ensure the passive ability is attached to a GameObject with an Organism component
        organism = GetComponent<Organism>();
        if (organism == null)
        {
            Debug.LogError($"[ThornsPassive] No Organism component found on {gameObject.name}! Thorns requires Organism.");
            enabled = false;
            return;
        }
    }
    public void SetContext(SubAbilityContext context)
    {
        parentConfig = context.parentConfig;
        owner = context.owner;
        statOwner = context.statOwner != null ? context.statOwner : context.owner;
        abilityName = context.AbilityName;
        abilityTags = context.AbilityTags;
    }

    private void OnEnable()
    {
    }
    

}

