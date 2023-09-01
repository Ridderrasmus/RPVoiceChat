using System;
using Vintagestory.API.Server;

namespace RPVoiceChat.Networking
{
    public class NativeNetworkServer : NativeNetworkBase, INetworkServer
    {
        public event Action<AudioPacket> OnReceivedPacket;
        private ICoreServerAPI api;
        private IServerNetworkChannel channel;

        public NativeNetworkServer(ICoreServerAPI sapi) : base(sapi)
        {
            api = sapi;
            channel = sapi.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(ReceivedAudioPacketFromClient);
        }

        public void SendPacket(INetworkPacket packet, string playerId)
        {
            var player = api.World.PlayerByUid(playerId) as IServerPlayer;
            channel.SendPacket(packet, player);
        }

        private void ReceivedAudioPacketFromClient(IServerPlayer player, AudioPacket packet)
        {
            OnReceivedPacket?.Invoke(packet);
        }
    }
}
