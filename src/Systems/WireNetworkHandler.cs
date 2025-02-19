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
            var networkId = Networks.Count + 1;
            network.networkID = networkId;
            network.AddNode(wireNode);
            Networks.Add(networkId, network);
            return network;
        }
        public static void AddNetwork(WireNetwork network)
        {
            if (Networks.ContainsValue(network))
                return;
            if (network.networkID == 0)
                network.networkID = Networks.Keys.Last() + 1;

            Networks.Add(network.networkID, network);
        }

        public static void RemoveNetwork(WireNetwork network)
        {
            if (Networks.ContainsValue(network))
                Networks.Remove(network.networkID);
        }

        public static WireNetwork GetNetwork(long networkID)
        {
            return Networks[networkID];
        }

        public static WireNetwork GetNetwork(WireNode node)
        {
            return Networks.Values.FirstOrDefault(network => network.Nodes.Contains(node));
        }

        public static WireNetwork MergeNetworks(List<WireNetwork> networksToMerge)
        {
            WireNetwork newNetwork = new WireNetwork();
            foreach (WireNetwork network in networksToMerge)
            {
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
