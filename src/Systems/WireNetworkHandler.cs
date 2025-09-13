using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.Systems;
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
            ServerChannel.BroadcastPacket(packet);
        }

        public static WireNetwork AddNewNetwork(WireNode wireNode)
        {
            long networkId = 1;
            if (Networks.Count > 0)
            {
                networkId = Networks.Keys.Max() + 1;
            }

            var network = new WireNetwork { networkID = networkId };
            AddNetwork(network);
            network.AddNode(wireNode);

            // Propagation sur tous les noeuds connectés (utile si wireNode a déjà des connexions)
            PropagateNetworkUIDToConnectedNodes(wireNode, network);

            return network;
        }

        public static void AddNetwork(WireNetwork network)
        {
            if (network == null) return;
            if (Networks.ContainsKey(network.networkID)) return;
            Networks.Add(network.networkID, network);
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

        public static WireNetwork GetNetwork(WireNode node)
        {
            if (node == null) return null;
            return Networks.Values.FirstOrDefault(nw => nw.Nodes.Contains(node));
        }

        /// <summary>
        /// Met à jour récursivement le NetworkUID de tous les noeuds connectés à partir d’un noeud de départ
        /// </summary>
        public static void PropagateNetworkUIDToConnectedNodes(WireNode startNode, WireNetwork network)
        {
            if (startNode == null || network == null) return;

            var visited = new HashSet<WireNode>();
            var queue = new Queue<WireNode>();

            queue.Enqueue(startNode);
            visited.Add(startNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current.NetworkUID != network.networkID)
                {
                    current.NetworkUID = network.networkID;
                    current.MarkForUpdate();
                }

                foreach (var conn in current.GetConnections())
                {
                    var other = conn.GetOtherNode(current);
                    if (other != null && !visited.Contains(other))
                    {
                        visited.Add(other);
                        queue.Enqueue(other);
                    }
                }
            }
        }

    }
}
