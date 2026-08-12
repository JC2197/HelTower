using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Specifies which type of traits to roll.
/// </summary>
public enum TraitRollType
{
    General,    // General-purpose buffs and survivability
    Ability     // Ability traits
}

/// <summary>
/// Rolls a set of random, distinct TraitData options for the player to choose from.
/// Rewards are handed out on arena completion (there is no leveling and no tier scaling).
/// Attach this to the Player GameObject.
/// </summary>
public class TraitRoller : MonoBehaviour
{
    private const int ROLL_COUNT = 3;

    [Header("Trait Pool")]
    [Tooltip("Global list of all TraitData assets available for rolling. " +
             "Use the 'Find All TraitDatas' button on the SO to populate.")]
    [SerializeField] private TraitDataList traitDataList;

    /// <summary>
    /// Fired when traits are rolled. TraitRollerUI listens to this to display the options.
    /// </summary>
    public static event Action<List<TraitData>> OnTraitsRolled;

    /// <summary>
    /// Roll a set of options for the local player (e.g. on arena completion).
    /// </summary>
    public List<TraitData> RollTraits(TraitRollType rollType = TraitRollType.General)
    {
        PlayerController player = GetComponent<PlayerController>();
        if (player == null || !player.IsOwner) return new List<TraitData>();

        CharacterData characterData = player.GetCurrentCharacterData();
        if (characterData == null)
        {
            Debug.LogWarning("[TraitRoller] No CharacterData found on player");
            return new List<TraitData>();
        }

        CharacterTraitManager traitManager = GetComponent<CharacterTraitManager>();
        List<TraitData> pool = BuildEligiblePool(characterData, traitManager, rollType);

        Dictionary<string, int> playerTagCounts = traitManager != null
            ? traitManager.GetTraitTagCollection()
            : new Dictionary<string, int>();

        List<TraitData> picked = PickRandomWeighted(pool, ROLL_COUNT, playerTagCounts);
        PublishRolledTraits(picked);
        return picked;
    }

    /// <summary>
    /// Build a pool of TraitData the player hasn't taken yet and is eligible for.
    /// </summary>
    private List<TraitData> BuildEligiblePool(CharacterData characterData, CharacterTraitManager traitManager, TraitRollType rollType)
    {
        List<TraitData> pool = new List<TraitData>();

        if (traitDataList == null || traitDataList.traitGroups == null || traitDataList.traitGroups.Count == 0)
        {
            Debug.LogWarning("[TraitRoller] TraitDataList is not assigned or empty! Assign a TraitDataList SO in the Inspector.");
            return pool;
        }

        foreach (TraitData trait in traitDataList.AllTraits)
        {
            if (trait == null) continue;

            // Skip unique traits the player already has
            if (trait.IsUniqueTraitType && traitManager != null && traitManager.HasTrait(trait))
                continue;

            // Skip if a mutually exclusive trait is already taken
            if (IsBlockedByExclusion(trait, traitManager))
                continue;

            if (!MeetsRequiredTraitPrerequisites(trait, traitManager))
                continue;

            // Filter by roll type
            switch (rollType)
            {
                case TraitRollType.Ability:
                    if (trait.traitType != TraitType.Ability) continue;
                    break;
                case TraitRollType.General:
                default:
                    if (trait.traitType != TraitType.General) continue;
                    List<string> weaponTags = trait.GetWeaponTags();
                    if (weaponTags != null && weaponTags.Count > 0) continue;
                    break;
            }

            if (!MeetsAbilityRequirement(trait, characterData))
                continue;

            pool.Add(trait);
        }

        Debug.Log($"[TraitRoller] Built pool of {pool.Count} eligible {rollType} traits");
        return pool;
    }

    private static bool IsBlockedByExclusion(TraitData trait, CharacterTraitManager traitManager)
    {
        if (trait.mutuallyExclusiveWith == null || traitManager == null)
            return false;

        foreach (TraitData exclusive in trait.mutuallyExclusiveWith)
        {
            if (exclusive != null && traitManager.HasTrait(exclusive))
                return true;
        }
        return false;
    }

    private static bool MeetsRequiredTraitPrerequisites(TraitData trait, CharacterTraitManager traitManager)
    {
        if (trait == null || trait.requiredTraits == null || trait.requiredTraits.Count == 0)
            return true;

        bool hasAnyValidRequirement = false;
        foreach (TraitData req in trait.requiredTraits)
        {
            if (req == null)
                continue;

            hasAnyValidRequirement = true;
            if (traitManager == null || !traitManager.HasTrait(req))
                return false;
        }

        // If requirements are configured but all entries are null/invalid, don't block the roll.
        return !hasAnyValidRequirement;
    }

    /// <summary>
    /// Check if the player owns the ability a trait requires (if any).
    /// </summary>
    private bool MeetsAbilityRequirement(TraitData trait, CharacterData characterData)
    {
        AbilityConfig required = trait.requiredAbility;
        if (required == null && trait.abilityReplacement != null)
            required = trait.abilityReplacement.requiredAbility;

        if (required == null)
            return true;

        if (characterData == null)
            return false;

        // Prefer the live runtime lists (handles abilities granted mid-run via traits).
        CharacterAbilityManager abilityManager = GetComponent<CharacterAbilityManager>();
        if (abilityManager != null)
        {
            if (abilityManager.GetWeaponAbilityRef()?.Config == required) return true;
            if (abilityManager.GetDashAbilityRef()?.Config == required) return true;

            foreach (var abilityRef in abilityManager.GetActiveTraitAbilityRefs())
                if (abilityRef?.Config == required) return true;

            foreach (var abilityRef in abilityManager.GetPassiveTraitAbilityRefs())
                if (abilityRef?.Config == required) return true;

            return false;
        }

        var loadout = characterData.abilityLoadout;
        if (loadout == null) return false;

        if (loadout.WeaponAbility?.Config == required || loadout.DashAbility?.Config == required)
            return true;

        foreach (var abilityRef in loadout.TraitAbilities)
            if (abilityRef?.Config == required) return true;

        return false;
    }

    private void PublishRolledTraits(List<TraitData> rolled)
    {
        if (rolled == null || rolled.Count == 0)
        {
            Debug.LogWarning("[TraitRoller] No eligible traits to roll!");
            return;
        }

        for (int i = 0; i < rolled.Count; i++)
        {
            TraitData t = rolled[i];
            string tagsList = string.Join(", ", t.GetAllTags().Where(tag => !string.IsNullOrEmpty(tag)));
            Debug.Log($"[TraitRoller]  Roll {i + 1}: [{t.traitType}] \"{t.displayName}\" (ID: {t.traitID}, Tags: {tagsList}) — {t.description}");
        }

        OnTraitsRolled?.Invoke(rolled);
    }

    /// <summary>
    /// Fisher-Yates partial shuffle to pick up to 'count' distinct items from the pool.
    /// </summary>
    private List<TraitData> PickRandom(List<TraitData> pool, int count)
    {
        List<TraitData> result = new List<TraitData>();
        if (pool.Count == 0) return result;

        List<TraitData> copy = new List<TraitData>(pool);
        int picks = Mathf.Min(count, copy.Count);

        for (int i = 0; i < picks; i++)
        {
            int rand = UnityEngine.Random.Range(i, copy.Count);
            (copy[i], copy[rand]) = (copy[rand], copy[i]);
            result.Add(copy[i]);
        }

        return result;
    }

    /// <summary>
    /// Pick random traits with weighted selection based on tag synergies.
    /// Traits with tags matching the player's active trait tags get higher weight.
    /// </summary>
    private List<TraitData> PickRandomWeighted(List<TraitData> pool, int count, Dictionary<string, int> playerTagCounts)
    {
        List<TraitData> result = new List<TraitData>();
        if (pool.Count == 0) return result;

        if (playerTagCounts == null || playerTagCounts.Count == 0)
            return PickRandom(pool, count);

        List<TraitData> poolCopy = new List<TraitData>(pool);
        int picks = Mathf.Min(count, poolCopy.Count);

        for (int i = 0; i < picks; i++)
        {
            List<float> weights = new List<float>(poolCopy.Count);
            float totalWeight = 0f;
            for (int j = 0; j < poolCopy.Count; j++)
            {
                float w = CalculateTraitWeight(poolCopy[j], playerTagCounts, result);
                weights.Add(w);
                totalWeight += w;
            }

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            int selectedIndex = 0;
            for (int j = 0; j < weights.Count; j++)
            {
                cumulative += weights[j];
                if (randomValue <= cumulative)
                {
                    selectedIndex = j;
                    break;
                }
            }

            result.Add(poolCopy[selectedIndex]);
            poolCopy.RemoveAt(selectedIndex);
        }

        return result;
    }

    /// <summary>
    /// Calculate weight for a trait based on tag synergies with the player's active traits.
    /// Matching tags use diminishing returns so established synergies matter without dominating rolls.
    /// </summary>
    private float CalculateTraitWeight(TraitData trait, Dictionary<string, int> playerTagCounts, List<TraitData> alreadySelectedTraits = null)
    {
        const float BASE_WEIGHT = 0.5f;
        const float SYNERGY_WEIGHT_SCALE = 0.50f;
        const float MAX_TOTAL_SYNERGY_BONUS = 10f;
        const float MIN_WEIGHT = 0.1f;

        float weight = BASE_WEIGHT;

        List<string> traitTags = trait.GetAllTags()
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        float totalSynergyBonus = 0f;
        foreach (string tag in traitTags)
        {
            if (playerTagCounts.TryGetValue(tag, out int tagCount))
                totalSynergyBonus += Mathf.Log(tagCount + 1f, 2f) * SYNERGY_WEIGHT_SCALE;
        }

        weight += Mathf.Min(totalSynergyBonus, MAX_TOTAL_SYNERGY_BONUS);

        return Mathf.Max(MIN_WEIGHT, weight);
    }
}
