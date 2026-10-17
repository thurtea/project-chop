using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class FishShowTests
    {
        [Test]
        public void LeapRisesAndFalls()
        {
            Assert.AreEqual(0f, FishShow.LeapHeight(0f));
            Assert.AreEqual(FishShow.LeapPeak, FishShow.LeapHeight(FishShow.LeapSeconds * 0.5f), 1e-4f);
            Assert.AreEqual(0f, FishShow.LeapHeight(FishShow.LeapSeconds));
            Assert.Less(FishShow.LeapHeight(FishShow.LeapSeconds * 0.2f), FishShow.LeapPeak);
        }

        [Test]
        public void LeapsOnlyWhileTheFishFights()
        {
            Assert.IsTrue(FishShow.LeapsIn(FishPhase.Pulling));
            Assert.IsTrue(FishShow.LeapsIn(FishPhase.Thrashing));
            Assert.IsFalse(FishShow.LeapsIn(FishPhase.Calm));
            Assert.That(FishShow.NextLeapGap(0f), Is.EqualTo(FishShow.LeapMinGap));
            Assert.That(FishShow.NextLeapGap(1f), Is.EqualTo(FishShow.LeapMaxGap));
        }

        [Test]
        public void RunCutStartsWhenTheFishRuns()
        {
            Assert.IsTrue(FishShow.StartsRunCut(FishPhase.Calm, FishPhase.Pulling));
            Assert.IsFalse(FishShow.StartsRunCut(FishPhase.Pulling, FishPhase.Pulling));
            Assert.IsFalse(FishShow.StartsRunCut(FishPhase.Pulling, FishPhase.Calm));
        }

        [Test]
        public void LineThickensWithDistance()
        {
            Assert.AreEqual(1f, FishShow.LineScale(2f));
            Assert.AreEqual(1.5f, FishShow.LineScale(FishShow.LineGrowFrom * 1.5f), 1e-4f);
            Assert.AreEqual(1f, FishShow.ArcScale(3f));
            Assert.AreEqual(FishShow.ArcMaxScale, FishShow.ArcScale(500f));
            Assert.LessOrEqual(FishShow.ArcMaxScale, 1.5f);
            Assert.AreEqual(FishShow.LineMaxScale, FishShow.LineScale(500f));
        }
    }
}
