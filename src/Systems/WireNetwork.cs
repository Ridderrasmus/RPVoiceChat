using RPVoiceChat.Blocks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace RPVoiceChat.src.Systems
{
    public class WireNetwork
    {
        public long networkID;
        public List<WireNode> Nodes = new List<WireNode>();
        public event Action<WireNode, string> OnRecievedSignal;

        public WireNetwork()
        {
        }

        public void AddNode(WireNode node)
        {
            Nodes.Add(node);
            node.NetworkUID = networkID;
        }

        public void RemoveNode(WireNode node)
        {
            Nodes.Remove(node);
            node.NetworkUID = 0;
        }

        public void SendSignal(WireNode sender, string message)
        {
            OnRecievedSignal?.Invoke(sender, message);
        }
    }
}
