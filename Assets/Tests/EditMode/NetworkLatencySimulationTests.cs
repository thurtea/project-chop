using Chopper.Networking;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class NetworkLatencySimulationTests
    {
        [Test]
        public void ReadsLatencyJitterAndPacketLoss()
        {
            bool valid = NetworkLatencySimulation.TryReadSettings(
                new[] { "-chopperLatency", "80", "-chopperJitter", "15", "-chopperPacketLoss", "2.5" },
                out NetworkLatencySettings settings, out string error);

            Assert.IsTrue(valid, error);
            Assert.IsTrue(settings.Enabled);
            Assert.AreEqual(80, settings.LatencyMilliseconds);
            Assert.AreEqual(15, settings.JitterMilliseconds);
            Assert.AreEqual(0.025d, settings.PacketLoss, 0.000001d);
        }

        [Test]
        public void RequiresLatencyWhenOtherSimulationSettingsArePresent()
        {
            bool valid = NetworkLatencySimulation.TryReadSettings(
                new[] { "-chopperPacketLoss", "1" },
                out _, out string error);

            Assert.IsFalse(valid);
            StringAssert.Contains("-chopperLatency", error);
        }

        [Test]
        public void RejectsInvalidPacketLoss()
        {
            bool valid = NetworkLatencySimulation.TryReadSettings(
                new[] { "-chopperLatency", "80", "-chopperPacketLoss", "101" },
                out _, out string error);

            Assert.IsFalse(valid);
            StringAssert.Contains("-chopperPacketLoss", error);
        }
    }
}
