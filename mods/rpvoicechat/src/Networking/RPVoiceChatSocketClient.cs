using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using System;
using Lidgren.Network;
using System.Windows.Forms;

namespace rpvoicechat
{
    public class RPVoiceChatSocketClient : RPVoiceChatSocketCommon
    {
        public NetClient client;

        public event EventHandler OnClientConnected;
        public event EventHandler OnClientDisconnected;

        private string serverAddress;
        private int serverPort;

        public RPVoiceChatSocketClient(ICoreClientAPI capi)
        {
            this.capi = capi;

            config.Port = 0;

            client = new NetClient(config);

            port = client.Port;
            client.Start();

            StartListening(client);

            capi.Logger.Notification("[RPVoiceChat - Client] Started on port " + port);
        }

        public void ConnectToServer(string address, int port)
        {
            string clientUID = capi?.World.Player.PlayerUID;
            while (clientUID == null) { clientUID = capi?.World.Player.PlayerUID; }
            serverAddress = address;
            serverPort = port;
            NetOutgoingMessage hail = client.CreateMessage("RPVoiceChat " + clientUID);
            client.Connect(serverAddress, serverPort, hail);
            
            OnClientConnected.Invoke(this, null);
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            NetOutgoingMessage msg = client.CreateMessage();
            packet.WriteToMessage(msg);
            client.SendMessage(msg, deliveryMethod);
        }

        public void Close()
        {
            client.Disconnect("Disconnecting");
            client.Shutdown("Client shutting down");
        }

    }
}
