using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Blocks;
using RPVoiceChat.src.Systems;
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
            return Networks.Values.FirstOrDefault(network => network.nodes.Contains(node));
        }

        public static WireNetwork MergeNetworks(List<WireNetwork> networksToMerge)
        {
            WireNetwork newNetwork = new WireNetwork();
            foreach (WireNetwork network in networksToMerge)
            {
                foreach (WireNode node in network.nodes)
                {
                    newNetwork.AddNode(node);
                }

                RemoveNetwork(network);
            }

            AddNetwork(newNetwork);
            return newNetwork;
        }
    }

    public class WireNetwork
    {
        public long networkID;
        public List<WireNode> nodes = new List<WireNode>();
        public event Action<WireNode, string> OnRecievedSignal;

        public WireNetwork()
        {
        }

        public void AddNode(WireNode node)
        {
            nodes.Add(node);
            node.NetworkUID = networkID;
        }

        public void RemoveNode(WireNode node)
        {
            nodes.Remove(node);
            node.NetworkUID = 0;
        }

        public void SendSignal(WireNode sender, string message)
        {
            OnRecievedSignal?.Invoke(sender, message);
        }
    }
}
