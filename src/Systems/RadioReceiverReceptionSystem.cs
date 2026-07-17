using System.Collections.Generic;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Networking;
using RPVoiceChat.Server;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public class RadioReceiverReceptionSystem : ModSystem, IVoiceRecipientExpander
    {
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
        }

        public void ExpandRoutedRecipients(
            AudioPacket packet,
            IReadOnlyList<VoiceRoute> routes,
            Dictionary<string, RoutedVoiceRecipient> recipients)
        {
            if (sapi == null || routes == null || routes.Count == 0)
            {
                return;
            }

            int receiverRange = ServerConfigManager.RadioReceiverRangeBlocks;
            double receiverRangeSq = (double)receiverRange * receiverRange;

            foreach (BlockEntityRadioReceiver receiver in RadioBlockIndex.GetLoadedReceivers(sapi.World))
            {
                string tunedFrequency = RadioFrequencyUtil.Normalize(receiver.TunedFrequency);
                if (tunedFrequency.Length == 0 || !RoutesContainFrequency(routes, tunedFrequency))
                {
                    continue;
                }

                Vec3d receiverPos = receiver.Pos.ToVec3d().Add(0.5, 0.5, 0.5);
                var listenRoute = new VoiceRoute(
                    receiverPos,
                    receiverRange,
                    receiver.Pos.dimension,
                    tunedFrequency);

                foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
                {
                    if (player?.Entity?.Pos == null || player.Entity.Pos.Dimension != receiver.Pos.dimension)
                    {
                        continue;
                    }

                    double distanceSq = SquareDistance(player.Entity.Pos.XYZ, receiverPos);
                    if (distanceSq > receiverRangeSq)
                    {
                        continue;
                    }

                    TrySetRecipient(player.PlayerUID, listenRoute, distanceSq, recipients);
                }
            }
        }

        private static bool RoutesContainFrequency(IReadOnlyList<VoiceRoute> routes, string tunedFrequency)
        {
            for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                VoiceRoute route = routes[routeIndex];
                if (!string.IsNullOrEmpty(route.RadioFrequency)
                    && RadioFrequencyUtil.Matches(route.RadioFrequency, tunedFrequency))
                {
                    return true;
                }
            }

            return false;
        }

        private static void TrySetRecipient(
            string playerUid,
            VoiceRoute route,
            double distanceSq,
            Dictionary<string, RoutedVoiceRecipient> recipients)
        {
            if (recipients.TryGetValue(playerUid, out RoutedVoiceRecipient existing) && existing.DistanceSq <= distanceSq)
            {
                return;
            }

            recipients[playerUid] = new RoutedVoiceRecipient(playerUid, route, distanceSq);
        }

        private static double SquareDistance(Vec3d listener, Vec3d source)
        {
            double dx = listener.X - source.X;
            double dy = listener.Y - source.Y;
            double dz = listener.Z - source.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
