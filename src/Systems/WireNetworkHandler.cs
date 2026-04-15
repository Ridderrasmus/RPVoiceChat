using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public static class WireNetworkHandler
    {
        private static IClientNetworkChannel ClientChannel;
        private static IServerNetworkChannel ServerChannel;

        public static Dictionary<long, WireNetwork> Networks = new Dictionary<long, WireNetwork>();

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

        public static bool CanConnectNodes(BEWireNode node1, BEWireNode node2, out string denialLangKey, out object[] denialArgs)
        {
            denialLangKey = null;
            denialArgs = Array.Empty<object>();

            if (node1 == null || node2 == null)
                return false;

            // Hybrid mode: if no switchboard in resulting component, allow baseline behavior.
            HashSet<BEWireNode> prospectiveComponent = GetProspectiveComponent(node1, node2);
            bool hasSwitchboard = prospectiveComponent.Any(n => GetNodeKind(n) == WireNodeKind.Switchboard);
            if (!hasSwitchboard)
                return true;

            int telegraphCount = prospectiveComponent.Count(n => GetNodeKind(n) == WireNodeKind.Telegraph);
            int telephoneCount = prospectiveComponent.Count(n => GetNodeKind(n) == WireNodeKind.Telephone);
            int wirelessCount = prospectiveComponent.Count(n => GetNodeKind(n) == WireNodeKind.Wireless);

            int activeKinds = 0;
            if (telegraphCount > 0) activeKinds++;
            if (telephoneCount > 0) activeKinds++;
            if (wirelessCount > 0) activeKinds++;

            if (activeKinds > 1)
            {
                denialLangKey = "Wire.ConnectionDenied.MixedTypes";
                return false;
            }

            WireNetworkKind targetKind = ResolveProspectiveKind(telegraphCount, telephoneCount, wirelessCount);
            WireNetworkRequirements requirements = WireNetworkTypeRules.GetRequirements(targetKind);
            int endpointCount = GetEndpointCountByKind(targetKind, telegraphCount, telephoneCount, wirelessCount);

            if (requirements.MaxEndpoints > 0 && endpointCount > requirements.MaxEndpoints)
            {
                denialLangKey = targetKind == WireNetworkKind.Telegraph
                    ? "Wire.ConnectionDenied.TelegraphCapacity"
                    : "Wire.ConnectionDenied.NetworkCapacity";
                denialArgs = targetKind == WireNetworkKind.Telegraph
                    ? new object[] { requirements.MaxEndpoints }
                    : new object[] { GetKindDisplayName(targetKind), requirements.MaxEndpoints };
                return false;
            }

            return true;
        }

        private static WireNetworkKind ResolveProspectiveKind(int telegraphCount, int telephoneCount, int wirelessCount)
        {
            if (telegraphCount > 0) return WireNetworkKind.Telegraph;
            if (telephoneCount > 0) return WireNetworkKind.Telephone;
            if (wirelessCount > 0) return WireNetworkKind.Wireless;
            return WireNetworkKind.None;
        }

        private static int GetEndpointCountByKind(WireNetworkKind kind, int telegraphCount, int telephoneCount, int wirelessCount)
        {
            switch (kind)
            {
                case WireNetworkKind.Telegraph:
                    return telegraphCount;
                case WireNetworkKind.Telephone:
                    return telephoneCount;
                case WireNetworkKind.Wireless:
                    return wirelessCount;
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
                case WireNetworkKind.Wireless:
                    return "wireless";
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
