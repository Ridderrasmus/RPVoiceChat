namespace RPVoiceChat.Networking
{
    public interface IExtendedNetworkClient : INetworkClient
    {
        public ConnectionInfo OnHandshakeReceived(ConnectionInfo serverConnection);
    }
}
