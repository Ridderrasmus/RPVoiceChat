namespace RPVoiceChat.Networking
{
    public interface IExtendedNetworkClient : INetworkClient
    {
        public ConnectionInfo Connect(ConnectionInfo serverConnection);
    }
}
