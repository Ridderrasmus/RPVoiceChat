using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.Blocks;
using RPVoiceChat.src.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public static class WireNetworkHandler
    {
        private static IClientNetworkChannel ClientChannel;
        private static IServerNetworkChannel ServerChannel;

        public static Dictionary<long, WireNetwork> Networks 
            = new Dictionary<long, WireNetwork> { { 0, new WireNetwork() { networkID = 0 } } };

        public static EventHandler<WireNetworkMessage> ClientSideMessageReceived;
        public static string NetworkChannel = "rpvc:wire-network";

        public static void RegisterClientside(ICoreClientAPI api)
        {
            ClientChannel = api.Network.RegisterChannel(NetworkChannel)
                .RegisterMessageType(typeof(WireNetworkMessage))
                .SetMessageHandler<WireNetworkMessage>(OnRecievedMessage_Client);
        }

        public static void RegisterServerside(ICoreServerAPI api)
        {
            ServerChannel = api.Network.RegisterChannel(NetworkChannel)
                .RegisterMessageType(typeof(WireNetworkMessage))
                .SetMessageHandler<WireNetworkMessage>(OnRecievedMessage_Server);
        }

        private static void OnRecievedMessage_Client(WireNetworkMessage packet)
        {
            ClientSideMessageReceived?.Invoke(null, packet);
        }

        private static void OnRecievedMessage_Server(IServerPlayer fromPlayer, WireNetworkMessage packet)
        {
            ServerChannel.BroadcastPacket(packet);
        }

        public static WireNetwork AddNewNetwork(WireNode wireNode)
        {
            WireNetwork network = new WireNetwork();
            long newID = Networks.Keys.Max() + 1;
            network.networkID = newID;
            wireNode.NetworkUID = newID;

            network.AddNode(wireNode);
            Networks.Add(newID, network);

            wireNode.MarkDirty(true);
            return network;
        }

        public static void AddNetwork(WireNetwork network)
        {
            if (!Networks.ContainsKey(network.networkID))
            {
                if (network.networkID <= 0 || Networks.ContainsKey(network.networkID))
                {
                    long newID = Networks.Keys.Max() + 1;
                    network.networkID = newID;
                }
                Networks.Add(network.networkID, network);
            }
        }

        public static void RemoveNetwork(WireNetwork network)
        {
            if (network != null && Networks.ContainsKey(network.networkID))
            {
                Networks.Remove(network.networkID);
            }
        }

        public static WireNetwork? GetNetwork(long networkID)
        {
            if (Networks.TryGetValue(networkID, out var net))
            {
                return net;
            }
            return null;
        }

        public static WireNetwork GetNetwork(WireNode node)
        {
            return Networks.Values.FirstOrDefault(network => network.Nodes.Contains(node));
        }

        public static WireNetwork MergeNetworks(List<WireNetwork> networksToMerge)
        {
            if (networksToMerge == null || networksToMerge.Count == 0)
            {
                return new WireNetwork();
            }

            WireNetwork newNetwork = new WireNetwork
            {
                networkID = networksToMerge.FirstOrDefault(n => n.networkID > 0)?.networkID ?? 0
            };

            foreach (WireNetwork oldNet in networksToMerge.Where(n => n != null))
            {
                foreach (var node in oldNet.Nodes.ToList())
                {
                    newNetwork.AddNode(node);
                    node.NetworkUID = newNetwork.networkID; // Ensure all nodes have the correct NetworkUID
                }
                foreach (var conn in oldNet.Connections.ToList())
                {
                    newNetwork.AddConnection(conn);
                }
                RemoveNetwork(oldNet);
            }
            AddNetwork(newNetwork);

            return newNetwork;
        }

        public static void AttemptSplitNetwork(WireNetwork network)
        {
            if (network == null) return;

            var visited = new HashSet<WireNode>();
            var subNetworks = new List<WireNetwork>();

            foreach (var node in network.Nodes.ToList())
            {
                if (!visited.Contains(node))
                {
                    var newNet = new WireNetwork();
                    BFSVisit(node, visited, newNet, network);
                    subNetworks.Add(newNet);
                }
            }

            if (subNetworks.Count > 1)
            {
                RemoveNetwork(network);
                foreach (var subNet in subNetworks)
                {
                    // Give each subnetwork a new ID and update node IDs
                    long newID = Networks.Keys.Max() + 1;
                    subNet.networkID = newID;
                    foreach (var n in subNet.Nodes)
                    {
                        n.NetworkUID = newID;
                        n.MarkDirty(true);
                    }
                    AddNetwork(subNet);
                }
            }
            else if (subNetworks.Count == 1)
            {
                // Ensure the existing network's ID is used and nodes are updated
                var singleSubNet = subNetworks[0];
                foreach (var n in singleSubNet.Nodes)
                {
                    if (n.NetworkUID != network.networkID)
                    {
                        n.NetworkUID = network.networkID;
                        n.MarkDirty(true);
                    }
                }
            }
        }

        private static void BFSVisit(
            WireNode start,
            HashSet<WireNode> visited,
            WireNetwork subNet,
            WireNetwork original)
        {
            var queue = new Queue<WireNode>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                subNet.AddNode(current);

                foreach (var conn in original.Connections.Where(c => c.InvolvesNode(current)))
                {
                    subNet.AddConnection(conn);
                    var otherNode = (conn.Node1 == current) ? conn.Node2 : conn.Node1;
                    if (!visited.Contains(otherNode))
                    {
                        visited.Add(otherNode);
                        queue.Enqueue(otherNode);
                    }
                }
            }
        }
    }
}
