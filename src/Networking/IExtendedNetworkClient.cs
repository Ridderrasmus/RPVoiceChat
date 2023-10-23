using System;

namespace RPVoiceChat.Networking
{
    public interface IExtendedNetworkClient : INetworkClient
    {
        public event Action<bool> OnConnectionLost;

        public ConnectionInfo Connect(ConnectionInfo serverConnection);
    }
}
