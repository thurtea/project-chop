using System;
using System.Globalization;
using FishNet.Managing.Transporting;
using UnityEngine;

namespace Chopper.Networking
{
    // Development-only command-line network conditions for repeatable local co-op runs.
    public class NetworkLatencySimulation : MonoBehaviour
    {
        private const string LatencyArg = "-chopperLatency";
        private const string JitterArg = "-chopperJitter";
        private const string LossArg = "-chopperPacketLoss";
        private const float JitterResampleSeconds = 0.1f;
        private const long MaximumLatencyMilliseconds = 60000;

        private LatencySimulator _simulator;
        private long _latencyMilliseconds;
        private long _jitterMilliseconds;
        private float _nextJitterSample;

        public static bool TryReadSettings(string[] args, out NetworkLatencySettings settings, out string error)
        {
            settings = default;
            error = null;
            bool supplied = false;

            if (!TryReadNonNegativeLong(args, LatencyArg, out long latency, out bool hasLatency, out error)) return false;
            if (!TryReadNonNegativeLong(args, JitterArg, out long jitter, out bool hasJitter, out error)) return false;
            if (!TryReadPercentage(args, LossArg, out double packetLoss, out bool hasLoss, out error)) return false;

            supplied = hasLatency || hasJitter || hasLoss;
            if (!supplied) return true;
            if (!hasLatency)
            {
                error = $"{LatencyArg} is required when simulating network conditions.";
                return false;
            }

            settings = new NetworkLatencySettings(latency, jitter, packetLoss, true);
            return true;
        }

        public void Configure(TransportManager transportManager)
        {
            if (!TryReadSettings(Environment.GetCommandLineArgs(), out NetworkLatencySettings settings, out string error))
            {
                Debug.LogError($"[NetworkSimulation] {error}");
                return;
            }
            if (!settings.Enabled || transportManager == null) return;

            _simulator = transportManager.LatencySimulator;
            _latencyMilliseconds = settings.LatencyMilliseconds;
            _jitterMilliseconds = settings.JitterMilliseconds;
            _simulator.SetPacketLoss(settings.PacketLoss);
            _simulator.SetOutOfOrder(0d);
            _simulator.SetLatency(_latencyMilliseconds);
            _simulator.SetEnabled(true);
            _nextJitterSample = 0f;
            Debug.Log($"[NetworkSimulation] latency {_latencyMilliseconds} ms, jitter +/-{_jitterMilliseconds} ms, packet loss {settings.PacketLoss * 100d:F1}%.");
        }

        private void Update()
        {
            if (_simulator == null || _jitterMilliseconds == 0 || Time.unscaledTime < _nextJitterSample) return;

            long minimum = Math.Max(0, _latencyMilliseconds - _jitterMilliseconds);
            long maximum = _latencyMilliseconds + _jitterMilliseconds;
            _simulator.SetLatency(UnityEngine.Random.Range((int)minimum, (int)maximum + 1));
            _nextJitterSample = Time.unscaledTime + JitterResampleSeconds;
        }

        private static bool TryReadNonNegativeLong(string[] args, string name, out long value, out bool supplied, out string error)
        {
            value = 0;
            supplied = false;
            error = null;
            int index = Array.IndexOf(args, name);
            if (index < 0) return true;

            supplied = true;
            if (index + 1 >= args.Length || !long.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0 || value > MaximumLatencyMilliseconds)
            {
                error = $"{name} needs a whole number of milliseconds from 0 to {MaximumLatencyMilliseconds}.";
                return false;
            }
            return true;
        }

        private static bool TryReadPercentage(string[] args, string name, out double value, out bool supplied, out string error)
        {
            value = 0d;
            supplied = false;
            error = null;
            int index = Array.IndexOf(args, name);
            if (index < 0) return true;

            supplied = true;
            if (index + 1 >= args.Length || !double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0d || value > 100d)
            {
                error = $"{name} needs a percentage from 0 to 100.";
                return false;
            }
            value /= 100d;
            return true;
        }
    }

    public readonly struct NetworkLatencySettings
    {
        public readonly long LatencyMilliseconds;
        public readonly long JitterMilliseconds;
        public readonly double PacketLoss;
        public readonly bool Enabled;

        public NetworkLatencySettings(long latencyMilliseconds, long jitterMilliseconds, double packetLoss, bool enabled)
        {
            LatencyMilliseconds = latencyMilliseconds;
            JitterMilliseconds = jitterMilliseconds;
            PacketLoss = packetLoss;
            Enabled = enabled;
        }
    }
}
