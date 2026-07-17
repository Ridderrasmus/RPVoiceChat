using RPVoiceChat.Config;
using RPVoiceChat.Networking;
using RPVoiceChat.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
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

        // Stored players and their associated listeners
        private long listenerUpdateTickListener = 0;
        private ConcurrentDictionary<string, HashSet<IPlayer>> playerListeners = new ConcurrentDictionary<string, HashSet<IPlayer>>();
        private readonly ConcurrentDictionary<string, bool> devicesVoiceFeedbackByPlayer = new ConcurrentDictionary<string, bool>();
        private readonly IReadOnlyList<IVoiceRouteProvider> voiceRouteProviders;
        private readonly IReadOnlyList<IVoiceRecipientExpander> voiceRecipientExpanders;
        private System.Func<AudioPacket, bool> tryConsumeProgramMicAudio;

        private volatile Grid voiceGrid = Grid.Empty;
        [ThreadStatic] private static List<GridPlayer> tlsGridCandidates;
        [ThreadStatic] private static Dictionary<string, RoutedVoiceRecipient> tlsRoutedRecipients;

        public GameServer(
            ICoreServerAPI sapi,
            List<INetworkServer> serverTransports,
            IEnumerable<IVoiceRouteProvider> voiceRouteProviders,
            IEnumerable<IVoiceRecipientExpander> voiceRecipientExpanders = null)
        {
            api = sapi;
            _initialTransports = serverTransports;
            this.voiceRouteProviders = voiceRouteProviders
                .Where(provider => provider != null)
                .ToList();
            this.voiceRecipientExpanders = voiceRecipientExpanders?
                .Where(expander => expander != null)
                .ToList() ?? new List<IVoiceRecipientExpander>();
            voiceBanManager = new VoiceBanManager(sapi);
            handshakeChannel = sapi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionRequest>()
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionInfo>(FinalizeHandshake);
            voiceBanChannel = sapi.Network
                .RegisterChannel("RPVoiceBan")
                .RegisterMessageType<VoiceBanStatusPacket>();
            listenerUpdateTickListener = sapi.Event.RegisterGameTickListener(RebuildVoiceRoutingSnapshot, 500);
        }

        private void RebuildVoiceRoutingSnapshot(float gameTick)
        {
            Grid newGrid = Grid.Build(api, ServerConfigManager.GridCellSizeBlocks);
            var newListeners = new ConcurrentDictionary<string, HashSet<IPlayer>>();

            bool othersHearSpectators = WorldConfig.GetBool("others-hear-spectators", true);
            var candidates = new List<GridPlayer>(64);
            var players = newGrid.Players;

            for (int i = 0; i < players.Count; i++)
            {
                GridPlayer transmittingPlayer = players[i];
                var megaphoneInfo = GetPlayerMegaphoneInfo(transmittingPlayer.Player);

                bool isGlobalBroadcast = megaphoneInfo.HasEnhancedMegaphone;
                int effectiveDistance = ResolveEffectiveListenerDistance(megaphoneInfo);
                double squareDistance = isGlobalBroadcast ? double.MaxValue : (double)effectiveDistance * effectiveDistance;

                var listeners = new HashSet<IPlayer>();

                if (isGlobalBroadcast)
                {
                    for (int candidateIndex = 0; candidateIndex < players.Count; candidateIndex++)
                    {
                        TryAddNormalVoiceListener
                        (
                            transmittingPlayer,
                            players[candidateIndex],
                            listeners,
                            isGlobalBroadcast,
                            squareDistance,
                            othersHearSpectators
                        );
                    }
                }
                else
                {
                    newGrid.CollectNear
                    (
                        transmittingPlayer.Dimension,
                        transmittingPlayer.X,
                        transmittingPlayer.Z,
                        effectiveDistance,
                        candidates
                    );

                    for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                    {
                        TryAddNormalVoiceListener
                        (
                            transmittingPlayer,
                            candidates[candidateIndex],
                            listeners,
                            isGlobalBroadcast,
                            squareDistance,
                            othersHearSpectators
                        );
                    }

                    candidates.Clear();
                }

                newListeners[transmittingPlayer.PlayerUID] = listeners;
            }

            voiceGrid = newGrid;
            playerListeners = newListeners;
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
            devicesVoiceFeedbackByPlayer.TryRemove(player.PlayerUID, out _);
            foreach (var server in activeServers)
            {
                if (server is not IExtendedNetworkServer extendedServer) continue;
                extendedServer.PlayerDisconnected(player.PlayerUID);
            }
        }

        public void SetProgramMicAudioSink(System.Func<AudioPacket, bool> sink)
        {
            tryConsumeProgramMicAudio = sink;
        }

        public void SendAudioToAllClientsInRange(AudioPacket packet)
        {
            // Check if the player is banned - don't send their audio to other players
            if (voiceBanManager.IsPlayerBanned(packet.PlayerId))
            {
                return;
            }

            if (tryConsumeProgramMicAudio?.Invoke(packet) == true)
            {
                return;
            }

            if (TryResolveVoiceRoutes(packet.PlayerId, out IReadOnlyList<VoiceRoute> routes))
            {
                SendRoutedVoiceAudio(packet, routes);
                return;
            }

            if (!playerListeners.TryGetValue(packet.PlayerId, out var recipients)) return;

            foreach (var recipient in recipients)
            {
                SendPacket(packet, recipient.PlayerUID);
            }
        }

        private void SendRoutedVoiceAudio(AudioPacket packet, IReadOnlyList<VoiceRoute> routes)
        {
            if (routes == null || routes.Count == 0) return;

            Grid grid = voiceGrid ?? Grid.Empty;
            if (grid.IsEmpty) grid = Grid.Build(api, ServerConfigManager.GridCellSizeBlocks);

            Dictionary<string, RoutedVoiceRecipient> recipients = tlsRoutedRecipients ??= new Dictionary<string, RoutedVoiceRecipient>(64);
            List<GridPlayer> candidates = tlsGridCandidates ??= new List<GridPlayer>(64);

            recipients.Clear();
            candidates.Clear();

            for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                VoiceRoute route = routes[routeIndex];
                if (route.EmissionPos == null || route.RangeBlocks <= 0) continue;

                grid.CollectNear(
                    route.Dimension,
                    route.EmissionPos.X,
                    route.EmissionPos.Z,
                    route.RangeBlocks,
                    candidates
                );

                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    TryAccumulateRoutedRecipient(packet, route, candidates[candidateIndex], recipients);
                }

                candidates.Clear();
            }

            for (int expanderIndex = 0; expanderIndex < voiceRecipientExpanders.Count; expanderIndex++)
            {
                voiceRecipientExpanders[expanderIndex].ExpandRoutedRecipients(packet, routes, recipients);
            }

            foreach (RoutedVoiceRecipient recipient in recipients.Values)
            {
                var routedPacket = CloneAudioPacket(packet);
                routedPacket.TransmissionRangeBlocks = recipient.Route.RangeBlocks;
                routedPacket.HasSourcePositionOverride = true;
                routedPacket.SourcePosX = recipient.Route.EmissionPos.X;
                routedPacket.SourcePosY = recipient.Route.EmissionPos.Y;
                routedPacket.SourcePosZ = recipient.Route.EmissionPos.Z;
                SendPacket(routedPacket, recipient.PlayerUID);
            }

            recipients.Clear();
            candidates.Clear();
        }

        private static AudioPacket CloneAudioPacket(AudioPacket src)
        {
            return new AudioPacket
            {
                PlayerId = src.PlayerId,
                AudioData = src.AudioData,
                Length = src.Length,
                VoiceLevel = src.VoiceLevel,
                Frequency = src.Frequency,
                Format = src.Format,
                SequenceNumber = src.SequenceNumber,
                Codec = src.Codec,
                TransmissionRangeBlocks = src.TransmissionRangeBlocks,
                EffectiveRange = src.EffectiveRange,
                IgnoreDistanceReduction = src.IgnoreDistanceReduction,
                WallThicknessOverride = src.WallThicknessOverride,
                IsGlobalBroadcast = src.IsGlobalBroadcast,
                HasSourcePositionOverride = src.HasSourcePositionOverride,
                SourcePosX = src.SourcePosX,
                SourcePosY = src.SourcePosY,
                SourcePosZ = src.SourcePosZ
            };
        }

        private bool TryResolveVoiceRoutes(string playerUid, out IReadOnlyList<VoiceRoute> routes)
        {
            routes = null;
            if (voiceRouteProviders.Count == 0)
            {
                return false;
            }

            foreach (var provider in voiceRouteProviders)
            {
                if (provider is IVoiceMultiRouteProvider multiRouteProvider &&
                    multiRouteProvider.TryGetRoutes(playerUid, out routes) &&
                    routes != null &&
                    routes.Count > 0)
                {
                    return true;
                }
            }

            foreach (var provider in voiceRouteProviders)
            {
                if (provider.TryGetRoute(playerUid, out Vec3d emissionPos, out int rangeBlocks) &&
                    emissionPos != null &&
                    rangeBlocks > 0)
                {
                    routes = new[] { new VoiceRoute(emissionPos, rangeBlocks) };
                    return true;
                }
            }

            return false;
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

            devicesVoiceFeedbackByPlayer[player.PlayerUID] = playerConnection.DevicesVoiceFeedback;

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

        private static int ResolveEffectiveListenerDistance(MegaphoneInfo megaphoneInfo)
        {
            if (megaphoneInfo.HasEnhancedMegaphone) { return int.MaxValue; }
            if (megaphoneInfo.HasMegaphone) { return ServerConfigManager.MegaphoneAudibleDistance + 10; }

            return WorldConfig.GetInt(VoiceLevel.Shouting) + 10;
        }

        private static void TryAddNormalVoiceListener
        (
            GridPlayer transmittingPlayer,
            GridPlayer candidate,
            HashSet<IPlayer> listeners,
            bool isGlobalBroadcast,
            double squareDistance,
            bool othersHearSpectators
        )
        {
            if (candidate.PlayerUID == transmittingPlayer.PlayerUID)                                        { return; }
            if (!othersHearSpectators && transmittingPlayer.IsSpectator && !candidate.IsSpectator)            { return; }
            if (!isGlobalBroadcast && SquareDistance(transmittingPlayer, candidate) > squareDistance)        { return; }
            listeners.Add(candidate.Player);
        }

        private void TryAccumulateRoutedRecipient(AudioPacket packet, VoiceRoute route, GridPlayer candidate, Dictionary<string, RoutedVoiceRecipient> recipients)
        {
            if (candidate.Dimension != route.Dimension) return;
            bool allowEmitterFeedback = devicesVoiceFeedbackByPlayer.TryGetValue(candidate.PlayerUID, out bool devicesVoiceFeedback) && devicesVoiceFeedback;
            if (packet.PlayerId == candidate.PlayerUID && !allowEmitterFeedback) return;

            double distanceSq = SquareDistance(candidate.X, candidate.Y, candidate.Z, route.EmissionPos);
            TrySetRoutedRecipient(candidate.PlayerUID, route, distanceSq, recipients);
        }

        private static void TrySetRoutedRecipient(string playerUid, VoiceRoute route, double distanceSq, Dictionary<string, RoutedVoiceRecipient> recipients)
        {
            double maxDistanceSq = (double)route.RangeBlocks * route.RangeBlocks;
            if (distanceSq > maxDistanceSq) { return; }
            if (recipients.TryGetValue(playerUid, out RoutedVoiceRecipient existing) && existing.DistanceSq <= distanceSq) { return; }

            recipients[playerUid] = new RoutedVoiceRecipient(playerUid, route, distanceSq);
        }

        private static double SquareDistance(GridPlayer a, GridPlayer b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        private static double SquareDistance(double x, double y, double z, Vec3d other)
        {
            double dx = x - other.X;
            double dy = y - other.Y;
            double dz = z - other.Z;
            return dx * dx + dy * dy + dz * dz;
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
                    api.Event.UnregisterGameTickListener(listenerUpdateTickListener);
                }
                catch (Exception e)
                {
                    Logger.server.Warning($"Error unsubscribing server events: {e.Message}");
                }
            }
        }
    }
}

