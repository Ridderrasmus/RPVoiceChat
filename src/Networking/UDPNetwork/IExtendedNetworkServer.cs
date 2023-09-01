namespace RPVoiceChat.Networking
{
    public interface IExtendedNetworkServer : INetworkServer
    {
        public void Launch();
        public ConnectionInfo GetConnection();
        public void PlayerConnected(string playerId, ConnectionInfo connectionInfo);
        public void PlayerDisconnected(string playerId);
    }
}
