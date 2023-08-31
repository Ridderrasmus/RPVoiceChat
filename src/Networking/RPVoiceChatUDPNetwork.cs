using rpvoicechat;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class RPVoiceChatUDPNetwork : INetworkCommon
    {
        private Thread _listeningThread;
        private ICoreAPI api;

        protected UdpClient UdpClient;
        protected const string ChannelName = "RPHandshakeChannel";

        public event Action<byte[]> OnMessageReceived;

        public RPVoiceChatUDPNetwork(ICoreAPI api)
        {
            this.api = api;
            api.Network.RegisterChannel(ChannelName).RegisterMessageType<ConnectionInfo>();
        }

        protected void OpenUDPClient(int port)
        {
            UdpClient = new UdpClient(port);
        }

        protected int OpenUDPClient()
        {
            int port = 0;
            UdpClient = new UdpClient();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, port);
            UdpClient.Client.Bind(endpoint);

            port = ((IPEndPoint)UdpClient.Client.LocalEndPoint).Port;
            return port;
        }

        protected void StartListening(IPEndPoint ipendpoint)
        {
            if (UdpClient == null) throw new Exception("Udp client has not been initialized. Can't start listening.");


            while (_listeningThread.IsAlive)
            {
                byte[] msg = UdpClient.Receive(ref ipendpoint);

                OnMessageReceived?.Invoke(msg);
            }
        }

        protected void StartListening(IPAddress address, int port)
        {
            StartListening(new IPEndPoint(address, port));
        }

        protected void StartListening(string address, int port)
        {
            StartListening(IPAddress.Parse(address), port);
        }

        protected void StartListening(int port)
        {
            StartListening(IPAddress.Any, port);
        }

        public IPEndPoint GetPublicIP()
        {
            if (UdpClient == null) throw new Exception("Udp client has not been initialized.");

            return new IPEndPoint();
        }
    }
}
