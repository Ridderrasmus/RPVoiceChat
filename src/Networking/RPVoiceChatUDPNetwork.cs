using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RPVoiceChat
{
    public class RPVoiceChatUDPNetwork : INetworkCommon
    {
        private Thread _listeningThread;

        protected UdpClient UdpClient;

        public event Action<byte[]> OnMessageReceived;

        public RPVoiceChatUDPNetwork()
        {
            UdpClient = new UdpClient(52525);
        }

        public RPVoiceChatUDPNetwork(int port)
        {
            UdpClient = new UdpClient(port);
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
            throw new NotImplementedException();
        }
    }
}
