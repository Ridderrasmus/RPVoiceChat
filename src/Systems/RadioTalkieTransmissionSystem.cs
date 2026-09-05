using System.Collections.Concurrent;
using System.Collections.Generic;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Items;
using RPVoiceChat.Server;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public class RadioTalkieTransmissionSystem : ModSystem
    {
        private readonly ConcurrentDictionary<string, string> activeTalkieFrequencies = new();
        private ICoreServerAPI sapi;
        private RadioVoiceRoutingSystem routing;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            routing = api.ModLoader.GetModSystem<RadioVoiceRoutingSystem>();
            api.Event.RegisterGameTickListener(OnServerTick, 250);
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
        }

        public bool IsTalkieTransmitting(string playerUid)
        {
            return !string.IsNullOrEmpty(playerUid) && activeTalkieFrequencies.ContainsKey(playerUid);
        }

        public void SetTalkieTransmitting(IServerPlayer player, bool transmitting, string frequency)
        {
            if (player == null)
            {
                return;
            }

            if (transmitting && !string.IsNullOrWhiteSpace(frequency))
            {
                string normalized = RadioFrequencyUtil.Normalize(frequency);
                activeTalkieFrequencies[player.PlayerUID] = normalized;
                ApplyTalkieRoute(player, normalized);
            }
            else
            {
                activeTalkieFrequencies.TryRemove(player.PlayerUID, out _);
                routing?.ClearTalkieRoute(player.PlayerUID);
            }
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            activeTalkieFrequencies.TryRemove(player.PlayerUID, out _);
            routing?.ClearRoute(player.PlayerUID);
        }

        private void OnServerTick(float dt)
        {
            if (sapi == null || routing == null)
            {
                return;
            }

            foreach (var entry in activeTalkieFrequencies)
            {
                IServerPlayer player = sapi.World.PlayerByUid(entry.Key) as IServerPlayer;
                if (player?.Entity?.Pos == null || !ItemRadio.IsTalkieActiveInHands(player))
                {
                    activeTalkieFrequencies.TryRemove(entry.Key, out _);
                    routing.ClearTalkieRoute(entry.Key);
                    continue;
                }

                ApplyTalkieRoute(player, entry.Value);
            }
        }

        private void ApplyTalkieRoute(IServerPlayer player, string frequency)
        {
            if (routing == null || player?.Entity?.Pos == null || string.IsNullOrEmpty(frequency))
            {
                return;
            }

            Vec3d emissionPos = player.Entity.Pos.XYZ;
            routing.SetTalkieRoutes(player.PlayerUID, new[]
            {
                new VoiceRoute(
                    emissionPos,
                    ServerConfigManager.RadioTalkieRangeBlocks,
                    player.Entity.Pos.Dimension,
                    frequency,
                    acousticEmission: true)
            });
        }
    }
}
