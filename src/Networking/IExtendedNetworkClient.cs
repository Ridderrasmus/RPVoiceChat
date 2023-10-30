using System;

namespace RPVoiceChat.Networking
{
    public interface IExtendedNetworkClient : INetworkClient
    {
        public event Action<bool, IExtendedNetworkClient> OnConnectionLost;

        public ConnectionInfo Connect(ConnectionInfo serverConnection);
    }
}
