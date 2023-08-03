using Lidgren.Network;
using System.Collections.Generic;
using System.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    public class RPVoiceChatSocketServer : RPVoiceChatSocketCommon
    {
        public NetServer server;
        private long totalPacketSize = 0;
        private long totalPacketCount = 0;

        private Dictionary<string, NetConnection> connections = new Dictionary<string, NetConnection>();

        public RPVoiceChatSocketServer(ICoreServerAPI sapi, int serverPort)
        {
            this.sapi = sapi;
            this.port = serverPort;
            sapi.Event.PlayerLeave += Event_PlayerLeave;
            BootVoiceServer();
        }

        private void Event_PlayerLeave(IServerPlayer player)
        {
            if (connections.TryGetValue(player.PlayerUID, out var connection))
            {
                if (connection.Status != NetConnectionStatus.Disconnected)
                {
                    connection.Disconnect("Player left");
                }

                connections.Remove(player.PlayerUID);
            }
        }

        private void BootVoiceServer()
        {
            config.EnableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryResponse);
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.EnableMessageType(NetIncomingMessageType.StatusChanged);
            config.EnableMessageType(NetIncomingMessageType.ErrorMessage);
            // this should probably be a config or something similar. 
            config.MaximumConnections = 100;
            config.UseMessageRecycling = true;
            config.AcceptIncomingConnections = true;
            // this should probably also be a config
            config.Port = port;
            config.EnableUPnP = true;

            server = new NetServer(config);

            server.Start();

            port = server.Port;
            if (server.UPnP.ForwardPort(port, "Vintage Story Voice Chat"))
                sapi.Logger.Notification("[RPVoiceChat - Server] UPnP successful.");
            else
                sapi.Logger.Warning("[RPVoiceChat - Server] UPnP failed.");

            OnMessageReceived += RPVoiceChatSocketServer_OnMessageReceived;
            OnConnectionApprovalMessage += RPVoiceChatSocketServer_OnConnectionApprovalMessage;
            OnDiscoveryRequestReceived += RPVoiceChatSocketServer_OnDiscoveryRequestReceived;
            OnErrorMessageReceived += RPVoiceChatSocketServer_OnErrorReceived;

            StartListening(server);

            sapi.Logger.Notification("[RPVoiceChat - Server] Started on port " + port);
        }

        private void RPVoiceChatSocketServer_OnConnectionApprovalMessage(object sender, NetIncomingMessage e)
        {
            string[] msgString = e.ReadString().Split(' ');
            string msgKey = msgString[0];
            string msgUID = msgString[1];

            // If it's the below string, accept the connection
            if (msgKey == "RPVoiceChat")
            {
                e.SenderConnection.Approve();
                connections.Add(msgUID, e.SenderConnection);
            }
            else
            {
                // Otherwise, reject it
                e.SenderConnection.Deny();
            }
        }

        private void RPVoiceChatSocketServer_OnDiscoveryRequestReceived(object sender, NetIncomingMessage e)
        {
            server.SendDiscoveryResponse(null, e.SenderEndPoint);
        }

        private void RPVoiceChatSocketServer_OnMessageReceived(object sender, NetIncomingMessage e)
        {
            SendAudioToAllClientsInRange(AudioPacket.ReadFromMessage(e));
        }

        private void RPVoiceChatSocketServer_OnErrorReceived(object sender, NetIncomingMessage e)
        {
            // this is needed to help debug possible errors that happen, always find ways to get as much information as possible
            sapi.Logger.Error("[RPVoiceChat:SocketServer] Error received {0}", e.ReadString());
        }

        public void SendAudioToAllClientsInRange(AudioPacket packet)
        {
            string key;
            switch(packet.VoiceLevel)
            {
                case VoiceLevel.Whispering:
                    key = "rpvoicechat:distance-whisper";
                    break;
                case VoiceLevel.Talking:
                    key = "rpvoicechat:distance-talk";
                    break;
                case VoiceLevel.Shouting:
                    key = "rpvoicechat:distance-shout";
                    break;
                default:
                    key = "rpvoicechat:distance-talk";
                    break;
            }

            int distance = sapi.World.Config.GetInt(key);


            IPlayer player = sapi.World.PlayerByUid(packet.PlayerId);
            // this might look slower but it should result in being faster, this (hopefully) uses collision partitioning,
            // if so instead of looping through literally all players it only loops through the ones within a partition.
            var players = sapi.World.GetPlayersAround(player.Entity.Pos.XYZ, distance, distance);

            foreach(var closePlayer in players)
            {
                if (closePlayer == player)
                    continue;

                if (connections.TryGetValue(closePlayer.PlayerUID, out var connection))
                {
                    SendAudioToClient(packet, connection);
                }
            }
        }

        public void SendAudioToClient(AudioPacket packet, NetConnection client)
        {
            NetOutgoingMessage message = server.CreateMessage();
            packet.WriteToMessage(message);
            server.SendMessage(message, client, deliveryMethod);

            // why do this ? Is there a reason ?
            totalPacketSize += message.LengthBytes;
            totalPacketCount++;
        }
        public void SendAudioToClient(AudioPacket packet, string uid)
        {
            if (connections.TryGetValue(uid, out var connection))
            {
                SendAudioToClient(packet, connection);
            }
        }

        public void Close()
        {
            server.Shutdown("[RPVoiceChat - Server] Shutting down");
            connections.Clear();
        }

        public string GetPublicIPAddress()
        {
            return new WebClient().DownloadString("https://ipv4.icanhazip.com/").Replace("\n", "");
        }

        public int GetPort()
        {
            return server.Port;
        }

        public double GetAveragePacketSize()
        {
            if (totalPacketCount == 0)
            {
                return 0;
            }
            else
            {
                return (double)totalPacketSize / totalPacketCount;
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            Close();
            server = null;
        }
    }
}
