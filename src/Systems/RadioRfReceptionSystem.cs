using System;
using System.Collections.Generic;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Items;
using RPVoiceChat.Networking;
using RPVoiceChat.Server;
using RPVoiceChat.Util;
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
            if (sapi?.World == null || routes == null || routes.Count == 0 || recipients == null)
            {
                return;
            }

            int talkieRange = ServerConfigManager.RadioTalkieRangeBlocks;

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player?.Entity?.Pos == null)
                {
                    continue;
                }

                if (player.PlayerUID == packet.PlayerId)
                {
                    continue;
                }

                try
                {
                    EntityAgent entity = player.Entity;
                    if (entity?.Pos == null)
                    {
                        continue;
                    }

                    Vec3d listenerPos = entity.Pos.XYZ;
                    int dimension = entity.Pos.Dimension;

                    foreach (string tunedFrequency in ItemRadio.GetActiveListenFrequencies(player))
                    {
                        if (string.IsNullOrEmpty(tunedFrequency))
                        {
                            continue;
                        }

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

                            // RF coverage uses the transmitter range; audio plays at the handheld, not the antenna.
                            double distanceSq = SquareDistance(listenerPos, route.EmissionPos);
                            double rfRangeSq = (double)route.RangeBlocks * route.RangeBlocks;
                            if (distanceSq > rfRangeSq)
                            {
                                continue;
                            }

                            var acousticAtPlayer = new VoiceRoute(
                                listenerPos,
                                talkieRange,
                                dimension,
                                route.RadioFrequency,
                                acousticEmission: true);
                            TrySetRecipient(player.PlayerUID, acousticAtPlayer, distanceSq, recipients);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.server?.Warning($"[RadioRfReception] Skipped talkie routing for {player.PlayerUID}: {ex.Message}");
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
