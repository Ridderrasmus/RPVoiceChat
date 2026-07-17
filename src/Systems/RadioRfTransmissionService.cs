using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Server;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public static class RadioRfTransmissionService
    {
        public static List<RadioTransmissionPoint> CollectWiredTransmissionPoints(ICoreServerAPI sapi)
        {
            var points = new List<RadioTransmissionPoint>();
            if (sapi?.World?.BlockAccessor == null)
            {
                return points;
            }

            foreach (var emitter in RadioBlockIndex.GetLoadedEmitters(sapi.World))
            {
                if (emitter.IsRepeaterMode || !emitter.IsWirelessTransmitting)
                {
                    continue;
                }

                string frequency = RadioFrequencyUtil.Normalize(emitter.GetConsoleFrequency());
                if (frequency.Length == 0)
                {
                    continue;
                }

                points.Add(new RadioTransmissionPoint(
                    emitter.Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                    emitter.GetEffectiveTransmitRangeBlocks(),
                    frequency,
                    emitter.Pos.dimension,
                    false));
            }

            return points;
        }

        public static List<RadioTransmissionPoint> CollectActiveTransmissionPoints(ICoreServerAPI sapi)
        {
            var points = CollectWiredTransmissionPoints(sapi);
            if (sapi?.World?.BlockAccessor == null)
            {
                return points;
            }

            foreach (var repeater in RadioBlockIndex.GetLoadedEmitters(sapi.World))
            {
                if (!repeater.IsRepeaterMode || !repeater.HasSufficientTransmitPower())
                {
                    continue;
                }

                string frequency = RadioFrequencyUtil.Normalize(repeater.RepeaterFrequency);
                if (frequency.Length == 0)
                {
                    continue;
                }

                Vec3d repeaterPos = repeater.Pos.ToVec3d().Add(0.5, 0.5, 0.5);
                int repeaterRange = repeater.GetEffectiveTransmitRangeBlocks();
                bool canRelay = points.Any(source =>
                    source.Dimension == repeater.Pos.dimension
                    && RadioFrequencyUtil.Matches(source.Frequency, frequency)
                    && repeaterPos.DistanceTo(source.Position) <= source.RangeBlocks);

                if (!canRelay)
                {
                    continue;
                }

                points.Add(new RadioTransmissionPoint(
                    repeaterPos,
                    repeaterRange,
                    frequency,
                    repeater.Pos.dimension,
                    true));
            }

            return points;
        }

        public static List<VoiceRoute> BuildVoiceRoutesForTransmissionPoints(IEnumerable<RadioTransmissionPoint> points)
        {
            var routes = new List<VoiceRoute>();
            foreach (var point in points)
            {
                if (point.Position == null || point.RangeBlocks <= 0 || string.IsNullOrEmpty(point.Frequency))
                {
                    continue;
                }

                routes.Add(new VoiceRoute(point.Position, point.RangeBlocks, point.Dimension, point.Frequency));
            }

            return routes;
        }

        public static void AppendReceiverRelayRoutes(ICoreServerAPI sapi, ICollection<VoiceRoute> routes, IEnumerable<string> frequencies)
        {
            if (sapi?.World?.BlockAccessor == null || routes == null)
            {
                return;
            }

            var frequencySet = new HashSet<string>();
            foreach (string frequency in frequencies)
            {
                string normalized = RadioFrequencyUtil.Normalize(frequency);
                if (normalized.Length > 0)
                {
                    frequencySet.Add(normalized);
                }
            }

            if (frequencySet.Count == 0)
            {
                return;
            }

            foreach (var receiver in RadioBlockIndex.GetLoadedReceivers(sapi.World))
            {
                if (receiver == null)
                {
                    continue;
                }

                string tuned = RadioFrequencyUtil.Normalize(receiver.TunedFrequency);
                if (tuned.Length == 0 || !frequencySet.Contains(tuned))
                {
                    continue;
                }

                routes.Add(new VoiceRoute(
                    receiver.Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                    ServerConfigManager.RadioReceiverRangeBlocks,
                    receiver.Pos.dimension,
                    tuned));
            }
        }
    }
}
