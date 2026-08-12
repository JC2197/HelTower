using System;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "StatTypeDatabase", menuName = "Modular Combat Core/Stat Type Database")]
public sealed class StatTypeDatabase : ScriptableObject
{

    private static StatTypeDatabase instance;

    public static StatTypeDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<StatTypeDatabase>("StatTypeDatabase");
                if (instance == null)
                {
                    Debug.LogError("StatTypeDatabase not found! Create one at Resources/StatTypeDatabase");
                }
            }
            return instance;
        }
    }
    
    [SerializeField] private List<StatCategoryData> categories = new List<StatCategoryData>();
    [SerializeField] private List<StatTypeData> statTypes = new List<StatTypeData>();

    [NonSerialized] private Dictionary<string, StatTypeData> statLookup;
    [NonSerialized] private Dictionary<string, StatCategoryData> categoryLookup;

    public IReadOnlyList<StatCategoryData> Categories => categories;
    public IReadOnlyList<StatTypeData> StatTypes => statTypes;

    private void OnEnable()
    {
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        statLookup = new Dictionary<string, StatTypeData>(StringComparer.OrdinalIgnoreCase);
        categoryLookup = new Dictionary<string, StatCategoryData>(StringComparer.OrdinalIgnoreCase);

        foreach (StatCategoryData category in categories)
        {
            if (category != null && !string.IsNullOrWhiteSpace(category.CategoryId))
                categoryLookup[category.CategoryId] = category;
        }

        foreach (StatTypeData statType in statTypes)
        {
            if (statType != null && !string.IsNullOrWhiteSpace(statType.StatId))
                statLookup[statType.StatId] = statType;
        }
    }

    public bool TryGetCategory(string categoryId, out StatCategoryData category)
    {
        if (categoryLookup == null)
            RebuildLookup();

        category = null;
        return !string.IsNullOrWhiteSpace(categoryId) && categoryLookup.TryGetValue(categoryId, out category);
    }

    public bool TryGetStatType(string statId, out StatTypeData statType)
    {
        if (statLookup == null)
            RebuildLookup();

        statType = null;
        return !string.IsNullOrWhiteSpace(statId) && statLookup.TryGetValue(statId, out statType);
    }

    public IEnumerable<StatTypeData> GetStatsForCategory(string categoryId)
    {
        foreach (StatTypeData statType in statTypes)
        {
            if (statType != null && string.Equals(statType.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase))
                yield return statType;
        }
    }

    public StatCategoryData AddCategory(string categoryId, string displayName, string description = "")
    {
        StatCategoryData category = new StatCategoryData(categoryId, displayName, description);
        categories.Add(category);
        RebuildLookup();
        return category;
    }

    public StatTypeData AddStat(string statId, string displayName, string categoryId, float defaultValue = 0f)
    {
        StatTypeData statType = new StatTypeData(statId, displayName, categoryId, defaultValue);
        statTypes.Add(statType);
        RebuildLookup();
        return statType;
    }

    public float CalculateStat(string statId, float baseValue, float flatModifier, float percentageModifier)
    {
        return TryGetStatType(statId, out StatTypeData statType)
            ? statType.CalculateFinalValue(baseValue, flatModifier, percentageModifier)
            : (baseValue + flatModifier) * (1f + percentageModifier / 100f);
    }
}

[Serializable]
public sealed class StatCategoryData
{
    [SerializeField] private string categoryId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(1, 3)] private string description;
    [SerializeField] private Color tint = new Color(0.35f, 0.6f, 1f, 1f);
    [SerializeField] private int sortOrder;

    public string CategoryId => categoryId;
    public string DisplayName => displayName;
    public string Description => description;
    public Color Tint => tint;
    public int SortOrder => sortOrder;

    public StatCategoryData(string categoryId, string displayName, string description = "")
    {
        this.categoryId = categoryId;
        this.displayName = displayName;
        this.description = description;
    }
}

[Serializable]
public sealed class StatTypeData
{
    [SerializeField] private string statId;
    [SerializeField] private string displayName;
    [SerializeField] private string categoryId;
    [SerializeField] private Sprite icon;
    [SerializeField] private float defaultValue;
    [SerializeField] private bool isPercentage;
    [SerializeField] private float maxValue;
    [SerializeField, TextArea(2, 4)] private string description;

    public string StatId => statId;
    public string DisplayName => displayName;
    public string CategoryId => categoryId;
    public Sprite Icon => icon;
    public float DefaultValue => defaultValue;
    public bool IsPercentage => isPercentage;
    public float MaxValue => maxValue;
    public string Description => description;

    public float CalculateFinalValue(float baseValue, float flatModifier, float percentageModifier)
    {
        float result = isPercentage
            ? (baseValue + flatModifier / 100f) * (1f + percentageModifier / 100f)
            : (baseValue + flatModifier) * (1f + percentageModifier / 100f);

        return maxValue > 0f ? Mathf.Min(result, maxValue) : result;
    }

    public string FormatValue(float value)
    {
        return isPercentage ? $"{value * 100f:F1}%" : value.ToString("F1");
    }

    public StatTypeData(string statId, string displayName, string categoryId, float defaultValue = 0f)
    {
        this.statId = statId;
        this.displayName = displayName;
        this.categoryId = categoryId;
        this.defaultValue = defaultValue;
    }
}
