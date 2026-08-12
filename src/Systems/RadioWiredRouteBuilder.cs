using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Server;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public static class RadioWiredRouteBuilder
    {
        public static List<VoiceRoute> BuildRoutesForWiredNode(ICoreServerAPI sapi, BEWireNode node)
        {
            var routes = new List<VoiceRoute>();
            if (sapi == null || node == null)
            {
                return routes;
            }

            var emitters = RadioWireNetworkHelper.FindEmitters(node)
                .Where(emitter => emitter.IsWirelessTransmitting)
                .ToList();
            var speakers = RadioWireNetworkHelper.FindSpeakers(node).ToList();
            if (emitters.Count == 0 && speakers.Count == 0)
            {
                return routes;
            }

            var frequencies = emitters
                .Select(emitter => RadioFrequencyUtil.Normalize(emitter.GetConsoleFrequency()))
                .Where(frequency => frequency.Length > 0)
                .Distinct()
                .ToList();

            var transmissionPoints = RadioRfTransmissionService.CollectActiveTransmissionPoints(sapi)
                .Where(point => frequencies.Any(frequency => RadioFrequencyUtil.Matches(frequency, point.Frequency)))
                .ToList();

            routes.AddRange(RadioRfTransmissionService.BuildVoiceRoutesForTransmissionPoints(transmissionPoints));
            RadioRfTransmissionService.AppendReceiverRelayRoutes(sapi, routes, frequencies);

            foreach (var speaker in speakers)
            {
                routes.Add(new VoiceRoute(
                    speaker.Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                    speaker.VoiceEmissionRangeBlocks,
                    node.Pos.dimension));
            }

            return routes;
        }
    }
}
