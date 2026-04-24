using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public static class WireNetworkHandler
    {
        private static IClientNetworkChannel ClientChannel;
        private static IServerNetworkChannel ServerChannel;

        public static Dictionary<long, WireNetwork> Networks = new Dictionary<long, WireNetwork>();
        private static readonly Dictionary<long, string> PersistedCustomNames = new Dictionary<long, string>();

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
            long networkId = 1;
            if (Networks.Count > 0)
            {
                networkId = Networks.Keys.Max() + 1;
            }

            var network = new WireNetwork { networkID = networkId };
            AddNetwork(network);
            network.AddNode(wireNode);

            wireNode.NetworkUID = networkId;
            wireNode.MarkForUpdate();

            // Notify the node that it created a new network (for INetworkRoot)
            wireNode.OnNetworkCreated(networkId);

            // Propagation to all connected nodes (useful if wireNode already has connections)
            PropagateNetworkUIDToConnectedNodes(wireNode, network);

            return network;
        }

        public static void AddNetwork(WireNetwork network)
        {
            if (network == null) return;
            if (Networks.ContainsKey(network.networkID)) return;
            if (PersistedCustomNames.TryGetValue(network.networkID, out string customName))
            {
                network.SetCustomName(customName);
            }
            Networks.Add(network.networkID, network);
            network.RebuildTypedState();
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
        /// Recursively updates the NetworkUID of all nodes connected from a starting node
        /// </summary>
        public static void PropagateNetworkUIDToConnectedNodes(BEWireNode startNode, WireNetwork network)
        {
            if (startNode == null || network == null) return;

            var visited = new HashSet<BEWireNode>();
            var queue = new Queue<BEWireNode>();

            queue.Enqueue(startNode);
            visited.Add(startNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current.NetworkUID != network.networkID)
                {
                    current.NetworkUID = network.networkID;
                    network.AddNode(current);
                    current.MarkForUpdate();
                }

                foreach (var conn in current.GetConnections())
                {
                    var other = conn.GetOtherNode(current);

                    if (other != null && !visited.Contains(other))
                    {
                        visited.Add(other);
                        queue.Enqueue(other);
                        other.MarkForUpdate();
                    }
                }
            }

            network.RebuildTypedState();
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
            var visited = new HashSet<BEWireNode>();
            var queue = new Queue<BEWireNode>();

            queue.Enqueue(startNode);
            visited.Add(startNode);

            while (queue.Count > 0)
            {
                int levelCount = queue.Count;
                var levelCandidates = new List<BlockEntitySwitchboard>();

                for (int i = 0; i < levelCount; i++)
                {
                    var current = queue.Dequeue();
                    if (current is BlockEntitySwitchboard switchboard)
                    {
                        levelCandidates.Add(switchboard);
                    }

                    foreach (var connection in current.GetConnections())
                    {
                        var other = connection.GetOtherNode(current);
                        if (other != null && visited.Add(other))
                        {
                            queue.Enqueue(other);
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
            int radioCount = prospectiveComponent.Count(n => GetNodeKind(n) == WireNodeKind.Radio);
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

            if (!hasSwitchboard)
            {
                // No switchboard:
                // - Telegraph networks: unlimited
                // - Telephone networks: max 2 endpoints
                // - Radio networks: max 1 endpoint
                if (telephoneCount > 2)
                {
                    denialLangKey = "Wire.ConnectionDenied.NetworkCapacity";
                    denialArgs = new object[] { GetKindDisplayName(WireNetworkKind.Telephone), 2 };
                    return false;
                }

                if (radioCount > 1)
                {
                    denialLangKey = "Wire.ConnectionDenied.NetworkCapacity";
                    denialArgs = new object[] { GetKindDisplayName(WireNetworkKind.Radio), 1 };
                    return false;
                }

                return true;
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
                    return GetNodeKind(endpoint) == WireNodeKind.Radio;
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
            bool usedByLoadedNetwork = Networks.Values.Any(network =>
                network != null &&
                network.networkID != exceptNetworkId &&
                !string.IsNullOrWhiteSpace(network.CustomName) &&
                string.Equals(network.CustomName, normalized, StringComparison.OrdinalIgnoreCase));
            if (usedByLoadedNetwork)
            {
                return true;
            }

            return PersistedCustomNames.Any(entry =>
                entry.Key != exceptNetworkId &&
                !string.IsNullOrWhiteSpace(entry.Value) &&
                string.Equals(entry.Value, normalized, StringComparison.OrdinalIgnoreCase));
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
                PersistedCustomNames[networkId] = "";
                return true;
            }

            if (IsNetworkNameTaken(networkId, normalized))
            {
                failureLangKey = "Switchboard.Settings.NameAlreadyUsed";
                return false;
            }

            network.SetCustomName(normalized);
            PersistedCustomNames[networkId] = normalized;
            return true;
        }

        public static string GetDisplayName(long networkId)
        {
            var network = GetNetwork(networkId);
            if (network == null)
            {
                if (PersistedCustomNames.TryGetValue(networkId, out string persistedName) && !string.IsNullOrWhiteSpace(persistedName))
                {
                    return persistedName;
                }
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

            string normalized = (customName ?? "").Trim();
            PersistedCustomNames[networkId] = normalized;

            var network = GetNetwork(networkId);
            if (network != null)
            {
                network.SetCustomName(normalized);
            }
        }

        public static string GetPersistedNetworkName(long networkId)
        {
            if (networkId == 0)
            {
                return "";
            }

            if (PersistedCustomNames.TryGetValue(networkId, out string persistedName))
            {
                return persistedName ?? "";
            }

            return "";
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
            if (startNode == null || output.Contains(startNode))
                return;

            var queue = new Queue<BEWireNode>();
            queue.Enqueue(startNode);
            output.Add(startNode);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var connection in node.GetConnections())
                {
                    var other = connection.GetOtherNode(node);
                    if (other != null && output.Add(other))
                    {
                        queue.Enqueue(other);
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
