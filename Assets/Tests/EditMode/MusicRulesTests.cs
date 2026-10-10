using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class MusicRulesTests
    {
        [Test]
        public void EachPlaceHasItsOwnDayMusic()
        {
            Assert.AreEqual(MusicMood.Hub, MusicRules.DayMood(Element.Neutral));
            Assert.AreEqual(MusicMood.Water, MusicRules.DayMood(Element.Water));
            Assert.AreEqual(MusicMood.Fire, MusicRules.DayMood(Element.Fire));
            Assert.AreEqual(MusicMood.Earth, MusicRules.DayMood(Element.Earth));
            Assert.AreEqual(MusicMood.Wind, MusicRules.DayMood(Element.Wind));
        }

        [Test]
        public void DayAndNightCrossWithTheDarkAndTheFightTakesOver()
        {
            Assert.AreEqual(1f, MusicRules.Weight(MusicMood.Hub, Element.Neutral, 0f, false));
            Assert.AreEqual(0f, MusicRules.Weight(MusicMood.Fire, Element.Neutral, 0f, false), "another forest's music is silent");
            Assert.AreEqual(0.5f, MusicRules.Weight(MusicMood.Night, Element.Neutral, 0.5f, false), 0.0001f);
            Assert.AreEqual(0.5f, MusicRules.Weight(MusicMood.Hub, Element.Neutral, 0.5f, false), 0.0001f);
            Assert.AreEqual(1f, MusicRules.Weight(MusicMood.Fight, Element.Fire, 1f, true));
            Assert.AreEqual(0f, MusicRules.Weight(MusicMood.Night, Element.Fire, 1f, true));
            Assert.AreEqual(0f, MusicRules.Weight(MusicMood.Fight, Element.Fire, 1f, false));
        }

        [Test]
        public void EveryMoodHasMusicToFallBackOn()
        {
            for (int i = 0; i < MusicRules.Count; i++)
            {
                var mood = (MusicMood)i;
                Assert.IsTrue(MusicRules.Name(mood).StartsWith("music-"));
                Assert.Greater(MusicRules.Fallback(mood).Length, 0, mood.ToString());
            }
            Assert.AreEqual("music-hub", MusicRules.Name(MusicMood.Hub));
        }
    }
}
