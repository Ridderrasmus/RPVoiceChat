using System;
using System.Net;
using System.Net.Http;
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
        string serverIp;
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            serverApi = api;

            // Register serverside connection to the the network channel
            serverChannel = serverApi.Network.GetChannel("rpvoicechat")
                .SetMessageHandler<ConnectionPacket>(OnHandshakeReceived);

            // Register event listener
            serverIp = GetPublicIp();
            serverApi.Event.PlayerNowPlaying += OnPlayerCreate;
            serverApi.Event.PlayerDisconnect += OnPlayerDisconnect;

            // Sockets
            socketServer = new RPVoiceChatSocketServer(serverApi);
            Task.Run(() => socketServer.StartListening());
            socketServer.OnServerAudioPacketReceived += Server_PacketReceived;
        }

        private void OnHandshakeReceived(IServerPlayer fromPlayer, ConnectionPacket packet)
        {
            socketServer.AddClientConnection(fromPlayer.PlayerUID, new IPEndPoint(IPAddress.Parse(packet.packetIp), packet.packetPort));
            serverApi.Logger.Debug("Handshake received from " + fromPlayer.PlayerUID + " with ip " + packet.packetIp + ":" + packet.packetPort);
            foreach (var client in socketServer.clients)
            {
                serverApi.Logger.Debug("Client: " + client.Key + " " + client.Value.ToString());
            }
        }

        private void Server_PacketReceived(PlayerAudioPacket packet)
        {
            foreach (var player in serverApi.World.AllOnlinePlayers)
            {
                if (player.PlayerUID == packet.playerUid) continue;
                if (player.Entity.Pos.DistanceTo(packet.audioPos) > (int)packet.voiceLevel) continue;

                socketServer.SendToClient(player.PlayerUID, packet);
            }
        }

        private void OnPlayerCreate(IServerPlayer player)
        {
            
            while (socketServer.RemoteEndPoint == null) { }
            serverChannel.SendPacket(new ConnectionPacket { playerUid = player.PlayerUID, packetIp = serverIp }, player);
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            socketServer.RemoveClientConnection(player.PlayerUID);
        }

    }
}
