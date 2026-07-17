using System.Collections.Generic;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Items;
using RPVoiceChat.Networking;
using RPVoiceChat.Server;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public class RadioRfReceptionSystem : ModSystem, IVoiceRecipientExpander
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

            int talkieRange = ServerConfigManager.RadioTalkieRangeBlocks;
            double talkieRangeSq = (double)talkieRange * talkieRange;

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player?.Entity?.Pos == null)
                {
                    continue;
                }

                string tunedFrequency = ItemRadio.GetTunedFrequency(player);
                if (string.IsNullOrEmpty(tunedFrequency))
                {
                    continue;
                }

                Vec3d listenerPos = player.Entity.Pos.XYZ;
                int dimension = player.Entity.Pos.Dimension;

                for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
                {
                    VoiceRoute route = routes[routeIndex];
                    if (route.EmissionPos == null || route.RangeBlocks <= 0)
                    {
                        continue;
                    }

                    if (route.Dimension != dimension)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(route.RadioFrequency))
                    {
                        continue;
                    }

                    if (!RadioFrequencyUtil.Matches(route.RadioFrequency, tunedFrequency))
                    {
                        continue;
                    }

                    double distanceSq = SquareDistance(listenerPos, route.EmissionPos);
                    if (distanceSq > talkieRangeSq)
                    {
                        continue;
                    }

                    TrySetRecipient(player.PlayerUID, route, distanceSq, recipients);
                }
            }
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
