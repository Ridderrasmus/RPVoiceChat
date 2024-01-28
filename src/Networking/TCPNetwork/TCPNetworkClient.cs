using RPVoiceChat.Utils;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public class TCPNetworkClient : TCPNetworkBase, IExtendedNetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;
        public event Action<bool, IExtendedNetworkClient> OnConnectionLost;

        private const int maxConnectionAttempts = 5;
        private int connectionAttempt = 0;
        private IPEndPoint serverEndpoint;
        private TCPConnection connection;

        public TCPNetworkClient() : base(Logger.client) { }

        public ConnectionInfo Connect(ConnectionInfo serverConnection)
        {
            serverEndpoint = NetworkUtils.GetEndPoint(serverConnection);
            Connect();

            return new ConnectionInfo(port);
        }

        public bool SendAudioToServer(AudioPacket packet)
        {
            if (isReady == false || connection == null)
            {
                logger.Warning($"Attempting to send audio over {_transportID} while client isn't ready. Skipping sending.");
                return false;
            }

            var data = packet.ToBytes();
            connection.Send(data);
            return true;
        }

        private ConnectionInfo Connect()
        {
            connectionAttempt++;
            try
            {
                connection = OpenConnection(serverEndpoint);
                VerifyClientReadiness();
                connectionAttempt = 0;
                return new ConnectionInfo(port);
            }
            catch
            {
                if (connectionAttempt > maxConnectionAttempts) throw;
                logger.VerboseDebug($"[Internal] Connection attempt over {_transportID} failed, retrying ({connectionAttempt}/{maxConnectionAttempts})");
                return Connect();
            }
        }

        private TCPConnection OpenConnection(IPEndPoint endPoint)
        {
            var connection = new TCPConnection(logger);
            connection.OnMessageReceived += MessageReceived;
            connection.OnDisconnected += ConnectionClosed;
            connection.Connect(endPoint);
            connection.StartListening();
            port = connection.port;

            return connection;
        }

        private void ConnectionClosed(bool isGraceful, bool isHalfClosed, TCPConnection closedConnection)
        {
            isReady = false;
            var closeType = isGraceful ? "gracefully" : "unexpectedly";
            closeType = isHalfClosed ? "by server's request" : closeType;
            logger.VerboseDebug($"Connection with {_transportID} server was closed {closeType}");
            closedConnection.Dispose();
            bool canReconnect = !isGraceful || isHalfClosed;
            OnConnectionLost?.Invoke(canReconnect, this);
        }

        private void MessageReceived(byte[] msg, TCPConnection _)
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
            _readinessProbeCTS = new CancellationTokenSource();

            try
            {
                connection.SendAsync(pingPacket, _readinessProbeCTS.Token);
                Task.Delay(3000, _readinessProbeCTS.Token).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException) { }

            if (isReady) return;
            throw new HealthCheckException(NetworkSide.Client);
        }

        public override void Dispose()
        {
            base.Dispose();
            connection?.Dispose();
        }
    }
}
