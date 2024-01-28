using Open.Nat;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private Dictionary<string, string> playerAddresses = new Dictionary<string, string>();
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
            if (playerAddresses.ContainsKey(playerId)) PlayerDisconnected(playerId);
            var playerAddress = NetworkUtils.GetEndPoint(connectionInfo).ToString();
            playerAddresses.Add(playerId, playerAddress);
            logger.VerboseDebug($"{playerId} connected over {_transportID}");
        }

        public void PlayerDisconnected(string playerId)
        {
            if (!playerAddresses.ContainsKey(playerId)) return;
            playerAddresses.Remove(playerId);
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
                catch (Exception e)
                {
                    if (e is SocketException se &&
                        (se.SocketErrorCode == SocketError.Interrupted ||
                        se.SocketErrorCode == SocketError.OperationAborted) ||
                        ct.IsCancellationRequested) return;

                    logger.Error($"Caught exception outside of main thread! Proceeding to ignore it to avoid server crash:\n{e}");
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

        private void ConnectionClosed(bool isGraceful, bool isHalfClosed, TCPConnection connection)
        {
            var address = connection.remoteEndpoint.ToString();
            if (!connectionsByAddress.ContainsKey(address)) return;

            string player = ResolvePlayer(connection);
            connectionsByAddress.Remove(address);
            connection.Dispose();

            var closeType = isGraceful ? "gracefully" : "unexpectedly";
            closeType = isHalfClosed ? "by client's request" : closeType;
            logger.VerboseDebug($"{_transportID} connection with {address} was closed {closeType}");
            if (player != null) PlayerDisconnected(player);
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

        private void SetupUpnp(int port)
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
            _readinessProbeCTS = new CancellationTokenSource();

            try
            {
                using var connection = new TCPConnection(logger);
                connection.ConnectAsync(ownEndPoint)
                    .ContinueWith(
                        _ => connection.SendAsync(selfPingPacket, _readinessProbeCTS.Token),
                        TaskContinuationOptions.OnlyOnRanToCompletion
                    );
                Task.Delay(5000, _readinessProbeCTS.Token).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException) { }

            if (isReady) return;
            throw new HealthCheckException(NetworkSide.Server);
        }

        private TCPConnection ResolveConnection(string playerId)
        {
            string playerAddress;
            TCPConnection connection;
            if (!playerAddresses.TryGetValue(playerId, out playerAddress)) return null;
            if (!connectionsByAddress.TryGetValue(playerAddress, out connection)) return null;

            return connection;
        }

        private string ResolvePlayer(TCPConnection connection)
        {
            var playerAddress = connectionsByAddress.FirstOrDefault(e => e.Value == connection).Key;
            if (playerAddress == null) return null;
            var playerId = playerAddresses.FirstOrDefault(e => e.Value == playerAddress).Key;

            return playerId;
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
