using Open.Nat;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public class TCPNetworkServer : TCPNetworkBase, IExtendedNetworkServer
    {
        public event Action<AudioPacket> AudioPacketReceived;

        private Dictionary<string, TCPConnection> connectionsByAddress = new Dictionary<string, TCPConnection>();
        private Dictionary<string, ConnectionInfo> connectionInfoByPlayer = new Dictionary<string, ConnectionInfo>();
        private IPAddress ip;
        protected bool upnpEnabled;
        private IPEndPoint ownEndPoint;
        private ConnectionInfo connectionInfo;
        private Socket socket;
        private Thread _listeningThread;
        private CancellationTokenSource _listeningCTS;

        public TCPNetworkServer(int port, string ip, bool forwardPorts) : base(Logger.server)
        {
            this.port = port;
            this.ip = IPAddress.Parse(ip ?? NetworkUtils.GetPublicIP());
            upnpEnabled = forwardPorts;
            ownEndPoint = NetworkUtils.GetEndPoint(GetConnectionInfo());
        }

        public void Launch()
        {
            if (!NetworkUtils.IsInternalNetwork(ip))
                SetupUpnp(port);
            StartServer();
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
            TCPConnection connection = ResolveConnection(playerId);
            if (connection == null) return false;

            var data = packet.ToBytes();
            connection.Send(data);

            return true;
        }

        public void PlayerConnected(string playerId, ConnectionInfo connectionInfo)
        {
            connectionInfoByPlayer.Add(playerId, connectionInfo);
            logger.VerboseDebug($"{playerId} connected over {_transportID}");
        }

        public void PlayerDisconnected(string playerId)
        {
            if (!connectionInfoByPlayer.ContainsKey(playerId)) return;
            connectionInfoByPlayer.Remove(playerId);
            logger.VerboseDebug($"{playerId} disconnected from {_transportID} server");
        }

        private void StartServer()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveTimeout = -1;
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            socket.Listen(10);

            _listeningCTS = new CancellationTokenSource();
            _listeningThread = new Thread(AcceptConnection);
            _listeningThread.Start(_listeningCTS.Token);
        }

        private void AcceptConnection(object cancellationToken)
        {
            CancellationToken ct = (CancellationToken)cancellationToken;
            while (_listeningThread.IsAlive && !ct.IsCancellationRequested)
            {
                try
                {
                    var connectionSocket = socket.Accept();

                    ConnectionAccepted(connectionSocket);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.Interrupted ||
                        e.SocketErrorCode == SocketError.OperationAborted ||
                        ct.IsCancellationRequested) return;

                    throw;
                }
            }
        }

        private void ConnectionAccepted(Socket socket)
        {
            var sender = socket.RemoteEndPoint as IPEndPoint;
            logger.VerboseDebug($"Accepted connection from {sender}");

            var connection = new TCPConnection(logger, socket);
            connection.OnMessageReceived += MessageReceived;
            connection.OnDisconnected += ConnectionClosed;
            connection.StartListening();
            connectionsByAddress[sender.ToString()] = connection;
        }

        private void ConnectionClosed(bool isGraceful, TCPConnection connection)
        {
            var address = connection.remoteEndpoint.ToString();
            if (!connectionsByAddress.ContainsKey(address)) return;
            connectionsByAddress.Remove(address);
            logger.VerboseDebug($"{_transportID} connection with {address} was closed {(isGraceful ? "gracefully" : "unexpectedly")}");
        }

        private void MessageReceived(byte[] msg, TCPConnection channel)
        {
            PacketType code = (PacketType)BitConverter.ToInt32(msg, 0);
            switch (code)
            {
                case PacketType.SelfPing:
                    if (isReady) return;
                    isReady = true;
                    _readinessProbeCTS.Cancel();
                    break;
                case PacketType.Ping:
                    SendEchoPacket(channel);
                    break;
                case PacketType.Audio:
                    var packet = NetworkPacket.FromBytes<AudioPacket>(msg);
                    _ = Task.Run(() => AudioPacketReceived?.Invoke(packet));
                    break;
                default:
                    throw new Exception($"Unsupported packet type: {code}");
            }
        }

        private void SendEchoPacket(TCPConnection connection)
        {
            var echoPacket = BitConverter.GetBytes((int)PacketType.Pong);
            try
            {
                connection.SendAsync(echoPacket);
            }
            catch (Exception e)
            {
                logger.Debug($"Failed to send echo request to {connection.remoteEndpoint}: {e.Message}");
                connection.Disconnect();
            }
        }

        protected void SetupUpnp(int port)
        {
            if (!upnpEnabled) return;

            try
            {
                // UPnP using Open.Nat
                logger.VerboseDebug("Attempting to portforward with UPnP");
                NatDiscoverer discoverer = new NatDiscoverer();
                CancellationTokenSource cts = new CancellationTokenSource(5000);
                Task<NatDevice> task = Task.Run(() => discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts));
                NatDevice device = task.GetAwaiter().GetResult();

                if (device == null)
                    throw new NatDeviceNotFoundException("NatDiscoverer have not returned the NatDevice");

                logger.VerboseDebug("Found a UPnP device, creating port map");
                device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, "Vintage Story Voice Chat"));
            }
            catch (TaskCanceledException)
            {
                logger.Warning("Device discovery got aborted, assuming public IP");
            }
            catch (NatDeviceNotFoundException)
            {
                logger.Warning($"Failed to port forward with UPnP. {_transportID} server may not be available if your IP is private or you are behind NAT");
            }
        }

        private void VerifyServerReadiness()
        {
            var selfPingPacket = BitConverter.GetBytes((int)PacketType.SelfPing);

            try
            {
                using var connection = new TCPConnection(logger);
                connection.ConnectAsync(ownEndPoint)
                    .ContinueWith(
                        _ => connection.SendAsync(selfPingPacket, _readinessProbeCTS.Token),
                        TaskContinuationOptions.OnlyOnRanToCompletion
                    );
                Task.Delay(1000, _readinessProbeCTS.Token).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException) { }

            if (isReady) return;
            throw new Exception("Server failed readiness probe. Aborting to prevent silent malfunction");
        }

        private TCPConnection ResolveConnection(string playerId)
        {
            ConnectionInfo connectionInfo;
            TCPConnection connection;
            if (!connectionInfoByPlayer.TryGetValue(playerId, out connectionInfo)) return null;
            string playerIp = NetworkUtils.GetEndPoint(connectionInfo).ToString();
            if (!connectionsByAddress.TryGetValue(playerIp, out connection)) return null;

            return connection;
        }

        public override void Dispose()
        {
            base.Dispose();
            _listeningCTS?.Cancel();
            _listeningCTS?.Dispose();
            socket?.Close();
            foreach (var connection in connectionsByAddress.Values)
                connection?.Dispose();
        }
    }
}
