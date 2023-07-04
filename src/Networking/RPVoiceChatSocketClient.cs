using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using System;
using Lidgren.Network;

namespace rpvoicechat
{
    public class RPVoiceChatSocketClient : RPVoiceChatSocketCommon
    {
        NetClient client;

        public event EventHandler OnClientConnected;
        public event EventHandler OnClientDisconnected;

        public RPVoiceChatSocketClient(ICoreClientAPI capi)
        {
            this.capi = capi;
            
            client = new NetClient(config);
            client.Start();
            port = client.Port;

            capi.Logger.Notification("[RPVoiceChat - Client] Started on port " + port);
        }

        public void ConnectToServer(string address, int port)
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("Address cannot be null or empty");
            
            if (port < 0 || port > 65535)
                throw new ArgumentException("Port must be between 0 and 65535");

            capi.Logger.Notification($"[RPVoiceChat - Client] Connecting to voice chat server {address}:{port} ");
            client.Connect(address, port);

            OnClientConnected?.Invoke(this, null);
            StartListening(client);

        }

        public void SendAudioToServer(AudioPacket packet)
        {
            NetOutgoingMessage msg = client.CreateMessage();
            packet.WriteToMessage(msg);
            client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced);
        }

        public void Close()
        {
            client.Disconnect("Disconnecting");
            client.Shutdown("Client shutting down");
        }
    }
}
