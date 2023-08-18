
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace rpvoicechat.Networking
{
    public class RPVoiceChatNativeNetworkServer : RPVoiceChatNativeNetwork
    {
        private ICoreServerAPI api;
        private IServerNetworkChannel channel;
        public Action<IServerPlayer, AudioPacket> OnReceivedPacket;
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
            string key;
            switch (packet.VoiceLevel)
            {
                case VoiceLevel.Whispering:
                    key = "rpvoicechat:distance-whisper";
                    break;
                case VoiceLevel.Talking:
                    key = "rpvoicechat:distance-talk";
                    break;
                case VoiceLevel.Shouting:
                    key = "rpvoicechat:distance-shout";
                    break;
                default:
                    key = "rpvoicechat:distance-talk";
                    break;
            }

            var distance = api.World.Config.GetInt(key);

            foreach (var closePlayer in api.World.AllOnlinePlayers)
            {
                if (closePlayer == player)
                    continue;

                if (closePlayer.Entity == null)
                    continue;

                if (player.Entity.Pos.SquareDistanceTo(closePlayer.Entity.Pos.XYZ) > distance * distance)
                    continue;

                channel.SendPacket(packet, closePlayer as IServerPlayer);
            }
        }
    }
}
