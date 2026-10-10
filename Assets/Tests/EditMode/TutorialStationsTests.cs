using Chopper.World;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests
{
    public class TutorialStationsTests
    {
        private static string Key(string action) => "[" + action + "]";

        [Test]
        public void EveryStationHasATitleAndText()
        {
            for (int i = 0; i < TutorialStations.Count; i++)
            {
                var station = (TutorialStations.Station)i;
                Assert.IsNotEmpty(TutorialStations.Title(station), station.ToString());
                string body = TutorialStations.Body(station, Key);
                Assert.Greater(body.Length, 80, station + " says enough to learn from");
                Assert.IsFalse(body.Contains("—") || TutorialStations.Title(station).Contains("—"), station + " has no em dash");
            }
        }

        [Test]
        public void TheSignsNameThePlayersOwnKeys()
        {
            StringAssert.Contains("[Attack]", TutorialStations.Body(TutorialStations.Station.Chopping, Key));
            StringAssert.Contains("[Block]", TutorialStations.Body(TutorialStations.Station.Fighting, Key));
            StringAssert.Contains("[Roll]", TutorialStations.Body(TutorialStations.Station.Fighting, Key));
            StringAssert.Contains("[Interact]", TutorialStations.Body(TutorialStations.Station.Gathering, Key));
            StringAssert.Contains("[Repair]", TutorialStations.Body(TutorialStations.Station.Workbench, Key));
            StringAssert.Contains("[Torch]", TutorialStations.Body(TutorialStations.Station.TorchAndBandage, Key));
        }

        [Test]
        public void SignsStandOutsideThePalisadeAndOffThePond()
        {
            var pond = new Vector2(30f, -20f);
            const float shore = 24f;
            for (int i = 0; i < TutorialStations.Count; i++)
            {
                var station = (TutorialStations.Station)i;
                Vector2 p = TutorialStations.Position(station, pond, shore);
                if (station == TutorialStations.Station.Fishing)
                {
                    Assert.AreEqual(shore + 2f, Vector2.Distance(p, pond), 0.01f, "the fishing sign is on the bank");
                    continue;
                }
                Assert.GreaterOrEqual(p.magnitude, TutorialStations.MinRadius - 0.01f, station + " is outside the palisade");
                Assert.GreaterOrEqual(Vector2.Distance(p, pond), shore + TutorialStations.PondMargin - 0.01f, station + " is off the pond");
            }
        }

        [Test]
        public void TheTrainingTreeStandsBeyondItsSign()
        {
            var pond = new Vector2(-30f, 10f);
            Vector2 sign = TutorialStations.Position(TutorialStations.Station.Fighting, pond, 24f);
            Vector2 dummy = TutorialStations.DummyPosition(pond, 24f);
            Assert.AreEqual(6f, Vector2.Distance(sign, dummy), 0.01f);
            Assert.Greater(dummy.magnitude, sign.magnitude);
        }

        [Test]
        public void TheSessionStartsAndEnds()
        {
            TutorialSession.End();
            Assert.IsFalse(TutorialSession.Active);
            TutorialSession.Begin();
            Assert.IsTrue(TutorialSession.Active);
            TutorialSession.End();
            Assert.IsFalse(TutorialSession.Active);
        }
    }
}
