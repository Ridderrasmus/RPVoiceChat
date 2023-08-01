using Lidgren.Network;
using System.Collections.Generic;
using System.Net;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    public class RPVoiceChatSocketServer : RPVoiceChatSocketCommon
    {
        public NetServer server;
        private long totalPacketSize = 0;
        private long totalPacketCount = 0;

        private Dictionary<string, NetConnection> connections = new Dictionary<string, NetConnection>();

        public RPVoiceChatSocketServer(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            BootVoiceServer();
        }


        public RPVoiceChatSocketServer(ICoreServerAPI sapi, int serverPort)
        {
            this.sapi = sapi;
            this.port = serverPort;
            BootVoiceServer();
        }

        private void BootVoiceServer()
        {
            config.EnableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryResponse);
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.EnableMessageType(NetIncomingMessageType.StatusChanged);
            config.MaximumConnections = 100;
            config.UseMessageRecycling = true;
            config.AcceptIncomingConnections = true;
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

        public void SendAudioToAllClientsInRange(AudioPacket packet)
        {
            string key;
            switch(packet.VoiceLevel)
            {
                case VoiceLevel.Whispering:
                    key = "rpvoicechat:distance-whisper";
                    break;
                case VoiceLevel.Normal:
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

            foreach (var connection in connections)
            {

                if (connection.Key == packet.PlayerId)
                    continue;

                SendAudioToClient(packet, connection.Value);
            }
        }

        public void SendAudioToClient(AudioPacket packet, NetConnection client)
        {
            NetOutgoingMessage message = server.CreateMessage();
            packet.WriteToMessage(message);
            server.SendMessage(message, client, deliveryMethod);

            totalPacketSize += message.LengthBytes;
            totalPacketCount++;
        }
        public void SendAudioToClient(AudioPacket packet, string uid)
        {
            NetConnection connection;
            if(connections.TryGetValue(uid, out connection))
                SendAudioToClient(packet, connection);
        }

        public void AddClientToDictionary(string uid, NetConnection connection)
        {
            connections.Add(uid, connection);
        }

        public void RemoveClientFromDictionary(string uid)
        {
            connections.Remove(uid);
        }


        public void Close()
        {
            server.Shutdown("[RPVoiceChat - Server] Shutting down");
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
    }
}
