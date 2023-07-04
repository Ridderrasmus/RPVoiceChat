using Lidgren.Network;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    public class RPVoiceChatSocketServer : RPVoiceChatSocketCommon
    {

        NetServer server;
        private long totalPacketSize = 0;
        private long totalPacketCount = 0;

        public RPVoiceChatSocketServer(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            config.EnableUPnP = true;
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);

            server = new NetServer(config);
            server.Start();

            if(server.UPnP.ForwardPort(port, "Vintage Story Voice Chat"))
                sapi.Logger.Notification("[RPVoiceChat - Server] UPnP successful.");
            else
                sapi.Logger.Warning("[RPVoiceChat - Server] UPnP failed.");
            port = server.Port;


            OnMessageReceived += RPVoiceChatSocketServer_OnMessageReceived;
            OnDiscoveryRequestReceived += RPVoiceChatSocketServer_OnDiscoveryRequestReceived;

            StartListening(server);

            Task.Run(() =>
            {
                TcpListener listener = new TcpListener(IPAddress.Any, port);
                listener.Start();

                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    client.Close();
                }
            });

            sapi.Logger.Notification("[RPVoiceChat - Server] Started on port " + port);
        }

        private void RPVoiceChatSocketServer_OnDiscoveryRequestReceived(object sender, NetIncomingMessage e)
        {
            server.SendDiscoveryResponse(null, e.SenderEndPoint);
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

            totalPacketSize += message.LengthBytes;
            totalPacketCount++;
        }

        public void SendAudioToClient(AudioPacket packet, NetConnection client)
        {
            NetOutgoingMessage message = server.CreateMessage();
            packet.WriteToMessage(message);
            server.SendMessage(message, client, NetDeliveryMethod.UnreliableSequenced);

            totalPacketSize += message.LengthBytes;
            totalPacketCount++;
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
