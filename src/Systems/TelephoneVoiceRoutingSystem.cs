using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Server;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Systems
{
    public class TelephoneVoiceRoutingSystem : ModSystem, IVoiceRouteProvider, IVoiceMultiRouteProvider
    {
        private readonly ConcurrentDictionary<string, IReadOnlyList<VoiceRoute>> routes = new();

        public void SetRoute(string playerUid, Vec3d emissionPos, int rangeBlocks)
        {
            SetRoutes(playerUid, new[] { new VoiceRoute(emissionPos, rangeBlocks) });
        }

        public void SetRoutes(string playerUid, IEnumerable<VoiceRoute> voiceRoutes)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || voiceRoutes == null)
            {
                return;
            }

            var sanitized = voiceRoutes
                .Where(route => route.EmissionPos != null && route.RangeBlocks > 0)
                .ToList();
            if (sanitized.Count == 0)
            {
                return;
            }

            routes[playerUid] = sanitized;
        }

        public void ClearRoute(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            routes.TryRemove(playerUid, out _);
        }

        public bool TryGetRoute(string playerUid, out Vec3d emissionPos, out int rangeBlocks)
        {
            emissionPos = null;
            rangeBlocks = 0;
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return false;
            }

            if (!routes.TryGetValue(playerUid, out var voiceRoutes) || voiceRoutes == null || voiceRoutes.Count == 0)
            {
                return false;
            }

            var route = voiceRoutes[0];
            if (route.EmissionPos == null || route.RangeBlocks <= 0)
            {
                return false;
            }

            emissionPos = route.EmissionPos;
            rangeBlocks = route.RangeBlocks;
            return true;
        }

        public bool TryGetRoutes(string playerUid, out IReadOnlyList<VoiceRoute> voiceRoutes)
        {
            voiceRoutes = null;
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return false;
            }

            if (!routes.TryGetValue(playerUid, out var routesForPlayer) || routesForPlayer == null || routesForPlayer.Count == 0)
            {
                return false;
            }

            voiceRoutes = routesForPlayer;
            return true;
        }
    }
}
