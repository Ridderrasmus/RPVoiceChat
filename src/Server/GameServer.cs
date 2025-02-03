using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RPVoiceChat.Server
{
    public class GameServer : IDisposable
    {
        private ICoreServerAPI api;
        private List<INetworkServer> _initialTransports;
        private List<INetworkServer> activeServers = new List<INetworkServer>();
        private IServerNetworkChannel handshakeChannel;
        private Dictionary<string, INetworkServer> serverByTransportID = new Dictionary<string, INetworkServer>();
        private ConnectionRequest connectionRequest;

        public GameServer(ICoreServerAPI sapi, List<INetworkServer> serverTransports)
        {
            api = sapi;
            _initialTransports = serverTransports;
            handshakeChannel = sapi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionRequest>()
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionInfo>(FinalizeHandshake);
        }

        public void Launch()
        {
            LaunchServers();
            if (activeServers.Count == 0) throw new Exception("Failed to launch any server");

            api.Event.PlayerNowPlaying += PlayerJoined;
            api.Event.PlayerDisconnect += PlayerLeft;
        }

        public void PlayerJoined(IServerPlayer player)
        {
            InitHandshake(player);
        }

        public void PlayerLeft(IServerPlayer player)
        {
            foreach (var server in activeServers)
            {
                if (server is not IExtendedNetworkServer extendedServer) continue;
                extendedServer.PlayerDisconnected(player.PlayerUID);
            }
        }

        public void SendAudioToAllClientsInRange(AudioPacket packet)
        {
            var transmittingPlayer = api.World.PlayerByUid(packet.PlayerId);
            bool transmittingIsSpectator = transmittingPlayer?.WorldData.CurrentGameMode == EnumGameMode.Spectator;
            int distance = WorldConfig.GetInt(packet.VoiceLevel);
            int squareDistance = distance * distance;

            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (player == transmittingPlayer ||
                    player.Entity == null ||
                    player.ConnectionState != EnumClientState.Playing ||
                    (!WorldConfig.GetBool("others-hear-spectators", true) && transmittingIsSpectator && player.WorldData.CurrentGameMode != EnumGameMode.Spectator) ||
                    transmittingPlayer.Entity.Pos.SquareDistanceTo(player.Entity.Pos.XYZ) > squareDistance)
                    continue;

                SendPacket(packet, player.PlayerUID);
            }
        }

        private void LaunchServers()
        {
            activeServers = new List<INetworkServer>();
            foreach (var transport in _initialTransports)
            {
                try
                {
                    LaunchServer(transport);
                    transport.AudioPacketReceived += SendAudioToAllClientsInRange;
                }
                catch (Exception e)
                {
                    Logger.server.Error($"Failed to launch {transport.GetTransportID()} server:\n{e}");
                    transport.Dispose();
                }
            }
        }

        private void LaunchServer(INetworkServer server)
        {
            var transportID = server.GetTransportID();
            Logger.server.Notification($"Launching {transportID} server");
            server.Launch();
            activeServers.Add(server);
            serverByTransportID.Add(transportID, server);
            Logger.server.Notification($"{transportID} server started");
        }

        private void InitHandshake(IServerPlayer player)
        {
            var connectionRequest = GetConnectionRequest();
            handshakeChannel.SendPacket(connectionRequest, player);
        }

        private void FinalizeHandshake(IServerPlayer player, ConnectionInfo playerConnection)
        {
            var playerTransport = playerConnection.Transport;
            if (!serverByTransportID.ContainsKey(playerTransport)) return;

            var extendedServer = serverByTransportID[playerTransport] as IExtendedNetworkServer;
            if (extendedServer == null) return;
            try
            {
                playerConnection.Address = NetworkUtils.ParseIP(player.IpAddress).MapToIPv4().ToString();
                extendedServer?.PlayerConnected(player.PlayerUID, playerConnection);
            }
            catch (Exception e)
            {
                Logger.server.Warning($"Server failed to establish connection with {player.PlayerUID}({player.PlayerName}) over " +
                    $"requested transport: {playerTransport}.\nServer will attempt to use other available transports to deliver " +
                    "packets to this client. Mismatch between server and client transports can result in unstable behavior!\n" +
                    $"Player address: {player.IpAddress}, Reason: {e}");
            }
        }

        private void SendPacket(NetworkPacket packet, string playerId)
        {
            foreach (var server in activeServers)
            {
                try
                {
                    bool success = server.SendPacket(packet, playerId);
                    if (success) return;
                }
                catch (Exception e)
                {
                    Logger.server.VerboseDebug($"Couldn't use {server.GetTransportID()} server to deliver a packet to {playerId}: {e.Message}");
                }
            }
            Logger.server.Error($"Failed to deliver a packet to {playerId}: All active servers refused to serve the player");
        }

        private ConnectionRequest GetConnectionRequest()
        {
            if (connectionRequest != null) return connectionRequest;

            var serverConnectionInfos = new List<ConnectionInfo>();
            foreach (var server in activeServers)
            {
                var connectionInfo = server.GetConnectionInfo();
                connectionInfo.Transport = server.GetTransportID();
                serverConnectionInfos.Add(connectionInfo);
            }
            connectionRequest = new ConnectionRequest(serverConnectionInfos);

            return connectionRequest;
        }

        public void Dispose()
        {
            foreach (var server in activeServers)
                server.Dispose();
        }
    }
}
