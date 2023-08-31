using RPVoiceChat.Networking;
using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace rpvoicechat.Networking
{
    public class NativeNetworkServer : NativeNetworkBase, INetworkServer
    {
        public event Action<IServerPlayer, AudioPacket> OnReceivedPacket;
        private IServerNetworkChannel channel;

        public NativeNetworkServer(ICoreServerAPI api) : base(api)
        {
            channel = api.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(ReceivedAudioPacketFromClient);
        }

        public void SendPacket(AudioPacket packet, IServerPlayer player)
        {
            channel.SendPacket(packet, player);
        }

        private void ReceivedAudioPacketFromClient(IServerPlayer player, AudioPacket packet)
        {
            OnReceivedPacket?.Invoke(player, packet);
        }
    }
}
