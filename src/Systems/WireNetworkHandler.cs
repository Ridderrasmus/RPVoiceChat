using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.Blocks;
using RPVoiceChat.GameContent.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public static class WireNetworkHandler
    {
        private static IClientNetworkChannel ClientChannel;
        private static IServerNetworkChannel ServerChannel;

        public static Dictionary<long, WireNetwork> Networks = new Dictionary<long, WireNetwork>() { { 0, new WireNetwork() { networkID = 0 } } };
        public static EventHandler<WireNetworkMessage> ClientSideMessageReceived;
        public static string NetworkChannel = "rpvc:wire-network";

        public static void RegisterClientside(ICoreClientAPI api)
        {
            ClientChannel = api.Network.RegisterChannel(NetworkChannel)
                .RegisterMessageType(typeof(WireNetworkMessage))
                .SetMessageHandler<WireNetworkMessage>(OnReceivedMessage_Client);
        }

        public static void RegisterServerside(ICoreServerAPI api)
        {
            ServerChannel = api.Network.RegisterChannel(NetworkChannel)
                .RegisterMessageType(typeof(WireNetworkMessage))
                .SetMessageHandler<WireNetworkMessage>(OnReceivedMessage_Server);
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
            long networkId = Networks.Keys.Max();
            networkId = networkId == 0 ? 1 : networkId + 1; // 0 is reserverd for no network

            WireNetwork network = new WireNetwork
            {
                networkID = networkId
            };
            network.AddNode(wireNode);
            Networks.Add(networkId, network);
            return network;
        }

        public static void AddNetwork(WireNetwork network)
        {
            if (network == null) return;

            if (Networks.ContainsValue(network))
                return;

            if (network.networkID == 0)
                network.networkID = Networks.Keys.Max() + 1;

            if (!Networks.ContainsKey(network.networkID))
            {
                Networks.Add(network.networkID, network);
            }
        }

        public static void RemoveNetwork(WireNetwork network)
        {
            if (network == null) return;

            if (Networks.ContainsValue(network))
            {
                Networks.Remove(network.networkID);
            }
        }

        public static WireNetwork GetNetwork(long networkID)
        {
            if (networkID == 0)
                return null; // 0 = no network

            return Networks.TryGetValue(networkID, out var network) ? network : null;
        }

        public static WireNetwork GetNetwork(WireNode node)
        {
            if (node == null)
                return null;

            return Networks.Values.FirstOrDefault(network => network.Nodes.Contains(node));
        }

        public static WireNetwork MergeNetworks(List<WireNetwork> networksToMerge)
        {
            if (networksToMerge == null || networksToMerge.Count == 0)
                return null;

            WireNetwork newNetwork = new WireNetwork();
            foreach (WireNetwork network in networksToMerge)
            {
                if (network == null) continue;

                foreach (WireNode node in network.Nodes)
                {
                    newNetwork.AddNode(node);
                }
                RemoveNetwork(network);
            }

            AddNetwork(newNetwork);
            return newNetwork;
        }
    }
}
