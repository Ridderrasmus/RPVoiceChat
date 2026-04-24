using System.Collections.Concurrent;
using RPVoiceChat.Server;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Systems
{
    public class TelephoneVoiceRoutingSystem : ModSystem, IVoiceRouteProvider
    {
        private readonly ConcurrentDictionary<string, (Vec3d EmissionPos, int RangeBlocks)> routes = new();

        public void SetRoute(string playerUid, Vec3d emissionPos, int rangeBlocks)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || emissionPos == null || rangeBlocks <= 0)
            {
                return;
            }

            routes[playerUid] = (emissionPos, rangeBlocks);
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

            if (!routes.TryGetValue(playerUid, out var route) || route.EmissionPos == null || route.RangeBlocks <= 0)
            {
                return false;
            }

            emissionPos = route.EmissionPos;
            rangeBlocks = route.RangeBlocks;
            return true;
        }
    }
}
