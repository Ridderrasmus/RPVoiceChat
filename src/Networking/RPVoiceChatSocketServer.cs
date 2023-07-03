using Lidgren.Network;
using ProperVersion;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    public class RPVoiceChatSocketServer : RPVoiceChatSocketCommon
    {

        NetServer server;

        public RPVoiceChatSocketServer(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            config.Port = port;
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);

            server = new NetServer(config);
            server.Start();

            OnMessageReceived += RPVoiceChatSocketServer_OnMessageReceived;

            StartListening(server);

            sapi.Logger.Notification("RPVoiceChat server started on port " + port);
        }

        private void RPVoiceChatSocketServer_OnMessageReceived(object sender, NetIncomingMessage e)
        {
            SendAudioToAllClients(AudioPacket.ReadFromMessage(e));
        }

        public void SendAudioToAllClients(AudioPacket packet)
        {
            NetOutgoingMessage message = server.CreateMessage();
            packet.WriteToMessage(message);
            server.SendToAll(message, NetDeliveryMethod.UnreliableSequenced);
        }

        public void SendAudioToClient(AudioPacket packet, NetConnection client)
        {
            NetOutgoingMessage message = server.CreateMessage();
            packet.WriteToMessage(message);
            server.SendMessage(message, client, NetDeliveryMethod.UnreliableSequenced);
        }

        public void Close()
        {
            server.Shutdown("Server shutting down");
        }

        public string GetPublicIPAddress()
        {
            return new WebClient().DownloadString("https://ipv4.icanhazip.com/").Replace("\n", "");
        }

        public int GetLocalPort()
        {
            return server.Configuration.Port;
        }
    }
}
