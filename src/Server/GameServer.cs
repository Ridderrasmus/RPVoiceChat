using RPVoiceChat.Config;
using RPVoiceChat.Networking;
using RPVoiceChat.Util;
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
        private IServerNetworkChannel voiceBanChannel;
        private Dictionary<string, INetworkServer> serverByTransportID = new Dictionary<string, INetworkServer>();
        private ConnectionRequest connectionRequest;
        private VoiceBanManager voiceBanManager;

        public GameServer(ICoreServerAPI sapi, List<INetworkServer> serverTransports)
        {
            api = sapi;
            _initialTransports = serverTransports;
            voiceBanManager = new VoiceBanManager(sapi);
            handshakeChannel = sapi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionRequest>()
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionInfo>(FinalizeHandshake);
            voiceBanChannel = sapi.Network
                .RegisterChannel("RPVoiceBan")
                .RegisterMessageType<VoiceBanStatusPacket>();
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
            // Send the ban status of all banned players to the new player
            SendAllBannedPlayersStatus(player);
            // Notify all other players if this player is banned
            if (voiceBanManager.IsPlayerBanned(player.PlayerUID))
            {
                NotifyAllPlayersBanStatus(player.PlayerUID, true);
            }
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

            // Check if the player is banned - don't send their audio to other players
            if (voiceBanManager.IsPlayerBanned(packet.PlayerId))
            {
                return;
            }

            // Security checks: Validate megaphone-related parameters on server side
            // This prevents clients from manipulating audio transmission without having the items
            var megaphoneInfo = GetPlayerMegaphoneInfo(transmittingPlayer);
            
            // Validate global broadcast - only allowed with enhanced megaphone
            bool isGlobalBroadcast = false;
            if (packet.IsGlobalBroadcast)
            {
                isGlobalBroadcast = megaphoneInfo.HasEnhancedMegaphone;
                if (!isGlobalBroadcast)
                {
                    // Player tried to use global broadcast without having the item - log and ignore
                    Logger.server.Warning($"Player {packet.PlayerId} attempted to use global broadcast without enhanced megaphone. Request denied.");
                    packet.IsGlobalBroadcast = false;
                }
            }

            // Validate ignoreDistanceReduction - only allowed with enhanced megaphone
            if (packet.IgnoreDistanceReduction && !megaphoneInfo.HasEnhancedMegaphone)
            {
                Logger.server.Warning($"Player {packet.PlayerId} attempted to ignore distance reduction without enhanced megaphone. Request denied.");
                packet.IgnoreDistanceReduction = false;
            }

            // Validate wallThicknessOverride - only allowed with enhanced megaphone
            if (packet.WallThicknessOverride >= 0 && !megaphoneInfo.HasEnhancedMegaphone)
            {
                Logger.server.Warning($"Player {packet.PlayerId} attempted to override wall thickness without enhanced megaphone. Request denied.");
                packet.WallThicknessOverride = -1f;
            }

            // Validate transmission range - should match configured megaphone range if using megaphone
            // If player has a megaphone, validate the transmission range
            if (megaphoneInfo.HasMegaphone || megaphoneInfo.HasEnhancedMegaphone)
            {
                int maxAllowedRange = megaphoneInfo.HasEnhancedMegaphone 
                    ? int.MaxValue // Enhanced megaphone has no range limit (global broadcast)
                    : ServerConfigManager.MegaphoneAudibleDistance;
                
                if (packet.TransmissionRangeBlocks > maxAllowedRange && !megaphoneInfo.HasEnhancedMegaphone)
                {
                    Logger.server.Warning($"Player {packet.PlayerId} attempted to use transmission range {packet.TransmissionRangeBlocks} which exceeds megaphone limit {maxAllowedRange}. Request denied.");
                    packet.TransmissionRangeBlocks = maxAllowedRange;
                }
            }
            else if (packet.TransmissionRangeBlocks > 0)
            {
                // Player doesn't have a megaphone but is trying to use custom transmission range
                // This could be legitimate (from other items) but we should validate it's reasonable
                int maxNormalRange = WorldConfig.GetInt(VoiceLevel.Shouting);
                if (packet.TransmissionRangeBlocks > maxNormalRange * 2)
                {
                    Logger.server.Warning($"Player {packet.PlayerId} attempted to use transmission range {packet.TransmissionRangeBlocks} without megaphone. Request denied.");
                    packet.TransmissionRangeBlocks = 0; // Reset to use voice level default
                }
            }

            // Calculate effective distance AFTER all validations to ensure consistency
            int effectiveDistance = packet.TransmissionRangeBlocks > 0
                ? packet.TransmissionRangeBlocks
                : WorldConfig.GetInt(packet.VoiceLevel);

            packet.EffectiveRange = effectiveDistance;

            float squareDistance = 0f;
            if (!isGlobalBroadcast)
            {
                squareDistance = effectiveDistance * effectiveDistance;
            }

            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (player == transmittingPlayer ||
                    player.Entity == null ||
                    player.ConnectionState != EnumClientState.Playing ||
                    (!WorldConfig.GetBool("others-hear-spectators", true) && transmittingIsSpectator && player.WorldData.CurrentGameMode != EnumGameMode.Spectator))
                    continue;

                // Skip distance calculation if it is a global broadcast
                if (!isGlobalBroadcast &&
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

        public void NotifyAllPlayersBanStatus(string playerUID, bool isBanned)
        {
            var packet = new VoiceBanStatusPacket(playerUID, isBanned);
            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (player.ConnectionState == EnumClientState.Playing)
                {
                    voiceBanChannel.SendPacket(packet, player);
                }
            }
        }

        private void SendAllBannedPlayersStatus(IServerPlayer player)
        {
            var bannedPlayers = voiceBanManager.GetBannedPlayers();
            foreach (var bannedPlayerUID in bannedPlayers)
            {
                var packet = new VoiceBanStatusPacket(bannedPlayerUID, true);
                voiceBanChannel.SendPacket(packet, player);
            }
        }

        public VoiceBanManager GetVoiceBanManager()
        {
            return voiceBanManager;
        }

        /// <summary>
        /// Information about megaphone items held by the player
        /// </summary>
        private struct MegaphoneInfo
        {
            public bool HasMegaphone;
            public bool HasEnhancedMegaphone;
        }

        /// <summary>
        /// Validates that the player has megaphone items in their hands
        /// This prevents clients from using megaphone features without actually having the items
        /// </summary>
        private MegaphoneInfo GetPlayerMegaphoneInfo(IPlayer player)
        {
            var info = new MegaphoneInfo();

            if (player?.Entity == null) return info;

            // Check active hand (right hand)
            var activeSlot = player.Entity.RightHandItemSlot;
            if (activeSlot?.Itemstack?.Item != null)
            {
                var itemCode = activeSlot.Itemstack.Item.Code?.ToString() ?? "";
                if (itemCode == "rpvoicechat:enhancedmegaphone")
                {
                    info.HasEnhancedMegaphone = true;
                    info.HasMegaphone = true;
                }
                else if (itemCode == "rpvoicechat:megaphone")
                {
                    info.HasMegaphone = true;
                }
            }

            // Check left hand
            var leftSlot = player.Entity.LeftHandItemSlot;
            if (leftSlot?.Itemstack?.Item != null)
            {
                var itemCode = leftSlot.Itemstack.Item.Code?.ToString() ?? "";
                if (itemCode == "rpvoicechat:enhancedmegaphone")
                {
                    info.HasEnhancedMegaphone = true;
                    info.HasMegaphone = true;
                }
                else if (itemCode == "rpvoicechat:megaphone")
                {
                    info.HasMegaphone = true;
                }
            }

            // Note: We don't check the entire inventory for performance reasons
            // The megaphone must be in hand to be used, which is consistent with game mechanics
            return info;
        }

        public void Dispose()
        {
            try
            {
                foreach (var server in activeServers)
                    server.Dispose();
            }
            catch (Exception e)
            {
                Logger.server.Warning($"Error disposing servers: {e.Message}");
            }
            finally
            {
                // Always unsubscribe from events, even if disposal fails
                try
                {
                    api.Event.PlayerNowPlaying -= PlayerJoined;
                    api.Event.PlayerDisconnect -= PlayerLeft;
                }
                catch (Exception e)
                {
                    Logger.server.Warning($"Error unsubscribing server events: {e.Message}");
                }
            }
        }
    }
}
