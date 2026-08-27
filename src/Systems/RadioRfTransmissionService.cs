using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public static class RadioRfTransmissionService
    {
        /// <summary>
        /// Active wired-source (non-repeater) TX points from the world-level presence registry.
        /// </summary>
        public static List<RadioTransmissionPoint> CollectWiredTransmissionPoints(ICoreServerAPI sapi)
        {
            var points = new List<RadioTransmissionPoint>();
            if (sapi == null)
            {
                return points;
            }

            foreach (var emitter in RadioRfPresenceRegistry.GetEmitters())
            {
                if (emitter.IsRepeater || !emitter.IsActive || emitter.RangeBlocks <= 0)
                {
                    continue;
                }

                string frequency = RadioFrequencyUtil.Normalize(emitter.Frequency);
                if (frequency.Length == 0)
                {
                    continue;
                }

                points.Add(ToTransmissionPoint(emitter, frequency, isRepeaterRelay: false));
            }

            return points;
        }

        /// <summary>
        /// Wired sources plus repeaters that can hear at least one matching source (chunk-independent).
        /// </summary>
        public static List<RadioTransmissionPoint> CollectActiveTransmissionPoints(ICoreServerAPI sapi)
        {
            var points = CollectWiredTransmissionPoints(sapi);
            if (sapi == null)
            {
                return points;
            }

            foreach (var repeater in RadioRfPresenceRegistry.GetEmitters())
            {
                if (!repeater.IsRepeater || !repeater.IsActive || repeater.RangeBlocks <= 0 || repeater.Pos == null)
                {
                    continue;
                }

                string frequency = RadioFrequencyUtil.Normalize(repeater.Frequency);
                if (frequency.Length == 0)
                {
                    continue;
                }

                Vec3d repeaterPos = repeater.Pos.ToVec3d().Add(0.5, 0.5, 0.5);
                bool canRelay = points.Any(source =>
                    source.Dimension == repeater.Dimension
                    && RadioFrequencyUtil.Matches(source.Frequency, frequency)
                    && repeaterPos.DistanceTo(source.Position) <= source.RangeBlocks);

                if (!canRelay)
                {
                    continue;
                }

                points.Add(ToTransmissionPoint(repeater, frequency, isRepeaterRelay: true));
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

                routes.Add(new VoiceRoute(point.Position, point.RangeBlocks, point.Dimension, point.Frequency, acousticEmission: false));
            }

            return routes;
        }

        public static void AppendReceiverRelayRoutes(ICoreServerAPI sapi, ICollection<VoiceRoute> routes, IEnumerable<string> frequencies)
        {
            if (routes == null)
            {
                return;
            }

            var frequencySet = new HashSet<string>();
            foreach (string frequency in frequencies ?? Enumerable.Empty<string>())
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

            foreach (var receiver in RadioRfPresenceRegistry.GetReceivers())
            {
                if (receiver == null || !receiver.IsEnabled)
                {
                    continue;
                }

                string tuned = RadioFrequencyUtil.Normalize(receiver.TunedFrequency);
                if (tuned.Length == 0 || !frequencySet.Contains(tuned))
                {
                    continue;
                }

                if (receiver.AcousticPoints != null && receiver.AcousticPoints.Count > 0)
                {
                    foreach (var acoustic in receiver.AcousticPoints)
                    {
                        if (acoustic?.Pos == null || acoustic.RangeBlocks <= 0)
                        {
                            continue;
                        }

                        routes.Add(new VoiceRoute(
                            acoustic.Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                            acoustic.RangeBlocks,
                            acoustic.Dimension,
                            tuned,
                            acousticEmission: true));
                    }

                    continue;
                }

                if (receiver.Pos == null || receiver.PlaybackRangeBlocks <= 0)
                {
                    continue;
                }

                routes.Add(new VoiceRoute(
                    receiver.Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                    receiver.PlaybackRangeBlocks,
                    receiver.Dimension,
                    tuned,
                    acousticEmission: true));
            }
        }

        private static RadioTransmissionPoint ToTransmissionPoint(RadioRfEmitterPresence emitter, string frequency, bool isRepeaterRelay)
        {
            return new RadioTransmissionPoint(
                emitter.Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                emitter.RangeBlocks,
                frequency,
                emitter.Dimension,
                isRepeaterRelay);
        }
    }
}
