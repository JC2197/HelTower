using UnityEngine;

public static class TraitUtils
{
    public static int GetTraitCost(int startingCost, int totalTraitLevels, float costMultiplier)
    {
        if (startingCost <= 0)
            return 0;

        float multiplier = Mathf.Pow(
            Mathf.Max(1f, costMultiplier),
            Mathf.Max(0, totalTraitLevels)
        );

        return Mathf.Max(
            0,
            Mathf.RoundToInt(
                startingCost * multiplier
            )
        );
    }
}