using System;
using System.Net;

namespace RPVoiceChat.Networking
{
    public class UDPNetworkClient : UDPNetworkBase, IExtendedNetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;

        private IPEndPoint serverEndpoint;

        public UDPNetworkClient()
        {
            OnMessageReceived += MessageReceived;
        }

        public ConnectionInfo Connect(ConnectionInfo serverConnection)
        {
            serverEndpoint = GetEndPoint(serverConnection);
            port = OpenUDPClient();

            if (!IsInternalNetwork(serverConnection.Address))
                SetupUpnp(port);
            StartListening(serverEndpoint);

            var clientConnection = GetConnection();
            return clientConnection;
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            if (UdpClient == null || serverEndpoint == null) throw new Exception("Udp client or server endpoint has not been initialized.");

            var data = packet.ToBytes();
            UdpClient.Send(data, data.Length, serverEndpoint);
        }

        private void MessageReceived(byte[] msg)
        {
            AudioPacket packet = AudioPacket.FromBytes(msg);
            OnAudioReceived?.Invoke(packet);
        }
    }
}
