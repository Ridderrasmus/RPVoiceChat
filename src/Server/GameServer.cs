using RPVoiceChat.Networking;
using Vintagestory.API.Server;

namespace rpvoicechat.Server
{
    public class GameServer
    {
        private ICoreServerAPI api;
        private INetworkServer networkServer;

        public GameServer(ICoreServerAPI sapi, INetworkServer server)
        {
            api = sapi;
            networkServer = server;
        }

        public void Launch()
        {
            networkServer.OnReceivedPacket += SendAudioToAllClientsInRange;
        }

        public void SendAudioToAllClientsInRange(IServerPlayer player, AudioPacket packet)
        {
            int distance = WorldConfig.GetVoiceDistance(api, packet.VoiceLevel);
            int squareDistance = distance * distance;

            foreach (var closePlayer in api.World.AllOnlinePlayers)
            {
                if (closePlayer == player ||
                    closePlayer.Entity == null ||
                    player.Entity.Pos.SquareDistanceTo(closePlayer.Entity.Pos.XYZ) > squareDistance)
                    continue;

                networkServer.SendPacket(packet, closePlayer as IServerPlayer);
            }
        }
    }
}
