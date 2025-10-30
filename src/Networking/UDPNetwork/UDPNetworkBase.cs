using Open.Nat;
using RPVoiceChat.Util;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public abstract class UDPNetworkBase : IDisposable
    {
        protected event Action<byte[], IPEndPoint> OnMessageReceived;

        private Thread _listeningThread;
        private CancellationTokenSource _listeningCTS;

        protected UdpClient UdpClient;
        protected int port;
        protected const string _transportID = "UDP";
        protected bool upnpEnabled;
        protected Logger logger;
        protected CancellationTokenSource _readinessProbeCTS;
        protected bool isReady = false;

        public UDPNetworkBase(Logger logger, bool forwardPorts)
        {
            this.logger = logger;
            upnpEnabled = forwardPorts;
            _readinessProbeCTS = new CancellationTokenSource();
        }

        public string GetTransportID()
        {
            return _transportID;
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

                if (device == null) throw new NatDeviceNotFoundException();

                logger.VerboseDebug("Found a UPnP device, creating port map");
                device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, "Vintage Story Voice Chat"));
            }
            catch (TaskCanceledException)
            {
                throw new NoStackTraceException("Device discovery got aborted");
            }
            catch (NatDeviceNotFoundException)
            {
                throw new NoStackTraceException($"Unable to port forward with UPnP, {_transportID} connection may not be available. Make sure your IP is public and UPnP is enabled if you want to use {_transportID} transport.");
            }
        }

        protected void OpenUDPClient(int port)
        {
            UdpClient = new UdpClient(port);
            UdpClient.Client.ReceiveBufferSize = UdpClient.Client.SendBufferSize = 64 * 1024;
        }

        protected int OpenUDPClient()
        {
            int port = 0;
            UdpClient = new UdpClient();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, port);
            UdpClient.Client.Bind(endpoint);
            UdpClient.Client.ReceiveBufferSize = UdpClient.Client.SendBufferSize = 64 * 1024;

            var localEndpoint = UdpClient.Client.LocalEndPoint as IPEndPoint;
            port = localEndpoint.Port;
            return port;
        }

        protected void StartListening()
        {
            if (UdpClient == null) throw new Exception("Udp client has not been initialized. Can't start listening.");

            _listeningCTS = new CancellationTokenSource();
            _listeningThread = new Thread(Listen);
            _listeningThread.Start(_listeningCTS.Token);
        }

        private void Listen(object cancellationToken)
        {
            CancellationToken ct = (CancellationToken)cancellationToken;
            while (_listeningThread.IsAlive && !ct.IsCancellationRequested)
            {
                IPEndPoint sender = null;
                try
                {
                    byte[] msg = UdpClient.Receive(ref sender);
                    if (msg.Length < 4) continue;

                    OnMessageReceived?.Invoke(msg, sender);
                }
                catch (Exception e)
                {
                    var se = e as SocketException;
                    // Windows will notify us here when destination of *sent* message is unreachable. We don't care.
                    if (se?.SocketErrorCode == SocketError.ConnectionReset) continue;
                    if (se?.SocketErrorCode == SocketError.Interrupted ||
                        ct.IsCancellationRequested) return;

                    logger.Error($"Datagram from {sender?.Address} caused an exception:\n{e}");
                    continue;
                }
            }
        }

        public void Dispose()
        {
            _readinessProbeCTS?.Cancel();
            _readinessProbeCTS?.Dispose();
            _listeningCTS?.Cancel();
            _listeningCTS?.Dispose();
            UdpClient?.Close();
            UdpClient?.Dispose();
        }
    }
}
