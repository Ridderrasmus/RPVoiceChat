using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public class UDPNetworkClient : UDPNetworkBase, IExtendedNetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;

        private IPEndPoint serverEndpoint;
        private CancellationTokenSource _readinessProbeCTS;

        public UDPNetworkClient() : base(Utils.Logger.client)
        {
            _readinessProbeCTS = new CancellationTokenSource();

            OnMessageReceived += MessageReceived;
        }

        public ConnectionInfo Connect(ConnectionInfo serverConnection)
        {
            serverEndpoint = GetEndPoint(serverConnection);
            port = OpenUDPClient();

            if (!IsInternalNetwork(serverConnection.Address))
                SetupUpnp(port);
            StartListening();
            VerifyClientReadiness();

            var clientConnection = GetConnection();
            return clientConnection;
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            if (UdpClient == null || serverEndpoint == null) throw new Exception("Udp client or server endpoint has not been initialized.");

            var data = packet.ToBytes();
            UdpClient.Send(data, data.Length, serverEndpoint);
        }

        private void MessageReceived(byte[] msg, IPEndPoint sender)
        {
            if (!IsServer(sender))
            {
                logger.Warning($"Received unauthorized message from {sender}, proceeding to ignore it");
                return;
            }

            PacketType code = (PacketType)BitConverter.ToInt32(msg, 0);
            switch (code)
            {
                case PacketType.Pong:
                    isReady = true;
                    _readinessProbeCTS.Cancel();
                    break;
                case PacketType.Audio:
                    AudioPacket packet = NetworkPacket.FromBytes<AudioPacket>(msg);
                    OnAudioReceived?.Invoke(packet);
                    break;
                default:
                    logger.Error($"Received unsupported packet type: {code}, proceeding to ignore it");
                    return;
            }
        }

        private void VerifyClientReadiness()
        {
            var pingPacket = BitConverter.GetBytes((int)PacketType.Ping);

            try
            {
                UdpClient.Send(pingPacket, pingPacket.Length, serverEndpoint);
                Task.Delay(3000, _readinessProbeCTS.Token).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException) { }

            if (isReady) return;
            throw new Exception("Client failed readiness probe. Aborting to prevent silent malfunction");
        }

        private bool IsServer(IPEndPoint endPoint)
        {
            return AssertEqual(endPoint, serverEndpoint);
        }
    }
}
