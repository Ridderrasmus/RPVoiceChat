using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.GameContent.Blocks;
using RPVoiceChat.Systems;

namespace RPVoiceChat.GameContent.Systems
{
    public class WireNetwork
    {
        public long networkID;
        public List<WireNode> Nodes { get; private set; } = new List<WireNode>();
        public event Action<WireNode, string> OnReceivedSignal;

        public WireNetwork() { }

        public void AddNode(WireNode node)
        {
            if (node == null || Nodes.Contains(node))
                return;

            Nodes.Add(node);
            node.NetworkUID = networkID;
            node.MarkDirty(true);
        }

        public void RemoveNode(WireNode node)
        {
            if (node == null)
                return;

            Nodes.Remove(node);
            node.NetworkUID = 0;
            node.MarkDirty(true);

            if (Nodes.Count == 0)
            {
                WireNetworkHandler.RemoveNetwork(this);
            }
        }

        public void SendSignal(WireNode sender, string message)
        {
            OnReceivedSignal?.Invoke(sender, message);

            foreach (var node in Nodes)
            {
                if (node != sender)
                {
                    node.SendSignal(new WireNetworkMessage
                    {
                        NetworkUID = networkID,
                        SenderPos = sender.Pos,
                        Message = message
                    });
                }
            }
        }

        public void MergeFrom(WireNetwork otherNetwork)
        {
            if (otherNetwork == null || otherNetwork == this)
                return;

            foreach (var node in otherNetwork.Nodes.ToList())
            {
                AddNode(node);
            }

            WireNetworkHandler.RemoveNetwork(otherNetwork);
        }
    }
}
