using Chopper.Trees;
using NUnit.Framework;
using UnityEngine;

namespace Chopper.Tests.EditMode
{
    public class TreeFallTests
    {
        [Test]
        public void OnLevelGroundTreesFallAwayFromTheChopper()
        {
            var random = new System.Random(5);
            Vector3 away = Vector3.forward;
            for (int i = 0; i < 2000; i++)
            {
                Vector3 fall = TreeFallRules.Choose(Vector3.zero, away, random);
                Assert.Greater(Vector3.Dot(fall, away), Mathf.Cos(26f * Mathf.Deg2Rad), "a level tree strayed too far from the push");
                Assert.AreEqual(0f, fall.y);
                Assert.AreEqual(1f, fall.magnitude, 1e-4f);
            }
        }

        [Test]
        public void ChoppedFromBelowOnAHillsideATreeComesBackDownOnTheChopper()
        {
            var random = new System.Random(11);
            // The ground falls away to +x; the chopper stands downhill and swings uphill (-x).
            Vector3 downhill = new Vector3(0.5f, 0f, 0f);
            Vector3 away = Vector3.left;
            int down = 0;
            const int trees = 1000;
            for (int i = 0; i < trees; i++) if (TreeFallRules.Choose(downhill, away, random).x > 0f) down++;
            Assert.Greater(down / (float)trees, 0.95f);
        }

        [Test]
        public void AGentleSlopeOnlyBendsTheFall()
        {
            var random = new System.Random(3);
            Vector3 downhill = new Vector3(0.08f, 0f, 0f);
            float sideways = 0f;
            const int trees = 1000;
            for (int i = 0; i < trees; i++)
            {
                Vector3 fall = TreeFallRules.Choose(downhill, Vector3.forward, random);
                Assert.Greater(fall.z, 0f, "a gentle slope should not turn a tree back on the chopper");
                sideways += fall.x;
            }
            Assert.Greater(sideways / trees, 0.1f, "the slope should still pull it downhill a little");
        }

        [Test]
        public void FirstLargeTreeFallsStraightTowardTheFeller()
        {
            Vector3 fall = TreeFallRules.TowardPlayer(Vector3.zero, new Vector3(-3f, 2f, 4f), Vector3.forward);
            Assert.AreEqual(new Vector3(-3f, 0f, 4f).normalized, fall);
        }
    }
}
