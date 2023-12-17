using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Blocks;

namespace RPVoiceChat.Systems
{
    public static class WireNetworkHandler
    {
        public static List<WireNetwork> networks = new List<WireNetwork>();

        public static WireNetwork AddNewNetwork(WireNode wireNode)
        {
            WireNetwork network = new WireNetwork();
            network.networkID = networks.Count + 1;
            network.AddNode(wireNode);
            networks.Add(network);
            return network;
        }
        public static void AddNetwork(WireNetwork network)
        {
            if (networks.Contains(network))
                return;
            networks.Add(network);
        }

        public static void RemoveNetwork(WireNetwork network)
        {
            if (networks.Contains(network))
                networks.Remove(network);
        }

        public static WireNetwork GetNetwork(int networkID)
        {
            return networks.FirstOrDefault(network => network.networkID == networkID);
        }

        public static WireNetwork GetNetwork(WireNode node)
        {
            return networks.FirstOrDefault(network => network.nodes.Contains(node));
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
        public int networkID;
        public List<WireNode> nodes = new List<WireNode>();
        public event Action<WireNode, string> OnRecievedSignal;

        public WireNetwork()
        {
            OnRecievedSignal += WireNetwork_OnRecievedSignal; ;
        }

        private void WireNetwork_OnRecievedSignal(WireNode node, string message)
        {
            foreach (WireNode wireNode in nodes)
            {
                if (wireNode == node)
                    continue;
                wireNode.OnRecievedSignal(message);
            }
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
