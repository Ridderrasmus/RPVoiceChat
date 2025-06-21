using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using RPVoiceChat.VoiceGroups.Manager;
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
        private VoiceGroupManagerServer _voiceGroupManager;

        public GameServer(ICoreServerAPI sapi, List<INetworkServer> serverTransports, VoiceGroupManagerServer voiceGroupManager)
        {
            api = sapi;
            _initialTransports = serverTransports;
            handshakeChannel = sapi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionRequest>()
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionInfo>(FinalizeHandshake);

            _voiceGroupManager = voiceGroupManager;
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

            foreach (IServerPlayer receivingPlayer in api.World.AllOnlinePlayers)
            {
                if (BaseLimitation(transmittingPlayer, receivingPlayer) ||
                    SpectatorHearsSpectatorsLimitation(receivingPlayer, transmittingIsSpectator) ||
                    !(IsWithinRangeLimitation(transmittingPlayer, receivingPlayer, squareDistance) || IsInGroupLimitation(transmittingPlayer, receivingPlayer))
                    )
                    continue;

                SendPacket(packet, receivingPlayer.PlayerUID);
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


        /// <summary>
        /// Determines whether a message transmission is allowed based on the relationship  between the transmitting
        /// player and the receiving player, as well as the receiving player's state.
        /// </summary>
        /// <param name="transmittingPlayer">The player attempting to transmit the message.</param>
        /// <param name="receivingPlayer">The player intended to receive the message.</param>
        /// <returns><see langword="true"/> if the receiving player is the same as the transmitting player,  the receiving
        /// player's entity is null, or the receiving player is not in the playing state;  otherwise, <see
        /// langword="false"/>.</returns>
        private static bool BaseLimitation(IPlayer transmittingPlayer, IServerPlayer receivingPlayer)
        {
            return receivingPlayer == transmittingPlayer ||
                    receivingPlayer.Entity == null ||
                    receivingPlayer.ConnectionState != EnumClientState.Playing;
        }

        /// <summary>
        /// Determines whether the distance between the transmitting player and the receiving player is within the
        /// specified square distance limit.
        /// </summary>
        /// <param name="transmittingPlayer">The player initiating the transmission.</param>
        /// <param name="receivingPlayer">The player receiving the transmission.</param>
        /// <param name="squareDistance">The maximum allowable distance, in squared units, between the transmitting player and the receiving player.</param>
        /// <returns><see langword="true"/> if the squared distance between the transmitting player and the receiving player is
        /// less than or equal to <paramref name="squareDistance"/>; otherwise, <see langword="false"/>.</returns>
        private static bool IsWithinRangeLimitation(IPlayer transmittingPlayer, IServerPlayer receivingPlayer, int squareDistance)
        {
            return transmittingPlayer.Entity.Pos.SquareDistanceTo(receivingPlayer.Entity.Pos.XYZ) <= squareDistance;
        }

        /// <summary>
        /// Determines whether a spectator's communication is limited based on the receiving player's game mode and the
        /// server configuration.
        /// </summary>
        /// <remarks>This method checks the server configuration setting <c>"others-hear-spectators"</c>
        /// and the receiving player's game mode to determine if a spectator's communication should be limited.
        /// Spectator communication is restricted when the configuration disables it, the transmitting player is a
        /// spectator, and the receiving player is not in spectator mode.</remarks>
        /// <param name="receivingPlayer">The player receiving the communication.</param>
        /// <param name="transmittingIsSpectator">A value indicating whether the transmitting player is a spectator.</param>
        /// <returns><see langword="true"/> if the spectator's communication is restricted and cannot be heard by the receiving
        /// player; otherwise, <see langword="false"/>.</returns>
        private static bool SpectatorHearsSpectatorsLimitation(IServerPlayer receivingPlayer, bool transmittingIsSpectator)
        {
            return (!WorldConfig.GetBool("others-hear-spectators", true) && transmittingIsSpectator && receivingPlayer.WorldData.CurrentGameMode != EnumGameMode.Spectator);
        }

        /// <summary>
        /// Determines whether the transmitting player and the receiving player are subject to group voice chat
        /// limitations.
        /// </summary>
        /// <param name="transmittingPlayer">The player initiating the voice transmission.</param>
        /// <param name="receivingPlayer">The player receiving the voice transmission.</param>
        /// <returns><see langword="true"/> if group voice chat is enabled and both players are in the same group;  otherwise,
        /// <see langword="false"/>.</returns>
        private bool IsInGroupLimitation(IPlayer transmittingPlayer, IServerPlayer receivingPlayer)
        {
            return (WorldConfig.GetBool("allow-group-voicechat", false) && _voiceGroupManager.InSameGroup(transmittingPlayer.PlayerUID, receivingPlayer.PlayerUID));
        }
    }
}
