using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class ElementAuraRulesTests
    {
        [Test]
        public void OnlyElementalEnemiesAreEnchanted()
        {
            Assert.IsFalse(ElementAuraRules.Style(Element.Neutral).On);
            foreach (Element e in new[] { Element.Fire, Element.Water, Element.Wind, Element.Earth })
                Assert.IsTrue(ElementAuraRules.Style(e).On, e.ToString());
        }

        [Test]
        public void EachElementMovesItsOwnWay()
        {
            Assert.Less(ElementAuraRules.Style(Element.Fire).Gravity, 0f, "embers rise");
            Assert.Greater(ElementAuraRules.Style(Element.Water).Gravity, 0f, "drops fall");
            Assert.Greater(ElementAuraRules.Style(Element.Wind).Swirl, 1f, "wind swirls");
            Assert.Greater(ElementAuraRules.Style(Element.Earth).Gravity, 1f, "grit drops");
        }
    }
}
