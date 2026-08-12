using UnityEngine;

/// <summary>
/// Life steal mode: heal a fixed amount or a percentage of damage dealt.
/// </summary>
public enum LifeStealType
{
    Flat,
    Percentage
}

/// <summary>
/// Configuration for life steal on a sub-ability hit.
/// Attach this to any sub-ability config (MeleeConfig, ProjectileConfig, etc.) to
/// grant healing to the ability owner whenever a hit connects.
///
/// For summons and constructs the healing always targets the player who owns the
/// ability, not the summon/construct itself.
/// </summary>
[System.Serializable]
public class LifeStealConfig
{
    [Tooltip("Enable life steal for this ability.")]
    public bool enabled = false;

    [Tooltip("Flat: heal a fixed HP amount per hit.\nPercentage: heal a fraction of the damage dealt (0–100).")]
    public LifeStealType type = LifeStealType.Percentage;

    [Tooltip("Flat mode: HP restored per hit.\nPercentage mode: percentage of damage dealt restored as HP (e.g. 5 = 5%).")]
    [Range(0f, 100f)]
    public float amount = 5f;
}

/// <summary>
/// Utility class for applying life steal healing after a damage hit.
/// </summary>
public static class LifeStealProcessor
{
    /// <summary>
    /// Apply life steal healing to <paramref name="healTarget"/> based on
    /// <paramref name="damageDone"/> and the supplied <paramref name="config"/>.
    /// Pass the player-owner as <paramref name="healTarget"/> to ensure that
    /// summon/construct damage heals the player, not the summon/construct.
    /// </summary>
    public static void Apply(LifeStealConfig config, float damageDone, GameObject healTarget)
    {
        if (config == null || !config.enabled || damageDone <= 0f || healTarget == null)
            return;

        float healAmount = config.type == LifeStealType.Flat
            ? config.amount
            : damageDone * (config.amount / 100f);

        if (healAmount <= 0f)
            return;

        Organism organism = healTarget.GetComponent<Organism>();
        if (organism != null)
            organism.Heal(healAmount);
    }
}
