using Open.Nat;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public abstract class UDPNetworkBase : IDisposable
    {
        public event Action<byte[]> OnMessageReceived;

        private Thread _listeningThread;

        protected UdpClient UdpClient;
        protected int port;
        protected ConnectionInfo connectionInfo;
        protected const string _transportID = "UDP";


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
            // UPnP using Open.Nat
            NatDiscoverer discoverer = new NatDiscoverer();
            Task<NatDevice> task = Task.Run(() => discoverer.DiscoverDeviceAsync());
            NatDevice device = task.GetAwaiter().GetResult();
            if (device == null)
                throw new NatDeviceNotFoundException("NatDiscoverer have not returned the NatDevice");

            device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, "Vintage Story Voice Chat"));
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

            _listeningThread = new Thread(Listen);
            _listeningThread.Start(ipendpoint);
        }

        protected void Listen(object arg)
        {
            var ipendpoint = arg as IPEndPoint;
            while (_listeningThread.IsAlive)
            {
                byte[] msg = UdpClient.Receive(ref ipendpoint);

                OnMessageReceived?.Invoke(msg);
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
            string publicIPString = new WebClient().DownloadString("https://ipinfo.io/ip");

            return publicIPString;
        }

        public void Dispose()
        {
            _listeningThread?.Abort();
            UdpClient?.Close();
            UdpClient?.Dispose();
        }
    }
}
