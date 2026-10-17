using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class SteerAndToolTests
    {
        private static Vector3 Run(Vector3 v, Vector3 target, float seconds, out Vector3 travelled)
        {
            travelled = Vector3.zero;
            float dt = 1f / 60f;
            for (float t = 0f; t < seconds; t += dt)
            {
                v = SteerRules.Step(v, target, SteerRules.Acceleration, SteerRules.Deceleration, dt);
                travelled += v * dt;
            }
            return v;
        }

        [Test]
        public void SprintStopsWithinAThirdOfAMetre()
        {
            Vector3 v = Run(Vector3.forward * 9.5f, Vector3.zero, 1f, out Vector3 slide);
            Assert.Less(slide.magnitude, 0.35f);
            Assert.AreEqual(0f, v.magnitude, 1e-4f);
            Assert.Less(SteerRules.StopDistance(9.5f, SteerRules.Deceleration), 0.35f);
        }

        [Test]
        public void SharpTurnKeepsNoSidewaysDrift()
        {
            Run(Vector3.forward * 9.5f, Vector3.right * 9.5f, 0.6f, out Vector3 moved);
            Assert.Less(Mathf.Abs(moved.z), 0.01f);
            Assert.Greater(moved.x, 4f);
        }

        [Test]
        public void ReachesSprintQuickly()
        {
            Vector3 v = Run(Vector3.zero, Vector3.forward * 9.5f, 0.2f, out _);
            Assert.AreEqual(9.5f, v.magnitude, 0.01f);
        }

        [Test]
        public void ReversingDoesNotCarryMomentum()
        {
            Vector3 v = SteerRules.Step(Vector3.forward * 9.5f, Vector3.back * 6f, SteerRules.Acceleration, SteerRules.Deceleration, 1f / 60f);
            Assert.LessOrEqual(Vector3.Dot(v, Vector3.forward), 0f);
        }

        [Test]
        public void EachToolHasItsOwnMotion()
        {
            ToolPoseKey axe = ToolMotion.Sample(SwingTool.Axe, 0, 0.38f);
            ToolPoseKey axeB = ToolMotion.Sample(SwingTool.Axe, 1, 0.38f);
            ToolPoseKey pick = ToolMotion.Sample(SwingTool.Pick, 0, 0.38f);
            ToolPoseKey hammer = ToolMotion.Sample(SwingTool.Hammer, 0, 0.3f);
            Assert.Greater(Vector3.Angle(axe.RightUpper, axeB.RightUpper), 20f);
            Assert.Greater(Vector3.Angle(axe.LeftUpper, pick.LeftUpper), 30f);
            Assert.Greater(Vector3.Angle(pick.RightLower, hammer.RightLower), 20f);
            Assert.IsTrue(ToolMotion.TwoHanded(SwingTool.Axe));
            Assert.IsFalse(ToolMotion.TwoHanded(SwingTool.Pick));
        }

        [Test]
        public void AxeWindsUpOverheadThenMeetsTheTrunkInFront()
        {
            ToolPoseKey windUp = ToolMotion.Sample(SwingTool.Axe, 0, 0.38f);
            ToolPoseKey contact = ToolMotion.Sample(SwingTool.Axe, 0, ToolMotion.Contact(SwingTool.Axe));
            Assert.Greater(windUp.RightUpper.y, 0.6f);
            Assert.Greater(contact.RightUpper.z, 0.9f);
            Assert.Less(contact.RightLower.y, 0f);
        }

        [Test]
        public void EachWeaponHasItsOwnMoveSet()
        {
            SwingTool[] weapons = { SwingTool.AxeFight, SwingTool.Sword, SwingTool.Mace, SwingTool.Spear };
            for (int i = 0; i < weapons.Length; i++)
                for (int j = i + 1; j < weapons.Length; j++)
                {
                    ToolPoseKey a = ToolMotion.Sample(weapons[i], 0, 0.35f);
                    ToolPoseKey b = ToolMotion.Sample(weapons[j], 0, 0.35f);
                    Assert.Greater(Vector3.Angle(a.RightUpper, b.RightUpper) + Vector3.Angle(a.RightLower, b.RightLower), 15f, weapons[i] + " vs " + weapons[j]);
                }
            Assert.Greater(ToolMotion.Sample(SwingTool.Mace, 0, 0.45f).RightUpper.y, 0.8f);
            Assert.Greater(ToolMotion.Sample(SwingTool.Spear, 0, ToolMotion.Contact(SwingTool.Spear)).RightLower.z, 0.95f);
            Assert.Greater(Mathf.Abs(ToolMotion.Sample(SwingTool.Sword, 0, 0.35f).Twist), 30f);
            Assert.Less(ToolMotion.Contact(SwingTool.AxeFight), ToolMotion.Contact(SwingTool.Axe));
            Assert.Greater(ToolMotion.Contact(SwingTool.Mace), ToolMotion.Contact(SwingTool.Axe));
        }

        [Test]
        public void PoseFadesInAndOut()
        {
            Assert.AreEqual(0f, ToolMotion.Weight(0f));
            Assert.AreEqual(1f, ToolMotion.Weight(0.5f));
            Assert.AreEqual(0f, ToolMotion.Weight(1f));
        }

        [Test]
        public void SnapsToChopReachFromTheTrunk()
        {
            Vector3 spot = ToolMotion.SnapPoint(new Vector3(3f, 1f, 0f), Vector3.zero, ToolMotion.ChopReach);
            Assert.AreEqual(ToolMotion.ChopReach, new Vector2(spot.x, spot.z).magnitude, 1e-4f);
            Assert.AreEqual(1f, spot.y);
        }
    }
}
