using System;
using System.Collections.Generic;

namespace RPVoiceChat.Networking
{
    public class UDPNetworkServer : UDPNetworkBase, IExtendedNetworkServer
    {
        public event Action<AudioPacket> OnReceivedPacket;

        private Dictionary<string, ConnectionInfo> connectionsByPlayer = new Dictionary<string, ConnectionInfo>();

        public UDPNetworkServer(int port)
        {
            this.port = port;

            OnMessageReceived += MessageReceived;
        }

        public void Launch()
        {
            SetupUpnp(port);
            OpenUDPClient(port);
            StartListening(port);
        }

        public override ConnectionInfo GetConnection()
        {
            if (connectionInfo != null) return connectionInfo;

            connectionInfo = new ConnectionInfo()
            {
                Address = GetPublicIP(),
                Port = port
            };

            return connectionInfo;
        }

        public void SendPacket(INetworkPacket packet, string playerId)
        {
            ConnectionInfo connectionInfo;
            if (!connectionsByPlayer.TryGetValue(playerId, out connectionInfo))
                throw new Exception($"Player {playerId} is not connected to the server");

            var data = packet.ToBytes();
            var destination = GetEndPoint(connectionInfo);

            UdpClient.Send(data, data.Length, destination);
        }

        private void MessageReceived(byte[] msg)
        {
            try
            {
                var packet = AudioPacket.FromBytes(msg);
                OnReceivedPacket?.Invoke(packet);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Couldn't parse received message: {e}");
            }
        }

        public void PlayerConnected(string playerId, ConnectionInfo connectionInfo)
        {
            connectionsByPlayer.Add(playerId, connectionInfo);
        }

        public void PlayerDisconnected(string playerId)
        {
            connectionsByPlayer.Remove(playerId);
        }
    }
}
