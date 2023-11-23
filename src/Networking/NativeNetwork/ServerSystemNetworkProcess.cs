using HarmonyLib;
using RPVoiceChat.Utils;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.Server;

namespace RPVoiceChat.Networking
{
    public class ServerSystemNetworkProcess : IDisposable
    {
        public event Func<int, Packet_CustomPacket, IServerPlayer, bool> OnProcessInBackground;

        private const int _customPacketId = 23;
        private ICoreServerAPI api;
        private Thread packetProcessingThread;
        private CancellationTokenSource _packetProcessingCTS;

        public ServerSystemNetworkProcess(ICoreServerAPI sapi)
        {
            api = sapi;
            packetProcessingThread = new Thread(NetworkProcess);
            _packetProcessingCTS = new CancellationTokenSource();
        }

        public void Launch()
        {
            packetProcessingThread.Start(_packetProcessingCTS.Token);
        }

        private static FieldInfo senderConnectionField = AccessTools.Field(typeof(NetIncomingMessage), "SenderConnection");
        private static FieldInfo messageField = AccessTools.Field(typeof(NetIncomingMessage), "message");
        private static FieldInfo messageLengthField = AccessTools.Field(typeof(NetIncomingMessage), "messageLength");

        private void NetworkProcess(object cancellationToken)
        {
            var ct = (CancellationToken)cancellationToken;
            while (packetProcessingThread.IsAlive && !ct.IsCancellationRequested)
            {
                try
                {
                    NetIncomingMessage msg;
                    while ((msg = TcpNetServerPatch.ReadMessage()) != null)
                    {
                        var connection = (NetConnection)senderConnectionField.GetValue(msg);
                        var player = ResolveServerPlayer(connection);
                        if (player == null) continue;
                        var data = (byte[])messageField.GetValue(msg);
                        var length = (int)messageLengthField.GetValue(msg);
                        TryReadPacket(data, length, player);
                    }
                    Thread.Sleep(1);
                }
                catch (Exception e)
                {
                    Logger.server.Error($"Caught exception outside of main thread! Proceeding to ignore it to avoid server crash:\n{e}");
                }
            }
        }

        private void TryReadPacket(byte[] data, int dataLength, IServerPlayer sender)
        {
            Packet_Client packet = new Packet_Client();
            Packet_ClientSerializer.DeserializeBuffer(data, dataLength, packet);

            ProcessInBackground(packet, sender);
        }

        private static FieldInfo packetIdField = AccessTools.Field(typeof(Packet_Client), "Id");
        private static FieldInfo customPacketField = AccessTools.Field(typeof(Packet_Client), "CustomPacket");
        private static FieldInfo channelIdField = AccessTools.Field(typeof(Packet_CustomPacket), "ChannelId");

        private bool ProcessInBackground(Packet_Client packet, IServerPlayer sender)
        {
            var id = (int)packetIdField.GetValue(packet);
            if (id != _customPacketId) return false;

            var customPacket = (Packet_CustomPacket)customPacketField.GetValue(packet);
            var channelId = (int)channelIdField.GetValue(customPacket);
            bool processed = OnProcessInBackground?.Invoke(channelId, customPacket, sender) ?? false;
            return processed;
        }

        private static FieldInfo FromSocketListenerField = AccessTools.Field(typeof(ConnectedClient), "FromSocketListener");
        private static FieldInfo SocketField = AccessTools.Field(typeof(ConnectedClient), "socket");
        private static FieldInfo PlayerField = AccessTools.Field(typeof(ConnectedClient), "Player");

        private IServerPlayer ResolveServerPlayer(NetConnection connection)
        {
            try
            {
                foreach (ConnectedClient connectedClient in ((ServerMain)api.World).Clients.Values.ToList())
                {
                    var server = (NetServer)FromSocketListenerField.GetValue(connectedClient);
                    var serverConnection = (NetConnection)SocketField.GetValue(connectedClient);
                    if (server is TcpNetServer && serverConnection.EqualsConnection(connection))
                        return (IServerPlayer)PlayerField.GetValue(connectedClient);
                }
            }
            catch (Exception e)
            {
                // ServerMain.Clients is a regular Dictionary that can get modified from the main thread, causing "Collection was modified" exception
                // It is a public object with no mechanisms for synchronization so there is nothing we can really do from our side to prevent this.
                // Luckily, this collection doesn't get modified often so just retrying should be safe enough.
                // - Dmitry221060, 23.11.2023
                if (e is InvalidOperationException) return ResolveServerPlayer(connection);
                Logger.server.Warning($"Unable to resolve IServerPlayer from NetConnection, packet will be dropped:\n{e}");
            }
            return null;
        }

        public void Dispose()
        {
            _packetProcessingCTS?.Cancel();
            _packetProcessingCTS?.Dispose();
            _packetProcessingCTS = null;
            OnProcessInBackground = null;
        }
    }
}
