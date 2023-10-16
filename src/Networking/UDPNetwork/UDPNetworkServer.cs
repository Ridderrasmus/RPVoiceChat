using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public class UDPNetworkServer : UDPNetworkBase, IExtendedNetworkServer
    {
        public event Action<AudioPacket> AudioPacketReceived;

        private Dictionary<string, ConnectionInfo> connectionsByPlayer = new Dictionary<string, ConnectionInfo>();
        private IPAddress ip;
        private IPEndPoint ownEndPoint;
        private ConnectionInfo connectionInfo;

        public UDPNetworkServer(int port, string ip, bool forwardPorts) : base(Logger.server, forwardPorts)
        {
            this.port = port;
            this.ip = IPAddress.Parse(ip ?? NetworkUtils.GetPublicIP());
            ownEndPoint = NetworkUtils.GetEndPoint(GetConnectionInfo());

            OnMessageReceived += MessageReceived;
        }

        public void Launch()
        {
            if (!NetworkUtils.IsInternalNetwork(ip))
                SetupUpnp(port);
            OpenUDPClient(port);
            StartListening();
            VerifyServerReadiness();
        }

        public ConnectionInfo GetConnectionInfo()
        {
            if (connectionInfo != null) return connectionInfo;

            connectionInfo = new ConnectionInfo()
            {
                Address = ip.MapToIPv4().ToString(),
                Port = port
            };

            return connectionInfo;
        }

        public bool SendPacket(NetworkPacket packet, string playerId)
        {
            ConnectionInfo connectionInfo;
            if (!connectionsByPlayer.TryGetValue(playerId, out connectionInfo)) return false;

            var data = packet.ToBytes();
            var destination = NetworkUtils.GetEndPoint(connectionInfo);

            UdpClient.Send(data, data.Length, destination);
            return true;
        }

        public void PlayerConnected(string playerId, ConnectionInfo connectionInfo)
        {
            connectionsByPlayer.Add(playerId, connectionInfo);
            logger.VerboseDebug($"{playerId} connected over {_transportID}");
        }

        public void PlayerDisconnected(string playerId)
        {
            if (!connectionsByPlayer.ContainsKey(playerId)) return;
            connectionsByPlayer.Remove(playerId);
            logger.VerboseDebug($"{playerId} disconnected from {_transportID} server");
        }

        private void MessageReceived(byte[] msg, IPEndPoint sender)
        {
            PacketType code = (PacketType)BitConverter.ToInt32(msg, 0);
            switch (code)
            {
                case PacketType.SelfPing:
                    if (!IsSelf(sender)) return;
                    isReady = true;
                    _readinessProbeCTS.Cancel();
                    break;
                case PacketType.Ping:
                    SendEchoPacket(sender);
                    break;
                case PacketType.Audio:
                    var packet = NetworkPacket.FromBytes<AudioPacket>(msg);
                    AudioPacketReceived?.Invoke(packet);
                    break;
                default:
                    throw new Exception($"Unsupported packet type: {code}");
            }
        }

        private void SendEchoPacket(IPEndPoint endPoint)
        {
            var echoPacket = BitConverter.GetBytes((int)PacketType.Pong);
            UdpClient.Send(echoPacket, echoPacket.Length, endPoint);
        }

        private void VerifyServerReadiness()
        {
            var selfPingPacket = BitConverter.GetBytes((int)PacketType.SelfPing);

            try
            {
                UdpClient.Send(selfPingPacket, selfPingPacket.Length, ownEndPoint);
                Task.Delay(1000, _readinessProbeCTS.Token).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException) { }

            if (isReady) return;
            throw new Exception("Server failed readiness probe. Aborting to prevent silent malfunction");
        }

        private bool IsSelf(IPEndPoint endPoint)
        {
            return NetworkUtils.AssertEqual(endPoint, ownEndPoint);
        }
    }
}
