using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    public class RPVoiceChatServer : RPVoiceChatCommon
    {

        IServerNetworkChannel serverChannel;
        ICoreServerAPI serverApi;
        RPVoiceChatSocketServer socketServer;
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            serverApi = api;

            // Register serverside connection to the the network channel
            serverChannel = serverApi.Network.GetChannel("rpvoicechat");
            //    .SetMessageHandler<PlayerAudioPacket>(OnAudioRecieved);

            //
            serverApi.Event.PlayerNowPlaying += OnPlayerCreate;

            // Sockets
            socketServer = new RPVoiceChatSocketServer(serverApi);
            Task.Run(() => socketServer.StartAsync());
            socketServer.OnAudioPacketReceived += Server_PacketReceived;

            serverApi.Logger.Debug("[RPVoiceChat] Server started");
        }

        private void Server_PacketReceived(PlayerAudioPacket packet)

        {
            foreach (var player in serverApi.World.AllOnlinePlayers)
            {
                if (player.Entity.Pos.DistanceTo(packet.audioPos) > (int)packet.voiceLevel) continue;

                socketServer.SendToClient(player.PlayerUID, packet);

                serverApi.Logger.Debug("[RPVoiceChat] Sending audio to " + player.PlayerName);
            }
        }

        private void OnPlayerCreate(IServerPlayer player)
        {
            serverApi.Logger.Debug("[RPVoiceChat] Player created");
            serverChannel.SendPacket(new ConnectionPacket { playerUid = player.PlayerUID, serverIp = serverApi.Server.ServerIp }, player);
        }

    }
}
