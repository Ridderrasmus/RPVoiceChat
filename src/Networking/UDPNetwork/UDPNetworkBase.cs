using Open.Nat;
using RPVoiceChat.Utils;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public abstract class UDPNetworkBase : IDisposable
    {
        public event Action<byte[]> OnMessageReceived;

        private Thread _listeningThread;
        private CancellationTokenSource _listeningCTS;

        protected UdpClient UdpClient;
        protected int port;
        protected ConnectionInfo connectionInfo;
        protected const string _transportID = "UDP";
        protected bool upnpEnabled = true;
        protected Logger logger;
        protected bool isReady = false;


        public string GetTransportID()
        {
            return _transportID;
        }

        public virtual ConnectionInfo GetConnection()
        {
            if (connectionInfo != null) return connectionInfo;

            connectionInfo = new ConnectionInfo()
            {
                Port = port
            };

            return connectionInfo;
        }

        public void TogglePortForwarding(bool? state = null)
        {
            upnpEnabled = state ?? !upnpEnabled;
        }

        protected bool IsInternalNetwork(string ip)
        {
            return IsInternalNetwork(IPAddress.Parse(ip));
        }

        protected bool IsInternalNetwork(IPAddress ip)
        {
            byte[] ipParts = ip.GetAddressBytes();

            if (ipParts[0] == 10 ||
               (ipParts[0] == 192 && ipParts[1] == 168) ||
               (ipParts[0] == 172 && (ipParts[1] >= 16 && ipParts[1] <= 31)) ||
               (ipParts[0] == 25 || ipParts[0] == 26))
                return true;

            return false;
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
                device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, "Vintage Story Voice Chat"));
            }
            catch (TaskCanceledException)
            {
                logger.Warning("Device discovery got aborted, assuming public IP");
            }
        }

        protected void OpenUDPClient(int port)
        {
            UdpClient = new UdpClient(port);
        }

        protected int OpenUDPClient()
        {
            int port = 0;
            UdpClient = new UdpClient();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, port);
            UdpClient.Client.Bind(endpoint);

            var localEndpoint = UdpClient.Client.LocalEndPoint as IPEndPoint;
            port = localEndpoint.Port;
            return port;
        }

        protected void StartListening(int port)
        {
            var endpoint = new IPEndPoint(IPAddress.Any, port);
            StartListening(endpoint);
        }

        protected void StartListening(IPEndPoint ipendpoint)
        {
            if (UdpClient == null) throw new Exception("Udp client has not been initialized. Can't start listening.");

            _listeningCTS = new CancellationTokenSource();
            _listeningThread = new Thread(() => Listen(ipendpoint, _listeningCTS.Token));
            _listeningThread.Start();
        }

        protected void Listen(IPEndPoint ipendpoint, CancellationToken ct)
        {
            while (_listeningThread.IsAlive && !ct.IsCancellationRequested)
            {
                try
                {
                    byte[] msg = UdpClient.Receive(ref ipendpoint);

                    OnMessageReceived?.Invoke(msg);
                }
                catch (SocketException e)
                {
                    // Windows will notify us here when destination of *sent* message is unreachable. We don't care.
                    if (e.SocketErrorCode == SocketError.ConnectionReset) continue;
                    if (e.SocketErrorCode == SocketError.Interrupted ||
                        ct.IsCancellationRequested) return;

                    throw e;
                }
            }
        }

        protected IPEndPoint GetEndPoint(ConnectionInfo connectionInfo)
        {
            var address = IPAddress.Parse(connectionInfo.Address);
            var endpoint = new IPEndPoint(address, connectionInfo.Port);

            return endpoint;
        }

        protected string GetPublicIP()
        {
            string publicIPString = new HttpClient().GetStringAsync("https://ipinfo.io/ip").GetAwaiter().GetResult();

            return publicIPString;
        }

        public void Dispose()
        {
            _listeningCTS?.Cancel();
            _listeningCTS?.Dispose();
            UdpClient?.Close();
            UdpClient?.Dispose();
        }
    }
}
