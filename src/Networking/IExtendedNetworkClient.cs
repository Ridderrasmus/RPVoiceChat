using System;

namespace RPVoiceChat.Networking
{
    public interface IExtendedNetworkClient : INetworkClient
    {
        public event Action OnConnectionLost;

        public ConnectionInfo Connect(ConnectionInfo serverConnection);
    }
}
