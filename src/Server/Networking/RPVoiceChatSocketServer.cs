using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    internal class RPVoiceChatSocketServer : RPVoiceChatSocketCommon
    {
        private ICoreServerAPI serverApi;

        // Dictionary of connected clients
        ConcurrentDictionary<string, EndPoint> clients = new ConcurrentDictionary<string, EndPoint>();

        public event Action<PlayerAudioPacket> OnServerAudioPacketReceived;

        public RPVoiceChatSocketServer(ICoreServerAPI serverApi)
        {
            this.serverApi = serverApi;
            this.IsServer = true;
            this.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        }

        public async Task StartAsync()
        {
            StartListening();
        }

        private void StartListening()
        {
            Task.Run(() =>
            {
                byte[] buffer = new byte[bufferSize];

                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));

                while (true)
                {
                    EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    int receivedBytes = serverSocket.ReceiveFrom(buffer, ref remoteEP);

                    byte[] receivedData = new byte[receivedBytes];
                    Array.Copy(buffer, 0, receivedData, 0, receivedBytes);

                    PlayerAudioPacket packet = DeserializePacket(receivedData);

                    // Invoke the event
                    OnServerAudioPacketReceived?.Invoke(packet);

                    AddClientConnection(packet.playerUid, remoteEP);
                }
            });
        }

        public async Task StopAsync()
        {
            serverSocket?.Close();
        }

        public bool AddClientConnection(string playerUid, EndPoint endPoint)
        {
            if (playerUid == null || endPoint == null) return false;

            if (clients.ContainsKey(playerUid)) return false;
            
            clients.TryAdd(playerUid, endPoint);
            
            return true;
        }

        private EndPoint RemoveClientConnection(string playerUid)
        {
            EndPoint endPoint;
            clients.TryRemove(playerUid, out endPoint);
            return endPoint;
        }

        // Public method to send a message to all connected clients
        public void SendToAllAsync(byte[] buffer)
        {
            foreach (IPEndPoint client in clients.Values)
            {
                Socket sendingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sendingSocket.SendTo(buffer, client);
            }
        }

        // Public method to send a message to a specific client using the playeruid as the key
        public void SendToClient(string playerUid, byte[] buffer)
        {
            EndPoint client;
            if (!clients.TryGetValue(playerUid, out client)) return;

            if (client != null)
            {
                serverSocket.SendTo(buffer, client); // Use the existing serverSocket to send
            }
        }

        // Public method to send a PlayerAudioPacket to a specific client using the playeruid as the key
        public void SendToClient(string playerUid, PlayerAudioPacket packet)
        {
            byte[] buffer = SerializePacket(packet);
            SendToClient(playerUid, buffer);
        }
    }
}
