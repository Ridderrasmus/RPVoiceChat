using System;

namespace RPVoiceChat.Networking
{
    public interface INetworkServer : IDisposable
    {
        public event Action<AudioPacket> AudioPacketReceived;

        public ConnectionInfo GetConnection();
        public string GetTransportID();
        public void SendPacket(NetworkPacket packet, string playerId);
    }
}
