using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace rpvoicechat.Networking
{
    public class RPVoiceChatNativeNetworkServer : RPVoiceChatNativeNetwork
    {
        public event Action<IServerPlayer, AudioPacket> OnReceivedPacket;
        private ICoreServerAPI api;
        private IServerNetworkChannel channel;
        private static Dictionary<VoiceLevel, string> configKeyByVoiceLevel = new Dictionary<VoiceLevel, string> 
        {
            { VoiceLevel.Whispering, "rpvoicechat:distance-whisper" },
            { VoiceLevel.Talking, "rpvoicechat:distance-talk" },
            { VoiceLevel.Shouting, "rpvoicechat:distance-shout" },
        };
        private string defaultKey = configKeyByVoiceLevel[VoiceLevel.Talking];

        public RPVoiceChatNativeNetworkServer(ICoreServerAPI api) : base(api)
        {
            this.api = api;
            channel = api.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(ReceivedAudioPacketFromClient);
        }

        private void ReceivedAudioPacketFromClient(IServerPlayer player, AudioPacket packet)
        {
            OnReceivedPacket?.Invoke(player, packet);
            SendAudioToAllClientsInRange(player, packet);
        }

        public void SendAudioToAllClientsInRange(IServerPlayer player, AudioPacket packet)
        {
            configKeyByVoiceLevel.TryGetValue(packet.VoiceLevel, out string key);

            int distance = api.World.Config.GetInt(key ?? defaultKey);
            int squareDistance = distance * distance;

            foreach (var closePlayer in api.World.AllOnlinePlayers)
            {
                if (closePlayer == player ||
                    closePlayer.Entity == null ||
                    player.Entity.Pos.SquareDistanceTo(closePlayer.Entity.Pos.XYZ) > squareDistance)
                    continue;

                channel.SendPacket(packet, closePlayer as IServerPlayer);
            }
        }
    }
}
