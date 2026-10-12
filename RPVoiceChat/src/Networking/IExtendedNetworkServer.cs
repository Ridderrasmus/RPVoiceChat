namespace RPVoiceChat.Networking
{
    public interface IExtendedNetworkServer : INetworkServer
    {
        public void PlayerConnected(string playerId, ConnectionInfo connectionInfo);
        public void PlayerDisconnected(string playerId);
    }
}
