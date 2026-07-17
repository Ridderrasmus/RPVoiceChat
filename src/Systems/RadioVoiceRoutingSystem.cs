using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Server;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Systems
{
    public class RadioVoiceRoutingSystem : ModSystem, IVoiceRouteProvider, IVoiceMultiRouteProvider
    {
        private readonly ConcurrentDictionary<string, IReadOnlyList<VoiceRoute>> micRoutes = new();
        private readonly ConcurrentDictionary<string, IReadOnlyList<VoiceRoute>> programRoutes = new();
        private readonly ConcurrentDictionary<string, IReadOnlyList<VoiceRoute>> talkieRoutes = new();

        public void SetMicRoutes(string playerUid, IEnumerable<VoiceRoute> voiceRoutes)
        {
            SetLayerRoutes(micRoutes, playerUid, voiceRoutes);
        }

        public void SetProgramRoutes(string playerUid, IEnumerable<VoiceRoute> voiceRoutes)
        {
            SetLayerRoutes(programRoutes, playerUid, voiceRoutes);
        }

        public void SetTalkieRoutes(string playerUid, IEnumerable<VoiceRoute> voiceRoutes)
        {
            SetLayerRoutes(talkieRoutes, playerUid, voiceRoutes);
        }

        public void ClearMicRoute(string playerUid)
        {
            ClearLayerRoute(micRoutes, playerUid);
        }

        public void ClearProgramRoute(string playerUid)
        {
            ClearLayerRoute(programRoutes, playerUid);
        }

        public void ClearTalkieRoute(string playerUid)
        {
            ClearLayerRoute(talkieRoutes, playerUid);
        }

        public void ClearRoute(string playerUid)
        {
            ClearMicRoute(playerUid);
            ClearProgramRoute(playerUid);
            ClearTalkieRoute(playerUid);
        }

        public bool TryGetRoute(string playerUid, out Vec3d emissionPos, out int rangeBlocks)
        {
            emissionPos = null;
            rangeBlocks = 0;
            if (!TryGetRoutes(playerUid, out var voiceRoutes) || voiceRoutes == null || voiceRoutes.Count == 0)
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

            if (talkieRoutes.TryGetValue(playerUid, out voiceRoutes) && voiceRoutes != null && voiceRoutes.Count > 0)
            {
                return true;
            }

            if (programRoutes.TryGetValue(playerUid, out voiceRoutes) && voiceRoutes != null && voiceRoutes.Count > 0)
            {
                return true;
            }

            return micRoutes.TryGetValue(playerUid, out voiceRoutes) && voiceRoutes != null && voiceRoutes.Count > 0;
        }

        private static void SetLayerRoutes(
            ConcurrentDictionary<string, IReadOnlyList<VoiceRoute>> layer,
            string playerUid,
            IEnumerable<VoiceRoute> voiceRoutes)
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
                layer.TryRemove(playerUid, out _);
                return;
            }

            layer[playerUid] = sanitized;
        }

        private static void ClearLayerRoute(ConcurrentDictionary<string, IReadOnlyList<VoiceRoute>> layer, string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            layer.TryRemove(playerUid, out _);
        }
    }
}
