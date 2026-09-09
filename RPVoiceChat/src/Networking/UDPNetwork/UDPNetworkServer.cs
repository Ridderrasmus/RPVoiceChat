using RPVoiceChat.Util;
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
        private Dictionary<string, string> playerByAddress = new Dictionary<string, string>();
        private IPAddress ip;
        private IPEndPoint ownEndPoint;
        private ConnectionInfo connectionInfo;
        private long readinessProbeToken;

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
            if (connectionsByPlayer.TryGetValue(playerId, out var oldConn))
                playerByAddress.Remove(NetworkUtils.GetEndPoint(oldConn).ToString());
            var addressKey = NetworkUtils.GetEndPoint(connectionInfo).ToString();
            if (playerByAddress.ContainsKey(addressKey)) playerByAddress.Remove(addressKey);
            connectionsByPlayer[playerId] = connectionInfo;
            playerByAddress[addressKey] = playerId;
            logger.VerboseDebug($"{playerId} connected over {_transportID}");
        }

        public void PlayerDisconnected(string playerId)
        {
            if (!connectionsByPlayer.TryGetValue(playerId, out var conn)) return;
            var addressKey = NetworkUtils.GetEndPoint(conn).ToString();
            connectionsByPlayer.Remove(playerId);
            playerByAddress.Remove(addressKey);
            logger.VerboseDebug($"{playerId} disconnected from {_transportID} server");
        }

        private void MessageReceived(byte[] msg, IPEndPoint sender)
        {
            PacketType code = (PacketType)BitConverter.ToInt32(msg, 0);
            switch (code)
            {
                case PacketType.SelfPing:
                    if (!IsValidReadinessSelfPing(msg)) return;
                    isReady = true;
                    _readinessProbeCTS.Cancel();
                    break;
                case PacketType.Ping:
                    SendEchoPacket(sender);
                    break;
                case PacketType.Audio:
                    var packet = NetworkPacket.FromBytes<AudioPacket>(msg);
                    if (!playerByAddress.TryGetValue(sender.ToString(), out string senderPlayerId))
                        break; // drop audio from unauthenticated connection
                    packet.PlayerId = senderPlayerId;
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
            readinessProbeToken = Random.Shared.NextInt64();
            byte[] selfPingPacket = CreateSelfPingPacket(readinessProbeToken);

            try
            {
                // Loopback avoids NAT hairpin issues where the reply appears from the router, not ownEndPoint.
                UdpClient.Send(selfPingPacket, selfPingPacket.Length, new IPEndPoint(IPAddress.Loopback, port));

                if (!IPAddress.IsLoopback(ownEndPoint.Address))
                {
                    UdpClient.Send(selfPingPacket, selfPingPacket.Length, ownEndPoint);
                }

                Task.Delay(5000, _readinessProbeCTS.Token).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException) { }

            readinessProbeToken = 0;

            if (isReady) return;
            throw new HealthCheckException(NetworkSide.Server);
        }

        private static byte[] CreateSelfPingPacket(long token)
        {
            byte[] packet = new byte[12];
            BitConverter.GetBytes((int)PacketType.SelfPing).CopyTo(packet, 0);
            BitConverter.GetBytes(token).CopyTo(packet, 4);
            return packet;
        }

        private bool IsValidReadinessSelfPing(byte[] msg)
        {
            if (readinessProbeToken == 0 || msg.Length < 12) return false;
            if ((PacketType)BitConverter.ToInt32(msg, 0) != PacketType.SelfPing) return false;
            return BitConverter.ToInt64(msg, 4) == readinessProbeToken;
        }
    }
}
