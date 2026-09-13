using System;

namespace RPVoiceChat.Networking
{
    public interface INetworkServer : IDisposable
    {
        public event Action<AudioPacket> AudioPacketReceived;

        public void Launch();
        public ConnectionInfo GetConnectionInfo();
        public string GetTransportID();
        public bool SendPacket(NetworkPacket packet, string playerId);
        public bool SendPacket(PreparedNetworkPacket packet, string playerId) => SendPacket(packet.Packet, playerId);
    }
}
