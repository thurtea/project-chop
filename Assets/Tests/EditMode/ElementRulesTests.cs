using System;
using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class ElementRulesTests
    {
        private static readonly Element[] AllElements = (Element[])Enum.GetValues(typeof(Element));

        [TestCase(Element.Water, Element.Fire)]
        [TestCase(Element.Fire, Element.Wind)]
        [TestCase(Element.Wind, Element.Earth)]
        [TestCase(Element.Earth, Element.Water)]
        public void CyclePairBeats(Element attacker, Element defender)
        {
            Assert.IsTrue(ElementRules.Beats(attacker, defender));
        }

        [TestCase(Element.Fire, Element.Water)]
        [TestCase(Element.Wind, Element.Fire)]
        [TestCase(Element.Earth, Element.Wind)]
        [TestCase(Element.Water, Element.Earth)]
        public void ReversePairDoesNotBeat(Element attacker, Element defender)
        {
            Assert.IsFalse(ElementRules.Beats(attacker, defender));
        }

        [Test]
        public void NeutralNeverBeatsAnything()
        {
            foreach (Element other in AllElements)
            {
                Assert.IsFalse(ElementRules.Beats(Element.Neutral, other), $"Neutral beat {other}");
            }
        }

        [Test]
        public void NothingBeatsNeutral()
        {
            foreach (Element other in AllElements)
            {
                Assert.IsFalse(ElementRules.Beats(other, Element.Neutral), $"{other} beat Neutral");
            }
        }

        [Test]
        public void NoElementBeatsItself()
        {
            foreach (Element element in AllElements)
            {
                Assert.IsFalse(ElementRules.Beats(element, element), $"{element} beat itself");
            }
        }

        [Test]
        public void ExactlyFourWinningPairsExist()
        {
            int wins = 0;
            foreach (Element a in AllElements)
            {
                foreach (Element b in AllElements)
                {
                    if (ElementRules.Beats(a, b)) wins++;
                }
            }
            Assert.AreEqual(4, wins);
        }
    }
}
