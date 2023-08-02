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
    public class ConnectionStatusUpdate : EventArgs
    {
        public NetConnectionStatus Status { get; set; }
        public string Reason { get; set; }
    }

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
            config.EnableMessageType(NetIncomingMessageType.StatusChanged);
            config.EnableMessageType(NetIncomingMessageType.ErrorMessage);

            client = new NetClient(config);
            OnStatucChangedReceived += RPVoiceChatSocketServer_OnStatusChanged;
            OnErrorMessageReceived += RPVoiceChatSocketServer_OnErrorMessageReceived;

            port = client.Port;
            client.Start();

            StartListening(client);

            capi.Logger.Notification("[RPVoiceChat - Client] Started on port " + port);
        }

        private void RPVoiceChatSocketServer_OnErrorMessageReceived(object sender, NetIncomingMessage e)
        {
            sapi.Logger.Error("[RPVoiceChat:SocketServer] Error received {0}", e.ReadString());
        }

        public void ConnectToServer(string address, int port)
        {
            string clientUID = capi?.World.Player.PlayerUID;
            while (clientUID == null) { clientUID = capi?.World.Player.PlayerUID; }
            serverAddress = address;
            serverPort = port;
            NetOutgoingMessage hail = client.CreateMessage("RPVoiceChat " + clientUID);
            client.Connect("192.168.1.238", serverPort, hail);
        }
        private void RPVoiceChatSocketServer_OnStatusChanged(object sender, NetIncomingMessage e)
        {
            // you need a way to know the connection status. this solves that problem
            NetConnectionStatus status = (NetConnectionStatus)e.ReadByte();
            var args = new ConnectionStatusUpdate()
            {
                Status = status,
                Reason = e.ReadString()
            };

            switch (status)
            {
                // we have a new valid connection
                case NetConnectionStatus.Connected:
                    OnClientConnected?.Invoke(this, args);
                    break;
                // we have lost a connection (for good or bad reasons)
                case NetConnectionStatus.Disconnected:
                    OnClientDisconnected?.Invoke(this, args);
                    break;
            }
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

        public override void Dispose()
        {
            base.Dispose();

            client = null;
        }
    }
}
