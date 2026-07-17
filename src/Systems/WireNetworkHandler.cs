using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace RPVoiceChat.Systems
{
    public static class WireNetworkHandler
    {
        private static IClientNetworkChannel ClientChannel;
        private static IServerNetworkChannel ServerChannel;

        public static Dictionary<long, WireNetwork> Networks = new Dictionary<long, WireNetwork>();
        private static long nextNetworkId;

        public static EventHandler<WireNetworkMessage> ClientSideMessageReceived;
        public static string NetworkChannel = "rpvc:wire-network";

        public static void RegisterServerside(ICoreServerAPI api)
        {
            ServerChannel = api.Network.RegisterChannel(NetworkChannel)
                .RegisterMessageType(typeof(WireNetworkMessage))
                .SetMessageHandler<WireNetworkMessage>(OnReceivedMessage_Server);
        }

        public static void RegisterClientside(ICoreClientAPI api)
        {
            ClientChannel = api.Network.RegisterChannel(NetworkChannel)
                .RegisterMessageType(typeof(WireNetworkMessage))
                .SetMessageHandler<WireNetworkMessage>(OnReceivedMessage_Client);
        }

        private static void OnReceivedMessage_Client(WireNetworkMessage packet)
        {
            ClientSideMessageReceived?.Invoke(null, packet);
        }

        private static void OnReceivedMessage_Server(IServerPlayer fromPlayer, WireNetworkMessage packet)
        {
            packet = ApplyRoutingOnServer(packet);
            ServerChannel.BroadcastPacket(packet);
        }

        private static WireNetworkMessage ApplyRoutingOnServer(WireNetworkMessage packet)
        {
            if (packet == null) return null;

            if (packet.RouteMode != WireRouteMode.NamedEndpoint || string.IsNullOrWhiteSpace(packet.TargetEndpointName))
            {
                packet.RouteMode = WireRouteMode.All;
                packet.TargetEndpointName = null;
                packet.TargetPos = null;
                return packet;
            }

            var target = ResolveTelegraphByName(packet.NetworkUID, packet.TargetEndpointName);
            if (target == null)
            {
                packet.RouteMode = WireRouteMode.All;
                packet.TargetEndpointName = null;
                packet.TargetPos = null;
                return packet;
            }

            packet.TargetPos = target.Pos.Copy();
            return packet;
        }

        public static WireNetwork AddNewNetwork(BEWireNode wireNode)
        {
            long networkId = AllocateNetworkId();

            var network = new WireNetwork { networkID = networkId };
            AddNetwork(network);
            network.JoinNode(wireNode);

            // Notify the node that it created a new network (for INetworkRoot)
            wireNode.OnNetworkCreated(networkId);

            // Propagation to all connected nodes (useful if wireNode already has connections)
            PropagateNetworkUIDToConnectedNodes(wireNode, network);

            return network;
        }

        private static long AllocateNetworkId()
        {
            long candidate = Math.Max(nextNetworkId, Networks.Count > 0 ? Networks.Keys.Max() : 0) + 1;
            while (Networks.ContainsKey(candidate))
            {
                candidate++;
            }

            nextNetworkId = candidate;
            return candidate;
        }

        public static void InitializeFromSave(byte[] networkBytes, byte[] nextIdBytes)
        {
            Networks.Clear();

            if (networkBytes == null || networkBytes.Length == 0)
            {
                return;
            }

            var savedNetworks = SerializerUtil.Deserialize<List<WireNetworkSaveData>>(networkBytes);
            if (savedNetworks == null)
            {
                return;
            }

            foreach (var savedNetwork in savedNetworks)
            {
                if (savedNetwork == null || savedNetwork.NetworkId == 0)
                {
                    continue;
                }

                var network = WireNetwork.FromSaveData(savedNetwork);
                Networks[network.networkID] = network;
            }

            if (nextIdBytes != null && nextIdBytes.Length > 0)
            {
                nextNetworkId = SerializerUtil.Deserialize<long>(nextIdBytes);
            }
            else if (Networks.Count > 0)
            {
                nextNetworkId = Networks.Keys.Max();
            }
        }

        /// <summary>
        /// Re-attaches block entities that initialized before <see cref="InitializeFromSave"/> ran.
        /// </summary>
        public static void RejoinLoadedNodes(ICoreServerAPI api)
        {
            var accessor = api?.World?.BlockAccessor;
            if (accessor == null)
            {
                return;
            }

            foreach (var network in Networks.Values)
            {
                if (network == null)
                {
                    continue;
                }

                foreach (var nodeRef in network.PersistedNodes)
                {
                    if (nodeRef?.Pos == null)
                    {
                        continue;
                    }

                    var node = accessor.GetBlockEntity(nodeRef.Pos) as BEWireNode;
                    if (node == null)
                    {
                        continue;
                    }

                    long authoritativeId = ResolveNetworkIdForPosition(nodeRef.Pos);
                    if (authoritativeId != 0 && node.NetworkUID != authoritativeId)
                    {
                        node.NetworkUID = authoritativeId;
                        node.MarkForUpdate();
                    }

                    if (node.NetworkUID != network.networkID)
                    {
                        continue;
                    }

                    network.JoinNode(node);
                }
            }
        }

        public static void PersistToSaveGame(ISaveGame saveGame)
        {
            if (saveGame == null)
            {
                return;
            }

            var payload = Networks.Values
                .Where(network => network != null && network.networkID != 0 && network.HasPersistedNodes)
                .Select(network => network.ToSaveData())
                .ToList();

            saveGame.StoreData(WireNetworkPersistence.NetworksDataKey, SerializerUtil.Serialize(payload));
            saveGame.StoreData(WireNetworkPersistence.NextNetworkIdDataKey, SerializerUtil.Serialize(nextNetworkId));
        }

        public static void AddNetwork(WireNetwork network)
        {
            if (network == null) return;
            if (Networks.ContainsKey(network.networkID)) return;
            Networks.Add(network.networkID, network);
            if (network.Nodes.Count > 0)
            {
                network.RebuildTypedState();
            }
        }

        public static void RemoveNetwork(WireNetwork network)
        {
            if (network == null) return;
            Networks.Remove(network.networkID);
        }

        public static WireNetwork GetNetwork(long networkID)
        {
            if (networkID == 0) return null;
            return Networks.TryGetValue(networkID, out var net) ? net : null;
        }

        public static WireNetwork GetNetwork(BEWireNode node)
        {
            if (node == null) return null;
            return Networks.Values.FirstOrDefault(nw => nw.Nodes.Contains(node));
        }

        /// <summary>
        /// Recursively updates the NetworkUID of all nodes connected from a starting node.
        /// Uses world topology so unloaded chunks do not break propagation.
        /// </summary>
        public static void PropagateNetworkUIDToConnectedNodes(BEWireNode startNode, WireNetwork network)
        {
            if (startNode?.Pos == null || network == null)
            {
                return;
            }

            var accessor = startNode.Api?.World?.BlockAccessor;
            var componentPositions = WireTopologyRegistry.GetConnectedComponent(startNode.Pos);

            foreach (var pos in componentPositions)
            {
                network.UpsertPersistedNodeFromPosition(pos, accessor);

                var node = accessor?.GetBlockEntity(pos) as BEWireNode;
                if (node == null)
                {
                    continue;
                }

                if (node.NetworkUID != network.networkID)
                {
                    node.NetworkUID = network.networkID;
                    network.JoinNode(node);
                    node.MarkForUpdate();
                }
            }

            network.RebuildTypedState();
        }

        public static long ResolveNetworkIdForPosition(BlockPos pos)
        {
            if (pos == null)
            {
                return 0;
            }

            foreach (var network in Networks.Values)
            {
                if (network?.PersistedNodes == null)
                {
                    continue;
                }

                if (network.PersistedNodes.Any(nodeRef => nodeRef.Pos != null && nodeRef.Pos.Equals(pos)))
                {
                    return network.networkID;
                }
            }

            return 0;
        }

        public static HashSet<BEWireNode> GetLoadedNodesAtPositions(IEnumerable<BlockPos> positions, IBlockAccessor accessor)
        {
            var result = new HashSet<BEWireNode>();
            if (positions == null || accessor == null)
            {
                return result;
            }

            foreach (var pos in positions)
            {
                if (pos == null)
                {
                    continue;
                }

                var node = accessor.GetBlockEntity(pos) as BEWireNode;
                if (node != null)
                {
                    result.Add(node);
                }
            }

            return result;
        }

        public static bool HasNetworkRootInPositions(IEnumerable<BlockPos> positions, IBlockAccessor accessor, IEnumerable<WireNodeRef> persistedNodes)
        {
            return CountNetworkRootsInPositions(positions, accessor, persistedNodes) > 0;
        }

        public static int CountNetworkRootsInPositions(IEnumerable<BlockPos> positions, IBlockAccessor accessor, IEnumerable<WireNodeRef> persistedNodes)
        {
            if (positions == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var pos in positions)
            {
                if (pos == null)
                {
                    continue;
                }

                if (accessor?.GetBlockEntity(pos) is INetworkRoot)
                {
                    count++;
                    continue;
                }

                WireNodeKind? kind = persistedNodes?
                    .FirstOrDefault(nodeRef => nodeRef.Pos != null && nodeRef.Pos.Equals(pos))
                    ?.Kind;

                if (kind == WireNodeKind.Telegraph || kind == WireNodeKind.Telephone)
                {
                    count++;
                }
            }

            return count;
        }

        public static void DetachPersistedPositions(WireNetwork network, IEnumerable<BlockPos> positions)
        {
            if (network == null || positions == null)
            {
                return;
            }

            foreach (var pos in positions)
            {
                network.RemovePersistedNode(pos);
            }
        }

        public static void RebuildNetworkState(long networkId)
        {
            var network = GetNetwork(networkId);
            network?.RebuildTypedState();
        }

        /// <summary>
        /// Server-only: copies switchboard routing capability from the authoritative in-memory network
        /// onto every telegraph in that network so clients receive it via normal BE sync (no stale client-side inference).
        /// </summary>
        public static void RefreshTelegraphRoutingSnapshot(long networkId)
        {
            var network = GetNetwork(networkId);
            if (network == null || network.Nodes.Count == 0)
            {
                return;
            }

            ICoreAPI api = network.Nodes[0].Api;
            if (api?.Side != EnumAppSide.Server)
            {
                return;
            }

            var activeEndpointOwners = ResolveOwnersForActiveEndpoints(network.Nodes);
            WireNetworkKind kindForPower = network.CurrentType == WireNetworkKind.None
                ? WireNetworkKind.Telegraph
                : network.CurrentType;

            foreach (var node in network.Nodes.ToArray())
            {
                if (node is BlockEntityTelegraph telegraph)
                {
                    bool managed = activeEndpointOwners.TryGetValue(telegraph, out BlockEntitySwitchboard owner) && owner != null;
                    bool overCapacity = IsOverCapacityForManagedComponent(network, kindForPower);
                    bool advanced = managed && !overCapacity && owner.HasSufficientPowerFor(kindForPower);
                    string disabledReason = overCapacity ? "Telegraph.Settings.DisabledCapacity" : "Telegraph.Settings.DisabledNoPower";
                    telegraph.ApplyServerRoutingFlags(managed, advanced, disabledReason);
                }
                else if (node is BlockEntityTelephone telephone)
                {
                    bool managed = activeEndpointOwners.TryGetValue(telephone, out BlockEntitySwitchboard owner) && owner != null;
                    bool overCapacity = IsOverCapacityForManagedComponent(network, WireNetworkKind.Telephone);
                    bool composeEnabled = managed && !overCapacity && owner.HasSufficientPowerFor(WireNetworkKind.Telephone);
                    string disabledReason = overCapacity ? "Telegraph.Settings.DisabledCapacity" : "Telegraph.Settings.DisabledNoPower";
                    telephone.ApplyServerComposeFlags(managed, composeEnabled, disabledReason);
                }
            }
        }

        private static Dictionary<BEWireNode, BlockEntitySwitchboard> ResolveOwnersForActiveEndpoints(IEnumerable<BEWireNode> nodes)
        {
            var nodeList = nodes?.Where(n => n != null).ToList() ?? new List<BEWireNode>();
            var owners = new Dictionary<BEWireNode, BlockEntitySwitchboard>();
            var switchboards = nodeList.OfType<BlockEntitySwitchboard>().ToList();
            var activeEndpoints = nodeList.Where(IsActiveEndpoint).ToList();

            if (switchboards.Count == 0)
            {
                foreach (var endpoint in activeEndpoints) owners[endpoint] = null;
                return owners;
            }

            foreach (var endpoint in activeEndpoints)
            {
                owners[endpoint] = FindNearestSwitchboardOwner(endpoint, switchboards);
            }

            return owners;
        }

        private static BlockEntitySwitchboard FindNearestSwitchboardOwner(BEWireNode startNode, List<BlockEntitySwitchboard> allSwitchboards)
        {
            if (startNode?.Pos == null)
            {
                return null;
            }

            var accessor = startNode.Api?.World?.BlockAccessor;
            var visited = new HashSet<BlockPos>();
            var queue = new Queue<BlockPos>();
            queue.Enqueue(startNode.Pos.Copy());
            visited.Add(startNode.Pos.Copy());

            while (queue.Count > 0)
            {
                int levelCount = queue.Count;
                var levelCandidates = new List<BlockEntitySwitchboard>();

                for (int i = 0; i < levelCount; i++)
                {
                    var currentPos = queue.Dequeue();
                    if (accessor?.GetBlockEntity(currentPos) is BlockEntitySwitchboard switchboard)
                    {
                        levelCandidates.Add(switchboard);
                    }

                    foreach (var neighborPos in WireTopologyRegistry.GetNeighborPositions(currentPos))
                    {
                        if (neighborPos == null)
                        {
                            continue;
                        }

                        var neighborCopy = neighborPos.Copy();
                        if (visited.Add(neighborCopy))
                        {
                            queue.Enqueue(neighborCopy);
                        }
                    }
                }

                if (levelCandidates.Count > 0)
                {
                    return levelCandidates
                        .OrderBy(sb => sb.Pos.X)
                        .ThenBy(sb => sb.Pos.Y)
                        .ThenBy(sb => sb.Pos.Z)
                        .First();
                }
            }

            return allSwitchboards
                .OrderBy(sb => sb.Pos.X)
                .ThenBy(sb => sb.Pos.Y)
                .ThenBy(sb => sb.Pos.Z)
                .FirstOrDefault();
        }

        private static bool IsActiveEndpoint(BEWireNode node)
        {
            return node?.IsActiveEndpoint ?? false;
        }

        public static BlockEntitySwitchboard ResolveOwnerSwitchboard(BEWireNode endpointNode)
        {
            if (endpointNode == null || !IsActiveEndpoint(endpointNode) || endpointNode.NetworkUID == 0)
            {
                return null;
            }

            var network = GetNetwork(endpointNode.NetworkUID);
            if (network == null)
            {
                return null;
            }

            var owners = ResolveOwnersForActiveEndpoints(network.Nodes);
            owners.TryGetValue(endpointNode, out BlockEntitySwitchboard owner);
            return owner;
        }

        public static string GetSubNetworkDisplayName(BEWireNode endpointNode)
        {
            var owner = ResolveOwnerSwitchboard(endpointNode);
            return owner?.GetNetworkCustomNameForEditor() ?? "";
        }

        public static string GetManagedRoutingDisabledReason(long networkId)
        {
            var network = GetNetwork(networkId);
            if (network == null || !network.IsManagedBySwitchboard)
            {
                return null;
            }

            WireNetworkKind kind = network.CurrentType == WireNetworkKind.None
                ? WireNetworkKind.Telegraph
                : network.CurrentType;

            if (IsOverCapacityForManagedComponent(network, kind))
            {
                return "Telegraph.Settings.DisabledCapacity";
            }

            return network.HasPoweredSwitchboard ? null : "Telegraph.Settings.DisabledNoPower";
        }

        public static bool CanConnectNodes(BEWireNode node1, BEWireNode node2, out string denialLangKey, out object[] denialArgs)
        {
            denialLangKey = null;
            denialArgs = Array.Empty<object>();

            if (node1 == null || node2 == null)
                return false;

            // Hybrid mode: if no switchboard in resulting component, allow baseline behavior.
            HashSet<BEWireNode> prospectiveComponent = GetProspectiveComponent(node1, node2);
            bool hasSwitchboard = prospectiveComponent.Any(n => GetNodeKind(n) == WireNodeKind.Switchboard);
            int telegraphCount = prospectiveComponent.Count(n => GetNodeKind(n) == WireNodeKind.Telegraph);
            int telephoneCount = prospectiveComponent.Count(n => GetNodeKind(n) == WireNodeKind.Telephone);
            int radioCount = prospectiveComponent.Count(n => WireNodeKindRules.IsRadioFamilyEndpoint(GetNodeKind(n)));
            int radioConsoleCount = prospectiveComponent.OfType<BlockEntityRadioSupervisionConsole>().Count();
            int speakerCount = prospectiveComponent.OfType<BlockEntitySpeaker>().Count();
            int telephoneHandsetCount = prospectiveComponent.OfType<BlockEntityTelephone>().Count();
            int activeKinds = 0;
            if (telegraphCount > 0) activeKinds++;
            if (telephoneCount > 0) activeKinds++;
            if (radioCount > 0) activeKinds++;

            // Defensive guard: dedicated network rules should prevent mixed endpoint families
            // in normal gameplay. Keep this to reject legacy/invalid graph states.
            if (activeKinds > 1)
            {
                denialLangKey = "Wire.ConnectionDenied.MixedTypes";
                return false;
            }

            if (node1 is BlockEntityRadioEmitter repeater1 && repeater1.IsRepeaterMode
                || node2 is BlockEntityRadioEmitter repeater2 && repeater2.IsRepeaterMode)
            {
                denialLangKey = "Wire.ConnectionDenied.RadioRepeaterNoWire";
                return false;
            }

            if (radioConsoleCount > 1)
            {
                denialLangKey = "Wire.ConnectionDenied.RadioSingleConsole";
                return false;
            }

            if (!hasSwitchboard)
            {
                // No switchboard:
                // - Telegraph networks: unlimited
                // - Telephone networks:
                //   - up to 2 handsets when there are no speakers (legacy telephone pairing)
                //   - up to 1 handset when speakers are present (PA-style branch)
                // - Radio networks: max 1 endpoint
                if (speakerCount > 0 && telephoneHandsetCount > 1)
                {
                    denialLangKey = "Wire.ConnectionDenied.SpeakerNetworkSingleTelephone";
                    return false;
                }

                if (speakerCount == 0 && telephoneCount > 2)
                {
                    denialLangKey = "Wire.ConnectionDenied.NetworkCapacity";
                    denialArgs = new object[] { GetKindDisplayName(WireNetworkKind.Telephone), 2 };
                    return false;
                }

                if (radioCount > 0 && radioCount > ServerConfigManager.RadioNetworkMaxEndpoints)
                {
                    denialLangKey = "Wire.ConnectionDenied.NetworkCapacity";
                    denialArgs = new object[] { GetKindDisplayName(WireNetworkKind.Radio), ServerConfigManager.RadioNetworkMaxEndpoints };
                    return false;
                }

                return true;
            }

            // Managed switchboard components must not contain speakers.
            if (speakerCount > 0)
            {
                denialLangKey = "Wire.ConnectionDenied.SpeakerWithSwitchboard";
                return false;
            }

            WireNetworkKind targetKind = ResolveProspectiveKind(telegraphCount, telephoneCount, radioCount);
            WireNetworkRequirements requirements = WireNetworkTypeRules.GetRequirements(targetKind);
            if (requirements.MaxEndpoints > 0)
            {
                int switchboardCount = prospectiveComponent.Count(n => GetNodeKind(n) == WireNodeKind.Switchboard);
                int endpointCount = GetEndpointCountByKind(targetKind, telegraphCount, telephoneCount, radioCount);
                int componentCapacity = requirements.MaxEndpoints * Math.Max(1, switchboardCount);
                if (endpointCount > componentCapacity)
                {
                    denialLangKey = targetKind == WireNetworkKind.Telegraph
                        ? "Wire.ConnectionDenied.TelegraphCapacity"
                        : "Wire.ConnectionDenied.NetworkCapacity";
                    denialArgs = targetKind == WireNetworkKind.Telegraph
                        ? new object[] { componentCapacity }
                        : new object[] { GetKindDisplayName(targetKind), componentCapacity };
                    return false;
                }
            }

            return true;
        }

        private static bool IsOverCapacityForManagedComponent(WireNetwork network, WireNetworkKind kind)
        {
            if (network == null || !network.IsManagedBySwitchboard || kind == WireNetworkKind.None)
            {
                return false;
            }

            WireNetworkRequirements requirements = WireNetworkTypeRules.GetRequirements(kind);
            if (requirements.MaxEndpoints <= 0)
            {
                return false;
            }

            int switchboardCount = network.Nodes.Count(n => GetNodeKind(n) == WireNodeKind.Switchboard);
            if (switchboardCount <= 0) return false;

            int endpointCount = kind switch
            {
                WireNetworkKind.Telegraph => network.TelegraphEndpointCount,
                WireNetworkKind.Telephone => network.TelephoneEndpointCount,
                WireNetworkKind.Radio => network.RadioEndpointCount,
                _ => 0
            };

            int capacity = requirements.MaxEndpoints * switchboardCount;
            return endpointCount > capacity;
        }

        private static bool IsEndpointOfKind(BEWireNode endpoint, WireNetworkKind kind)
        {
            switch (kind)
            {
                case WireNetworkKind.Telegraph:
                    return GetNodeKind(endpoint) == WireNodeKind.Telegraph;
                case WireNetworkKind.Telephone:
                    return GetNodeKind(endpoint) == WireNodeKind.Telephone;
                case WireNetworkKind.Radio:
                    return WireNodeKindRules.IsRadioFamilyEndpoint(GetNodeKind(endpoint));
                default:
                    return false;
            }
        }

        private static WireNetworkKind ResolveProspectiveKind(int telegraphCount, int telephoneCount, int radioCount)
        {
            if (telegraphCount > 0) return WireNetworkKind.Telegraph;
            if (telephoneCount > 0) return WireNetworkKind.Telephone;
            if (radioCount > 0) return WireNetworkKind.Radio;
            return WireNetworkKind.None;
        }

        private static int GetEndpointCountByKind(WireNetworkKind kind, int telegraphCount, int telephoneCount, int radioCount)
        {
            switch (kind)
            {
                case WireNetworkKind.Telegraph:
                    return telegraphCount;
                case WireNetworkKind.Telephone:
                    return telephoneCount;
                case WireNetworkKind.Radio:
                    return radioCount;
                default:
                    return 0;
            }
        }

        private static string GetKindDisplayName(WireNetworkKind kind)
        {
            switch (kind)
            {
                case WireNetworkKind.Telephone:
                    return "telephone";
                case WireNetworkKind.Radio:
                    return "radio";
                case WireNetworkKind.Telegraph:
                    return "telegraph";
                default:
                    return "network";
            }
        }

        public static BlockEntityTelegraph ResolveTelegraphByName(long networkUID, string endpointName)
        {
            if (networkUID == 0 || string.IsNullOrWhiteSpace(endpointName))
                return null;

            var network = GetNetwork(networkUID);
            if (network == null)
                return null;

            return network.Nodes
                .OfType<BlockEntityTelegraph>()
                .FirstOrDefault(t => string.Equals(t.CustomEndpointName, endpointName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsNetworkNameTaken(long exceptNetworkId, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            string normalized = candidate.Trim();
            return Networks.Values.Any(network =>
                network != null &&
                network.networkID != exceptNetworkId &&
                !string.IsNullOrWhiteSpace(network.CustomName) &&
                string.Equals(network.CustomName, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryRenameNetwork(long networkId, string candidate, out string failureLangKey)
        {
            failureLangKey = null;
            var network = GetNetwork(networkId);
            if (network == null)
            {
                failureLangKey = "Network.NoNetwork";
                return false;
            }

            string normalized = (candidate ?? "").Trim();
            if (normalized.Length == 0)
            {
                network.SetCustomName("");
                return true;
            }

            if (IsNetworkNameTaken(networkId, normalized))
            {
                failureLangKey = "Switchboard.Settings.NameAlreadyUsed";
                return false;
            }

            network.SetCustomName(normalized);
            return true;
        }

        public static string GetDisplayName(long networkId)
        {
            var network = GetNetwork(networkId);
            if (network == null)
            {
                return networkId.ToString();
            }

            if (!string.IsNullOrWhiteSpace(network.CustomName))
            {
                return network.CustomName;
            }

            return network.networkID.ToString();
        }

        public static void SetPersistedNetworkName(long networkId, string customName)
        {
            if (networkId == 0)
            {
                return;
            }

            var network = GetNetwork(networkId);
            network?.SetCustomName((customName ?? "").Trim());
        }

        public static string GetPersistedNetworkName(long networkId)
        {
            if (networkId == 0)
            {
                return "";
            }

            return GetNetwork(networkId)?.CustomName ?? "";
        }

        public static bool IsEndpointNameTaken(long networkUID, string candidate, BlockEntityTelegraph except = null)
        {
            if (networkUID == 0 || string.IsNullOrWhiteSpace(candidate))
                return false;

            var network = GetNetwork(networkUID);
            if (network == null)
                return false;

            return network.Nodes
                .OfType<BlockEntityTelegraph>()
                .Any(t => !ReferenceEquals(t, except) &&
                          string.Equals(t.CustomEndpointName, candidate, StringComparison.OrdinalIgnoreCase));
        }

        private static HashSet<BEWireNode> GetProspectiveComponent(BEWireNode node1, BEWireNode node2)
        {
            var result = new HashSet<BEWireNode>();
            AddReachable(node1, result);
            AddReachable(node2, result);
            return result;
        }

        /// <summary>
        /// Returns all nodes reachable from a start node through wire connections.
        /// Useful for endpoint queries that must traverse connectors/infrastructure.
        /// </summary>
        public static HashSet<BEWireNode> GetReachableNodes(BEWireNode startNode)
        {
            var result = new HashSet<BEWireNode>();
            AddReachable(startNode, result);
            return result;
        }

        private static void AddReachable(BEWireNode startNode, HashSet<BEWireNode> output)
        {
            if (startNode?.Pos == null || output.Contains(startNode))
            {
                return;
            }

            var accessor = startNode.Api?.World?.BlockAccessor;
            var visited = new HashSet<BlockPos>();
            var queue = new Queue<BlockPos>();
            var startPos = startNode.Pos.Copy();

            queue.Enqueue(startPos);
            visited.Add(startPos);
            output.Add(startNode);

            while (queue.Count > 0)
            {
                var currentPos = queue.Dequeue();
                foreach (var neighborPos in WireTopologyRegistry.GetNeighborPositions(currentPos))
                {
                    if (neighborPos == null)
                    {
                        continue;
                    }

                    var neighborCopy = neighborPos.Copy();
                    if (!visited.Add(neighborCopy))
                    {
                        continue;
                    }

                    queue.Enqueue(neighborCopy);

                    var neighborNode = accessor?.GetBlockEntity(neighborCopy) as BEWireNode;
                    if (neighborNode != null)
                    {
                        output.Add(neighborNode);
                    }
                }
            }
        }

        private static WireNodeKind GetNodeKind(BEWireNode node)
        {
            if (node is IWireTypedNode typedNode)
                return typedNode.WireNodeKind;
            return WireNodeKind.Infrastructure;
        }

    }
}
