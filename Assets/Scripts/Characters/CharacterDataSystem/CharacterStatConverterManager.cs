using UnityEngine;

/// <summary>
/// Runtime owner for player stat conversions. Configure conversion data here or via Resources/StatConversionData.
/// </summary>
public class CharacterStatConverterManager : MonoBehaviour
{
    [Header("Conversion Data")]
    [SerializeField] private StatConversionData conversionData;

    [Header("References")]
    [SerializeField] private PlayerController playerController;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (conversionData == null)
            conversionData = Resources.Load<StatConversionData>("StatConversionData");
    }

    public void RecalculateLiveStats()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        CharacterData characterData = playerController != null ? playerController.GetCurrentCharacterData() : null;
        StatContainer baseStats = characterData != null ? characterData.statContainer : null;
        StatContainer targetStats = playerController != null ? playerController.AllStats : null;

        if (baseStats == null || targetStats == null)
            return;

        baseStats.CopyToStatContainer(targetStats);
        CharacterStatConverter.ApplyConversions(baseStats, targetStats, conversionData);
    }
}