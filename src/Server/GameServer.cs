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
    public partial class GameServer : IDisposable
    {
        private ICoreServerAPI api;
        private List<INetworkServer> _initialTransports;
        private List<INetworkServer> activeServers = new List<INetworkServer>();
        private IServerNetworkChannel handshakeChannel;
        private IServerNetworkChannel voiceBanChannel;
        private IServerNetworkChannel voiceGroupChannel;
        private Dictionary<string, INetworkServer> serverByTransportID = new Dictionary<string, INetworkServer>();
        private ConnectionRequest connectionRequest;
        private VoiceBanManager voiceBanManager;
        private VoiceGroupManager voiceGroupManager;

        // Stored players and their associated listeners
        private long listenerUpdateTickListener = 0;
        private readonly ConcurrentDictionary<string, INetworkServer> preferredTransports = new();
        private readonly ConcurrentDictionary<string, bool> timedVoiceClients = new();
        private volatile Dictionary<string, string[]> groupRecipientsByPlayer = new();
        private sealed class RoutingSnapshot
        {
            public Grid Grid = Grid.Empty;
            public Dictionary<string, MegaphoneInfo> Megaphones = new();
            public bool OthersHearSpectators;
            public readonly ConcurrentDictionary<(string Uid, int Range, bool Global), GridPlayer[]> Listeners = new();
            public readonly ConcurrentDictionary<(int Dimension, double X, double Y, double Z, int Range), GridPlayer[]> Emissions = new();
        }
        private volatile RoutingSnapshot routingSnapshot = new();
        private readonly ConcurrentDictionary<string, bool> devicesVoiceFeedbackByPlayer = new ConcurrentDictionary<string, bool>();
        private readonly IReadOnlyList<IVoiceRouteProvider> voiceRouteProviders;
        private readonly IReadOnlyList<IVoiceRecipientExpander> voiceRecipientExpanders;
        private System.Func<AudioPacket, bool> tryConsumeProgramMicAudio;

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
            voiceGroupManager = new VoiceGroupManager(sapi);
            PublishGroupRecipients(voiceGroupManager.BuildStateEntries());
            handshakeChannel = sapi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionRequest>()
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionInfo>(FinalizeHandshake);
            voiceBanChannel = sapi.Network
                .RegisterChannel("RPVoiceBan")
                .RegisterMessageType<VoiceBanStatusPacket>();
            voiceGroupChannel = sapi.Network
                .RegisterChannel("RPVoiceGroups")
                .RegisterMessageType<VoiceGroupStatePacket>()
                .RegisterMessageType<VoiceGroupActionPacket>()
                .RegisterMessageType<VoiceGroupUiStatePacket>()
                .RegisterMessageType<VoiceGroupActionResultPacket>()
                .SetMessageHandler<VoiceGroupActionPacket>(OnVoiceGroupAction);
            listenerUpdateTickListener = sapi.Event.RegisterGameTickListener(RebuildVoiceRoutingSnapshot, 500);
        }

        private void RebuildVoiceRoutingSnapshot(float gameTick)
        {
            if (voiceGroupManager.ExpireInvitations()) NotifyAllPlayersVoiceGroupsUpdated();
            var snapshot = new RoutingSnapshot
            {
                Grid = Grid.Build(api, ServerConfigManager.GridCellSizeBlocks),
                OthersHearSpectators = WorldConfig.GetBool("others-hear-spectators", true)
            };
            foreach (var player in snapshot.Grid.Players)
                snapshot.Megaphones[player.PlayerUID] = GetPlayerMegaphoneInfo(player.Player);
            routingSnapshot = snapshot;
        }

        private static GridPlayer[] CollectListeners(RoutingSnapshot snapshot, GridPlayer speaker, int range, bool global)
        {
            var candidates = new List<GridPlayer>();
            if (global) candidates.AddRange(snapshot.Grid.Players);
            else snapshot.Grid.CollectNear(speaker.Dimension, speaker.X, speaker.Z, range + 10, candidates);
            double distanceSquared = (double)(range + 10) * (range + 10);
            return candidates.Where(candidate => candidate.PlayerUID != speaker.PlayerUID
                && (snapshot.OthersHearSpectators || !speaker.IsSpectator || candidate.IsSpectator)
                && (global || (candidate.Dimension == speaker.Dimension && SquareDistance(speaker, candidate) <= distanceSquared))).ToArray();
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
            SendVoiceGroupsStateToPlayer(player);
            NotifyVoiceGroupUiPlayers();
            // Notify all other players if this player is banned
            if (voiceBanManager.IsPlayerBanned(player.PlayerUID))
            {
                NotifyAllPlayersBanStatus(player.PlayerUID, true);
            }
        }

        public void PlayerLeft(IServerPlayer player)
        {
            preferredTransports.TryRemove(player.PlayerUID, out _);
            timedVoiceClients.TryRemove(player.PlayerUID, out _);
            lastGroupRequest.Remove(player.PlayerUID);
            api.Event.EnqueueMainThreadTask(NotifyVoiceGroupUiPlayers, "rpvoicechat:groupPlayerLeft");
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
            if (packet == null || string.IsNullOrEmpty(packet.PlayerId) || !timedVoiceClients.ContainsKey(packet.PlayerId)) return;
            packet.GroupDelivery = false;
            if (!routingSnapshot.Grid.TryGetPlayer(packet.PlayerId, out var effectState)) return;
            packet.DrunkStrength = effectState.DrunkStrength;
            packet.TemporalStrength = effectState.TemporalStrength;
            packet.HasVoiceEffectState = true;
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

            RoutingSnapshot snapshot = routingSnapshot;
            if (!snapshot.Grid.TryGetPlayer(packet.PlayerId, out var speaker)) return;
            snapshot.Megaphones.TryGetValue(packet.PlayerId, out var megaphone);
            var outgoing = CloneAudioPacket(packet);
            outgoing.VoiceLevel = Enum.IsDefined(typeof(VoiceLevel), packet.VoiceLevel) ? packet.VoiceLevel : VoiceLevel.Talking;
            bool amplifierRequested = packet.TransmissionRangeBlocks > 0 || packet.IsGlobalBroadcast;
            bool global = amplifierRequested && megaphone.HasEnhancedMegaphone;
            int range = amplifierRequested && megaphone.HasMegaphone
                ? ServerConfigManager.MegaphoneAudibleDistance : WorldConfig.GetInt(outgoing.VoiceLevel);
            range = Math.Clamp(range, 0, 100000);
            outgoing.TransmissionRangeBlocks = range;
            outgoing.EffectiveRange = range;
            outgoing.IsGlobalBroadcast = global;
            outgoing.IgnoreDistanceReduction = amplifierRequested && megaphone.HasMegaphone && packet.IgnoreDistanceReduction;
            outgoing.WallThicknessOverride = amplifierRequested && megaphone.HasMegaphone ? packet.WallThicknessOverride : -1;
            outgoing.HasSourcePositionOverride = false;
            outgoing.SourceDimension = speaker.Dimension;
            outgoing.GroupDelivery = false;

            var listeners = snapshot.Listeners.GetOrAdd((packet.PlayerId, range, global),
                _ => CollectListeners(snapshot, speaker, range, global));
            var spatialRecipients = new HashSet<string>(listeners.Select(player => player.PlayerUID));
            var groupRecipients = new HashSet<string>();
            if (IsVoiceGroupsEnabled() && groupRecipientsByPlayer.TryGetValue(packet.PlayerId, out var members))
            {
                foreach (string uid in members)
                {
                    if (uid == speaker.PlayerUID || !snapshot.Grid.TryGetPlayer(uid, out var member)) continue;
                    if (!snapshot.OthersHearSpectators && speaker.IsSpectator && !member.IsSpectator) continue;
                    // A group member within the audible range keeps positional playback; distant members get an explicit flat delivery.
                    if (!global && (member.Dimension != speaker.Dimension || SquareDistance(speaker, member) > (double)range * range))
                    {
                        spatialRecipients.Remove(uid);
                        groupRecipients.Add(uid);
                    }
                }
            }
            var spatialPacket = new PreparedNetworkPacket(outgoing);
            foreach (string uid in spatialRecipients) SendPacket(spatialPacket, uid);
            if (groupRecipients.Count > 0)
            {
                var groupPacket = CloneAudioPacket(outgoing);
                groupPacket.GroupDelivery = true;
                var prepared = new PreparedNetworkPacket(groupPacket);
                foreach (string uid in groupRecipients) SendPacket(prepared, uid);
            }
        }

        internal void SendProgramAudio(AudioPacket packet)
        {
            if (packet != null && TryResolveVoiceRoutes(packet.PlayerId, out var routes))
                SendRoutedVoiceAudio(packet, routes);
        }

        private void SendRoutedVoiceAudio(AudioPacket packet, IReadOnlyList<VoiceRoute> routes)
        {
            if (routes == null || routes.Count == 0) return;

            RoutingSnapshot snapshot = routingSnapshot;
            Grid grid = snapshot.Grid;

            Dictionary<string, RoutedVoiceRecipient> recipients = tlsRoutedRecipients ??= new Dictionary<string, RoutedVoiceRecipient>(64);

            recipients.Clear();

            for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                VoiceRoute route = routes[routeIndex];
                if (!route.IsAcousticEmission || route.EmissionPos == null || route.RangeBlocks <= 0) continue;

                var key = (route.Dimension, route.EmissionPos.X, route.EmissionPos.Y, route.EmissionPos.Z, route.RangeBlocks);
                var emissionListeners = snapshot.Emissions.GetOrAdd(key, _ =>
                {
                    var near = new List<GridPlayer>();
                    grid.CollectNear(route.Dimension, route.EmissionPos.X, route.EmissionPos.Z, route.RangeBlocks, near);
                    return near.Where(candidate => candidate.Dimension == route.Dimension
                        && SquareDistance(candidate.X, candidate.Y, candidate.Z, route.EmissionPos) <= (double)route.RangeBlocks * route.RangeBlocks).ToArray();
                });
                foreach (var candidate in emissionListeners)
                    TryAccumulateRoutedRecipient(packet, route, candidate, recipients);

            }

            for (int expanderIndex = 0; expanderIndex < voiceRecipientExpanders.Count; expanderIndex++)
            {
                voiceRecipientExpanders[expanderIndex].ExpandRoutedRecipients(packet, routes, recipients);
            }

            var variants = new Dictionary<(int Dimension, double X, double Y, double Z, int Range), PreparedNetworkPacket>();
            foreach (RoutedVoiceRecipient recipient in recipients.Values)
            {
                var route = recipient.Route;
                var key = (route.Dimension, route.EmissionPos.X, route.EmissionPos.Y, route.EmissionPos.Z, route.RangeBlocks);
                if (!variants.TryGetValue(key, out var prepared))
                {
                    var routedPacket = CloneAudioPacket(packet);
                    routedPacket.TransmissionRangeBlocks = route.RangeBlocks;
                    routedPacket.HasSourcePositionOverride = true;
                    routedPacket.SourcePosX = route.EmissionPos.X;
                    routedPacket.SourcePosY = route.EmissionPos.Y;
                    routedPacket.SourcePosZ = route.EmissionPos.Z;
                    routedPacket.SourceDimension = route.Dimension;
                    routedPacket.GroupDelivery = false;
                    routedPacket.IsGlobalBroadcast = false;
                    routedPacket.IgnoreDistanceReduction = false;
                    prepared = new PreparedNetworkPacket(routedPacket);
                    variants.Add(key, prepared);
                }
                SendPacket(prepared, recipient.PlayerUID);
            }

            recipients.Clear();
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
                CaptureSampleTime = src.CaptureSampleTime,
                SampleCount = src.SampleCount,
                CaptureSession = src.CaptureSession,
                DrunkStrength = src.DrunkStrength,
                TemporalStrength = src.TemporalStrength,
                HasVoiceEffectState = src.HasVoiceEffectState,
                GroupDelivery = src.GroupDelivery,
                SourceDimension = src.SourceDimension,
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

            if (playerConnection.VoiceTimingVersion < 1)
            {
                player.SendMessage(0, "Voice chat requires the matching updated RPVoiceChat build (timed audio playback).", EnumChatType.Notification);
                return;
            }
            timedVoiceClients[player.PlayerUID] = true;
            INetworkServer selected = serverByTransportID[playerTransport];
            preferredTransports.AddOrUpdate(player.PlayerUID, selected,
                (_, current) => activeServers.IndexOf(selected) < activeServers.IndexOf(current) ? selected : current);
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

        private void SendPacket(PreparedNetworkPacket packet, string playerId)
        {
            if (!timedVoiceClients.ContainsKey(playerId)) return;
            preferredTransports.TryGetValue(playerId, out var preferred);
            if (preferred != null)
            {
                try { if (preferred.SendPacket(packet, playerId)) return; }
                catch (Exception e) { Logger.server.VerboseDebug($"Preferred voice transport failed: {e.Message}"); }
                preferredTransports.TryRemove(playerId, out _);
            }
            foreach (var server in activeServers)
            {
                if (server == preferred) continue;
                try
                {
                    bool success = server.SendPacket(packet, playerId);
                    if (success) { preferredTransports[playerId] = server; return; }
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
            connectionRequest = new ConnectionRequest(serverConnectionInfos) { VoiceTimingVersion = 1 };

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

        public void NotifyAllPlayersVoiceGroupsUpdated()
        {
            NotifyVoiceGroupUiPlayers();
            var packet = IsVoiceGroupsEnabled()
                ? new VoiceGroupStatePacket(voiceGroupManager.BuildStateEntries())
                : new VoiceGroupStatePacket(new List<VoiceGroupStateEntry>());
            PublishGroupRecipients(packet.Groups);

            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (player.ConnectionState == EnumClientState.Playing)
                {
                    voiceGroupChannel.SendPacket(packet, player);
                }
            }
        }

        private void PublishGroupRecipients(IEnumerable<VoiceGroupStateEntry> groups)
        {
            var membership = new Dictionary<string, string[]>();
            foreach (var group in groups)
            {
                var members = group.Members.ToArray();
                foreach (string uid in members) membership[uid] = members;
            }
            groupRecipientsByPlayer = membership;
        }

        private void SendVoiceGroupsStateToPlayer(IServerPlayer player)
        {
            if (player.ConnectionState != EnumClientState.Playing)
            {
                return;
            }

            var packet = IsVoiceGroupsEnabled()
                ? new VoiceGroupStatePacket(voiceGroupManager.BuildStateEntries())
                : new VoiceGroupStatePacket(new List<VoiceGroupStateEntry>());
            voiceGroupChannel.SendPacket(packet, player);
        }

        private bool IsVoiceGroupsEnabled()
        {
            return ServerConfigManager.VoiceGroupsEnabled;
        }

        public VoiceBanManager GetVoiceBanManager()
        {
            return voiceBanManager;
        }

        public VoiceGroupManager GetVoiceGroupManager()
        {
            return voiceGroupManager;
        }

        /// <summary>
        /// Information about megaphone items held by the player
        /// </summary>
        private struct MegaphoneInfo
        {
            public bool HasMegaphone;
            public bool HasEnhancedMegaphone;
        }

        private void TryAccumulateRoutedRecipient(AudioPacket packet, VoiceRoute route, GridPlayer candidate, Dictionary<string, RoutedVoiceRecipient> recipients)
        {
            if (candidate.Dimension != route.Dimension) return;

            if (packet.PlayerId == candidate.PlayerUID)
            {
                // RF replay at the handheld talkie must not win over receiver/speaker output.
                if (!string.IsNullOrEmpty(route.RadioFrequency)
                    && SquareDistance(candidate.X, candidate.Y, candidate.Z, route.EmissionPos) <= 4.0)
                {
                    return;
                }

                bool allowEmitterFeedback = devicesVoiceFeedbackByPlayer.TryGetValue(candidate.PlayerUID, out bool devicesVoiceFeedback) && devicesVoiceFeedback;
                if (!allowEmitterFeedback) return;
            }

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

                voiceGroupManager?.Dispose();
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
