using System;
using Vintagestory.API.Server;

namespace RPVoiceChat.Networking
{
    public class NativeNetworkServer : NativeNetworkBase, INetworkServer
    {
        public event Action<AudioPacket> AudioPacketReceived;
        private ICoreServerAPI api;
        private IServerNetworkChannel channel;
        private readonly VoicePacketWorker<AudioPacket> incoming;

        public NativeNetworkServer(ICoreServerAPI sapi) : base(sapi)
        {
            api = sapi;
            incoming = new VoicePacketWorker<AudioPacket>(DispatchAudioPacket,
                error => api.Logger.Warning("[RPVoiceChat] Native voice routing failed: " + error.Message));
            channel = api.Network.GetChannel(ChannelName).SetMessageHandler<AudioPacket>(ReceivedAudioPacketFromClient);
            if (api.Server.IsDedicated == false)
                api.Network.RegisterChannel(SPChannelName)
                    .RegisterMessageType<AudioPacket>()
                    .SetMessageHandler<AudioPacket>(ReceivedAudioPacketFromClient);

        }

        public void Launch()
        {
        }

        public ConnectionInfo GetConnectionInfo()
        {
            var connectionInfo = new ConnectionInfo();
            return connectionInfo;
        }

        public bool SendPacket(NetworkPacket packet, string playerId)
            => SendPacket(new PreparedNetworkPacket(packet), playerId);

        public bool SendPacket(PreparedNetworkPacket packet, string playerId)
        {
            var player = api.World.PlayerByUid(playerId) as IServerPlayer;
            if (player == null || player.ConnectionState != EnumClientState.Playing)
            {
                return false;
            }
            channel.SendPacket((AudioPacket)packet.Packet, packet.Payload, player);
            return true;
        }

        private void ReceivedAudioPacketFromClient(IServerPlayer player, AudioPacket packet)
        {
            // Security: ignore client-supplied PlayerId and use the authenticated sender
            packet.PlayerId = player.PlayerUID;
            // Use the game's authenticated channel dispatch; do not copy and deserialize
            // every movement/interaction packet on a second thread to find voice packets.
            incoming.Enqueue(packet);
        }

        private void DispatchAudioPacket(AudioPacket packet)
        {
            AudioPacketReceived?.Invoke(packet);
        }

        public void Dispose()
        {
            incoming.Dispose();
        }
    }
}
