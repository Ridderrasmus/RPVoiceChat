using RPVoiceChat.Util;
using System;
using System.Net;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public class UDPNetworkClient : UDPNetworkBase, IExtendedNetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;
        public event Action<bool, IExtendedNetworkClient> OnConnectionLost = delegate { }; //UDP is connectionless, event should never fire

        private IPEndPoint serverEndpoint;

        public UDPNetworkClient(bool forwardPorts) : base(Logger.client, forwardPorts)
        {
            OnMessageReceived += MessageReceived;
        }

        public ConnectionInfo Connect(ConnectionInfo serverConnection)
        {
            serverEndpoint = NetworkUtils.GetEndPoint(serverConnection);
            port = OpenUDPClient();

            if (!NetworkUtils.IsInternalNetwork(serverConnection.Address))
                SetupUpnp(port);
            StartListening();
            VerifyClientReadiness();

            return new ConnectionInfo(port);
        }

        public bool SendAudioToServer(AudioPacket packet)
        {
            if (UdpClient == null || serverEndpoint == null)
            {
                logger.Warning($"{_transportID} client or server endpoint have not been initialized.");
                return false;
            }

            var data = packet.ToBytes();
            UdpClient.Send(data, data.Length, serverEndpoint);
            return true;
        }

        private void MessageReceived(byte[] msg, IPEndPoint sender)
        {
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
            throw new HealthCheckException(NetworkSide.Client);
        }
    }
}
