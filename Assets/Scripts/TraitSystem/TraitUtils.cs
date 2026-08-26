using UnityEngine;

public static class TraitUtils
{
    private static float goldCostMultiplier = 3f;

    public static int GetGoldCost(TraitNode node, int totalTraitLevels)
    {
        if (node == null || node.traitData == null)
            return 0;

        float multiplier = Mathf.Pow(
            goldCostMultiplier,
            totalTraitLevels
        );

        return Mathf.Max(
            0,
            Mathf.RoundToInt(
                node.traitData.baseGoldCost * multiplier
            )
        );
    }
}