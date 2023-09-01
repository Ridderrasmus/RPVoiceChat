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


        protected void SetupUpnp(int port)
        {
            NatDevice device = null;
            try
            {
                // UPnP using Mono.Nat
                NatDiscoverer discoverer = new NatDiscoverer();
                device = Task.Run(() => discoverer.DiscoverDeviceAsync()).Result;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Couldn't discover a device for UPnP: {e}");
            }

            if (device != null)
            {
                device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, "Vintage Story Voice Chat"));
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

        public ConnectionInfo GetConnection()
        {
            var remoteEndpoint = UdpClient.Client.RemoteEndPoint as IPEndPoint; //TODO: This throws an exception and LocalEndPoint doesn't have the IP address. Has to be resolved in some other way
            var connection = new ConnectionInfo()
            {
                Address = remoteEndpoint.Address.ToString(),
                Port = remoteEndpoint.Port
            };

            return connection;
        }

        public IPEndPoint GetEndPoint(ConnectionInfo connectionInfo)
        {
            var address = IPAddress.Parse(connectionInfo.Address);
            var endpoint = new IPEndPoint(address, connectionInfo.Port);

            return endpoint;
        }

        public void Dispose()
        {
            _listeningThread.Abort();
        }
    }
}
