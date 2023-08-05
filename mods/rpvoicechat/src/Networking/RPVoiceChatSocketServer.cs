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
            config.UseMessageRecycling = true;
            config.AcceptIncomingConnections = true;
            config.EnableUPnP = true;
            
            // Config options
            config.MaximumConnections = ModConfig.Config.MaximumConnections;
            config.Port = ModConfig.Config.ServerPort;

            server = new NetServer(config);

            server.Start();

            port = server.Port;
            if (server.UPnP.ForwardPort(port, "Vintage Story Voice Chat"))
                sapi.Logger.Notification("[RPVoiceChat - Server] UPnP successful.");
            else
                sapi.Logger.Warning("[RPVoiceChat - Server] UPnP failed. Port forwarding needed for voice chat to function normally.");

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
                // we want to add or update always
                // the .Add before would just throw an exception if the key already existed,
                // but we don't care if it does, we just care that the connection is correctly set
                connections[msgUID] = e.SenderConnection;
            }
            else
            {
                // Otherwise, reject it
                e.SenderConnection.Deny();
                sapi.Logger.Debug($"Denying connection from {e.SenderConnection}");

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
            //var players = sapi.World.GetPlayersAround(player.Entity.Pos.XYZ, distance, distance);

            // The reason we use this instead of the above is because the above doesn't work in blocks.
            var players = sapi.World.AllOnlinePlayers;

            foreach(var closePlayer in players)
            {
                if (closePlayer == player)
                    continue;

                if (closePlayer.Entity == null)
                    continue;

                if (player.Entity.Pos.SquareDistanceTo(closePlayer.Entity.Pos.XYZ) > distance*distance)
                    continue;

                if (connections.TryGetValue(closePlayer.PlayerUID, out var connection))
                {
                    SendAudioToClient(packet, connection);
                }
                else
                {
                    sapi.Logger.Error($"Failed to get connection for player {closePlayer.PlayerName} when sending audio packet");
                }
            }
        }

        public void SendAudioToClient(AudioPacket packet, NetConnection client)
        {
            NetOutgoingMessage message = server.CreateMessage();
            packet.WriteToMessage(message);
            server.SendMessage(message, client, deliveryMethod);
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

        public override void Dispose()
        {
            base.Dispose();

            Close();
            server = null;
        }
    }
}
