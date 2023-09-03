using System;

namespace RPVoiceChat.Networking
{
    public interface INetworkServer
    {
        public event Action<AudioPacket> OnReceivedPacket;

        public ConnectionInfo GetConnection();
        public string GetTransportID();
        public void SendPacket(INetworkPacket packet, string playerId);
    }
}
