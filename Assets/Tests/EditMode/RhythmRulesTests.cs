using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class RhythmRulesTests
    {
        const float B = RhythmRules.Beat;

        [Test]
        public void StrikeBeatsAreOnTheBeatAndTheThirdBeatRests()
        {
            Assert.AreEqual(RhythmHit.OnBeat, RhythmRules.Judge(0f));
            Assert.AreEqual(RhythmHit.OnBeat, RhythmRules.Judge(B + 0.08f));
            Assert.AreEqual(RhythmHit.Rest, RhythmRules.Judge(2 * B));
            Assert.AreEqual(RhythmHit.OnBeat, RhythmRules.Judge(3 * B - 0.1f));
            Assert.AreEqual(RhythmHit.OffBeat, RhythmRules.Judge(0.2f));
            Assert.AreEqual(RhythmHit.OnBeat, RhythmRules.Judge(4 * B));
        }

        [Test]
        public void ABarStruckOnEveryBeatCompletesTheCombo()
        {
            int bar = -1, mask = 0;
            Assert.IsFalse(RhythmRules.Register(ref bar, ref mask, 0f, RhythmRules.Judge(0f)));
            Assert.IsFalse(RhythmRules.Register(ref bar, ref mask, B, RhythmRules.Judge(B)));
            Assert.IsTrue(RhythmRules.Register(ref bar, ref mask, 3 * B, RhythmRules.Judge(3 * B)));
            Assert.IsFalse(RhythmRules.Register(ref bar, ref mask, 4 * B, RhythmRules.Judge(4 * B)));
        }

        [Test]
        public void AMissOrARestStrikeBreaksTheBar()
        {
            int bar = -1, mask = 0;
            RhythmRules.Register(ref bar, ref mask, 0f, RhythmHit.OnBeat);
            RhythmRules.Register(ref bar, ref mask, 2 * B, RhythmRules.Judge(2 * B));
            Assert.IsFalse(RhythmRules.Register(ref bar, ref mask, B, RhythmHit.OnBeat));
            Assert.IsFalse(RhythmRules.Register(ref bar, ref mask, 3 * B, RhythmHit.OnBeat));
            RhythmRules.Register(ref bar, ref mask, 4 * B, RhythmHit.OnBeat);
            RhythmRules.Register(ref bar, ref mask, 5 * B, RhythmHit.OnBeat);
            Assert.IsTrue(RhythmRules.Register(ref bar, ref mask, 7 * B, RhythmHit.OnBeat), "the next bar starts clean");
        }

        [Test]
        public void TheCueFlashesOnStrikeBeatsAndStaysFaintOnTheRest()
        {
            Assert.AreEqual(1f, RhythmRules.Pulse(0f), 0.001f);
            Assert.AreEqual(1f, RhythmRules.Pulse(3 * B), 0.001f);
            Assert.Less(RhythmRules.Pulse(2 * B), 0.25f);
            Assert.Less(RhythmRules.Pulse(0.9f * B), 0.01f);
        }
    }
}
