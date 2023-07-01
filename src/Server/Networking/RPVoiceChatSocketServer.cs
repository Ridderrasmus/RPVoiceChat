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

            port = int.Parse(serverApi.World.Config.GetString("rpvoicechat:port", "52525"));
        }

        public void StartListening()
        {
            serverApi.Logger.Debug("Server started with port: " + port);

            byte[] buffer = new byte[bufferSize];
            EndPoint remoteEP= new IPEndPoint(IPAddress.Any, 0);

            try 
            { 
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            catch (Exception e)
            {
                serverApi.Logger.Error("Failed to bind to port: " + port);
                serverApi.Logger.Error(e.Message);
                return;
            }

            while (true)
            {
                int receivedBytes = serverSocket.ReceiveFrom(buffer, ref remoteEP);

                byte[] receivedData = new byte[receivedBytes];
                Array.Copy(buffer, 0, receivedData, 0, receivedBytes);

                PlayerAudioPacket packet = DeserializePacket(receivedData);

                // Invoke the event
                OnServerAudioPacketReceived?.Invoke(packet);

                AddClientConnection(packet.playerUid, remoteEP);
            }
        }

        public bool AddClientConnection(string playerUid, EndPoint endPoint)
        {
            if (string.IsNullOrEmpty(playerUid) || endPoint == null) return false;

            if (clients.ContainsKey(playerUid)) return false;
            
            clients.TryAdd(playerUid, endPoint);
            
            return true;
        }

        public EndPoint RemoveClientConnection(string playerUid)
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
