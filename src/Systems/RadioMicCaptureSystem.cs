using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public class RadioMicCaptureSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private RadioVoiceRoutingSystem routing;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            routing = api.ModLoader.GetModSystem<RadioVoiceRoutingSystem>();
            api.Event.RegisterGameTickListener(OnServerTick, 500);
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            if (player == null || sapi == null)
            {
                return;
            }

            foreach (BlockEntityRadioMicrophone microphone in RadioBlockIndex.GetLoadedMicrophones(sapi.World))
            {
                if (microphone.ActiveOperatorPlayerUid == player.PlayerUID)
                {
                    microphone.ClearTransmission();
                }
            }
        }

        private void OnServerTick(float dt)
        {
            if (sapi == null || routing == null)
            {
                return;
            }

            var routesByPlayer = new Dictionary<string, List<VoiceRoute>>();
            var microphones = RadioBlockIndex.GetLoadedMicrophones(sapi.World).ToList();

            foreach (var microphone in microphones)
            {
                if (microphone.NetworkUID == 0)
                {
                    continue;
                }

                if (!microphone.IsTransmitting || string.IsNullOrWhiteSpace(microphone.ActiveOperatorPlayerUid))
                {
                    continue;
                }

                if (RadioWireNetworkHelper.HasOnAirMixingConsole(microphone))
                {
                    continue;
                }

                if (sapi.World.PlayerByUid(microphone.ActiveOperatorPlayerUid) is not IServerPlayer operatorPlayer)
                {
                    microphone.ClearTransmission();
                    continue;
                }

                var routes = RadioWiredRouteBuilder.BuildRoutesForWiredNode(sapi, microphone);
                if (routes.Count == 0)
                {
                    continue;
                }

                routesByPlayer[operatorPlayer.PlayerUID] = routes;
            }

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player == null)
                {
                    continue;
                }

                if (routesByPlayer.TryGetValue(player.PlayerUID, out var routes) && routes.Count > 0)
                {
                    routing.SetMicRoutes(player.PlayerUID, routes);
                }
                else
                {
                    routing.ClearMicRoute(player.PlayerUID);
                }
            }
        }
    }
}
