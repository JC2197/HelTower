using System;
using System.Collections.Generic;
using UnityEngine;

namespace JoeConticello.ModularCombatCore
{
    [Serializable]
    public sealed class StatContainer
    {
        [SerializeField] private List<StatValue> stats = new List<StatValue>();

        [NonSerialized] private Dictionary<string, StatValue> statLookup;

        public event Action<string, float> StatChanged;
        public event Action AnyStatChanged;

        public void Initialize(StatTypeDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            stats.Clear();

            foreach (StatTypeData statType in database.StatTypes)
            {
                if (statType == null || string.IsNullOrWhiteSpace(statType.StatId))
                    continue;

                stats.Add(new StatValue(statType.StatId, statType.DisplayName, statType.CategoryId, statType.DefaultValue));
            }

            RebuildLookup();
        }

        public bool InitializeFromResources(string resourcePath = "StatTypeDatabase")
        {
            StatTypeDatabase database = Resources.Load<StatTypeDatabase>(resourcePath);
            if (database == null)
                return false;

            Initialize(database);
            return true;
        }

        public int Migrate(StatTypeDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            EnsureLookup();
            int addedCount = 0;

            foreach (StatTypeData statType in database.StatTypes)
            {
                if (statType == null || string.IsNullOrWhiteSpace(statType.StatId) || statLookup.ContainsKey(statType.StatId))
                    continue;

                StatValue stat = new StatValue(statType.StatId, statType.DisplayName, statType.CategoryId, statType.DefaultValue);
                stats.Add(stat);
                statLookup[stat.StatId] = stat;
                addedCount++;
            }

            return addedCount;
        }

        public bool TryGetStat(string statId, out float value)
        {
            EnsureLookup();

            if (!string.IsNullOrWhiteSpace(statId) && statLookup.TryGetValue(statId, out StatValue stat))
            {
                value = stat.CurrentValue;
                return true;
            }

            value = 0f;
            return false;
        }

        public float GetStat(string statId, float fallback = 0f)
        {
            return TryGetStat(statId, out float value) ? value : fallback;
        }

        public bool HasStat(string statId)
        {
            EnsureLookup();
            return !string.IsNullOrWhiteSpace(statId) && statLookup.ContainsKey(statId);
        }

        public bool SetStat(string statId, float value)
        {
            EnsureLookup();
            if (string.IsNullOrWhiteSpace(statId) || !statLookup.TryGetValue(statId, out StatValue stat))
                return false;

            if (Mathf.Approximately(stat.CurrentValue, value))
                return true;

            stat.CurrentValue = value;
            StatChanged?.Invoke(stat.StatId, value);
            AnyStatChanged?.Invoke();
            return true;
        }

        public bool ModifyStat(string statId, float amount)
        {
            return TryGetStat(statId, out float currentValue) && SetStat(statId, currentValue + amount);
        }

        public IReadOnlyList<StatValue> GetStatsByCategory(string categoryId)
        {
            List<StatValue> categoryStats = new List<StatValue>();
            for (int i = 0; i < stats.Count; i++)
            {
                if (stats[i] != null && string.Equals(stats[i].CategoryId, categoryId, StringComparison.OrdinalIgnoreCase))
                    categoryStats.Add(stats[i]);
            }

            return categoryStats;
        }

        public IReadOnlyList<StatValue> GetAllStats()
        {
            return stats;
        }

        private void EnsureLookup()
        {
            if (statLookup == null)
                RebuildLookup();
        }

        private void RebuildLookup()
        {
            statLookup = new Dictionary<string, StatValue>(StringComparer.OrdinalIgnoreCase);
            AddToLookup(stats);
        }

        private void AddToLookup(IEnumerable<StatValue> stats)
        {
            foreach (StatValue stat in stats)
            {
                if (stat != null && !string.IsNullOrWhiteSpace(stat.StatId))
                    statLookup[stat.StatId] = stat;
            }
        }
    }

    [Serializable]
    public sealed class StatValue
    {
        [SerializeField] private string statId;
        [SerializeField] private string displayName;
        [SerializeField] private string categoryId;
        [SerializeField] private float currentValue;

        public string StatId => statId;
        public string DisplayName => displayName;
        public string CategoryId => categoryId;
        public float CurrentValue
        {
            get => currentValue;
            internal set => currentValue = value;
        }

        public StatValue(string statId, string displayName, string categoryId, float currentValue)
        {
            this.statId = statId;
            this.displayName = displayName;
            this.categoryId = categoryId;
            this.currentValue = currentValue;
        }
    }
}