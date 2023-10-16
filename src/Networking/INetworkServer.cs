using System;

namespace RPVoiceChat.Networking
{
    public interface INetworkServer : IDisposable
    {
        public event Action<AudioPacket> AudioPacketReceived;

        public ConnectionInfo GetConnectionInfo();
        public string GetTransportID();
        public bool SendPacket(NetworkPacket packet, string playerId);
    }
}
