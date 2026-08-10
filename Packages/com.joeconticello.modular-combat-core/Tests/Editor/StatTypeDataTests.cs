using UnityEngine;
using NUnit.Framework;

namespace JoeConticello.ModularCombatCore.Tests
{
    public sealed class StatTypeDataTests
    {
        [Test]
        public void AbsoluteStatAppliesFlatThenPercentageModifier()
        {
            StatTypeData stat = new StatTypeData("MaxHealth", "Max Health", "Core", 100f);

            Assert.That(stat.CalculateFinalValue(100f, 20f, 10f), Is.EqualTo(132f).Within(0.001f));
        }

        [Test]
        public void StatContainerGroupsByEditorDefinedCategory()
        {
            StatContainer container = new StatContainer();
            StatTypeDatabase database = ScriptableObject.CreateInstance<StatTypeDatabase>();
            database.AddCategory("Core", "Core Stats");
            database.AddStat("MaxHealth", "Max Health", "Core", 100f);

            container.Initialize(database);

            Assert.That(container.HasStat("MaxHealth"), Is.True);
            Assert.That(container.GetStatsByCategory("Core").Count, Is.EqualTo(1));
        }
    }
}