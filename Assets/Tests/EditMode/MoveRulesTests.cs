using Chopper.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class MoveRulesTests
    {
        [Test]
        public void ClimbsAboutOneBlockOnLandAndMoreOutOfWater()
        {
            Assert.IsTrue(MoveRules.Climbable(0.5f, MoveRules.MaxAutoClimb(false)));
            Assert.IsFalse(MoveRules.Climbable(1.3f, MoveRules.MaxAutoClimb(false)));
            Assert.IsTrue(MoveRules.Climbable(1.5f, MoveRules.MaxAutoClimb(true)));
            Assert.IsFalse(MoveRules.Climbable(0f, MoveRules.MaxAutoClimb(true)));
        }

        [Test]
        public void FindsTheTopOfABoxLedgeAhead()
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = new Vector3(0f, 0f, 1.0f);
            Physics.SyncTransforms();
            bool found = MoveRules.FindLedge(Vector3.zero, Vector3.forward, 0.35f, 1.8f, MoveRules.LandClimb, null, out Vector3 top);
            Object.DestroyImmediate(box);
            Assert.IsTrue(found);
            Assert.AreEqual(0.53f, top.y, 0.05f);
        }
    }
}