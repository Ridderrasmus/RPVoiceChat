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
            if (sapi == null || node == null)
            {
                return new List<VoiceRoute>();
            }

            var speakers = RadioWireNetworkHelper.FindSpeakers(node).ToList();
            return BuildRoutesForNetwork(sapi, node.NetworkUID, speakers, node.Pos.dimension);
        }

        /// <summary>
        /// RF (+ optional loaded speakers) for a wired network without requiring the program source BE to be loaded.
        /// </summary>
        public static List<VoiceRoute> BuildRoutesForNetwork(
            ICoreServerAPI sapi,
            long networkId,
            IEnumerable<BlockEntitySpeaker> speakers = null,
            int speakerDimension = 0)
        {
            var routes = new List<VoiceRoute>();
            if (sapi == null || networkId == 0)
            {
                return routes;
            }

            var speakerList = speakers?.Where(s => s != null).ToList() ?? new List<BlockEntitySpeaker>();
            var frequencies = RadioRfPresenceRegistry.GetActiveFrequenciesForNetwork(networkId).ToList();

            // Merge currently loaded transmitting emitters (fresh console frequency).
            foreach (var emitter in RadioBlockIndex.GetLoadedEmitters(sapi.World)
                .Where(e => e != null && e.NetworkUID == networkId && e.IsWirelessTransmitting))
            {
                string frequency = RadioFrequencyUtil.Normalize(emitter.GetConsoleFrequency());
                if (frequency.Length > 0 && !frequencies.Any(existing => RadioFrequencyUtil.Matches(existing, frequency)))
                {
                    frequencies.Add(frequency);
                }
            }

            if (frequencies.Count == 0 && speakerList.Count == 0)
            {
                return routes;
            }

            if (frequencies.Count > 0)
            {
                var transmissionPoints = RadioRfTransmissionService.CollectActiveTransmissionPoints(sapi)
                    .Where(point => frequencies.Any(frequency => RadioFrequencyUtil.Matches(frequency, point.Frequency)))
                    .ToList();

                routes.AddRange(RadioRfTransmissionService.BuildVoiceRoutesForTransmissionPoints(transmissionPoints));
                RadioRfTransmissionService.AppendReceiverRelayRoutes(sapi, routes, frequencies);
            }

            foreach (var speaker in speakerList)
            {
                routes.Add(new VoiceRoute(
                    speaker.Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                    speaker.VoiceEmissionRangeBlocks,
                    speaker.Pos?.dimension ?? speakerDimension));
            }

            return routes;
        }
    }
}
