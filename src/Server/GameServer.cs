using RPVoiceChat.Networking;
using Vintagestory.API.Server;

namespace rpvoicechat.Server
{
    public class GameServer
    {
        private ICoreServerAPI api;
        private INetworkServer networkServer;
        private bool handshakeRequired;
        private IServerNetworkChannel handshakeChannel;

        public GameServer(ICoreServerAPI sapi, INetworkServer server)
        {
            api = sapi;
            networkServer = server;
            handshakeRequired = server is IExtendedNetworkServer;
            handshakeChannel = sapi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionInfo>(FinalizeHandshake);
        }

        public void Launch()
        {
            if (handshakeRequired)
            {
                var extendedServer = networkServer as IExtendedNetworkServer;
                extendedServer.Launch();
                api.Event.PlayerNowPlaying += PlayerJoined;
                api.Event.PlayerDisconnect += PlayerLeft;
            }
            networkServer.OnReceivedPacket += SendAudioToAllClientsInRange;
        }

        public void PlayerJoined(IServerPlayer player)
        {
            InitHandshake(player);
        }

        public void PlayerLeft(IServerPlayer player)
        {
            var extendedServer = networkServer as IExtendedNetworkServer;
            extendedServer.PlayerDisconnected(player.PlayerUID);
        }

        public void SendAudioToAllClientsInRange(AudioPacket packet)
        {
            var player = api.World.PlayerByUid(packet.PlayerId);
            int distance = WorldConfig.GetVoiceDistance(api, packet.VoiceLevel);
            int squareDistance = distance * distance;

            foreach (var closePlayer in api.World.AllOnlinePlayers)
            {
                if (closePlayer == player ||
                    closePlayer.Entity == null ||
                    player.Entity.Pos.SquareDistanceTo(closePlayer.Entity.Pos.XYZ) > squareDistance)
                    continue;

                networkServer.SendPacket(packet, closePlayer.PlayerUID);
            }
        }

        private void InitHandshake(IServerPlayer player)
        {
            if (!handshakeRequired) return;
            var extendedServer = networkServer as IExtendedNetworkServer;
            var serverConnection = extendedServer.GetConnection();
            handshakeChannel.SendPacket(serverConnection, player);
        }

        private void FinalizeHandshake(IServerPlayer player, ConnectionInfo playerConnection)
        {
            if (!handshakeRequired) return;
            var extendedServer = networkServer as IExtendedNetworkServer;
            extendedServer.PlayerConnected(player.PlayerUID, playerConnection);
        }
    }
}
