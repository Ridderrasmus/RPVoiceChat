using System;

namespace RPVoiceChat.Networking
{
    public interface INetworkServer
    {
        public event Action<AudioPacket> OnReceivedPacket;

        public void SendPacket(INetworkPacket packet, string playerId);
    }
}
