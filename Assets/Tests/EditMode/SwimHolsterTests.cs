using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class SwimHolsterTests
    {
        [Test]
        public void EnteringWaterPutsTheToolAwayOnce()
        {
            bool remembered = false;
            Assert.IsTrue(SwimHolster.Step(true, true, ref remembered));
            Assert.IsTrue(remembered);
            Assert.IsFalse(SwimHolster.Step(true, true, ref remembered));
            Assert.IsFalse(SwimHolster.Step(true, false, ref remembered));
        }

        [Test]
        public void LeavingWaterBringsItBack()
        {
            bool remembered = false;
            SwimHolster.Step(true, true, ref remembered);
            Assert.IsTrue(SwimHolster.Step(false, false, ref remembered));
            Assert.IsFalse(remembered);
            Assert.IsFalse(SwimHolster.Step(false, false, ref remembered));
        }

        [Test]
        public void NothingIsDrawnWhileSwimming()
        {
            Assert.IsFalse(SwimHolster.MayDraw(true));
            Assert.IsTrue(SwimHolster.MayDraw(false));
        }

        [Test]
        public void EmptyHandsStayEmpty()
        {
            bool remembered = false;
            Assert.IsFalse(SwimHolster.Step(true, false, ref remembered));
            Assert.IsFalse(SwimHolster.Step(false, false, ref remembered));
            Assert.IsFalse(SwimHolster.Step(false, true, ref remembered));
        }
    }
}
