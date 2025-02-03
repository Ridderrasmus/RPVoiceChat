using RPVoiceChat.GameContent.Blocks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace RPVoiceChat.src.Systems
{
    public class WireNetwork
    {
        public long networkID;
        public List<WireNode> Nodes = new List<WireNode>();

        // New connections list
        public List<WireConnection> Connections = new List<WireConnection>();

        public event Action<WireNode, string> OnRecievedSignal;

        public WireNetwork() { }

        public void AddNode(WireNode node)
        {
            // Prevent duplicates
            if (!Nodes.Any(n => n.Pos == node.Pos))
            {
                Nodes.Add(node);
                node.MarkDirty(true);
            }
        }

        public void RemoveNode(WireNode node)
        {
            if (Nodes.Remove(node))
            {
                // Remove any connections involving this node
                Connections.RemoveAll(c => c.InvolvesNode(node));
                node.NetworkUID = 0;
                node.MarkDirty(true);
            }
        }

        // Add or remove connections in the network
        public void AddConnection(WireConnection connection)
        {
            if (!Connections.Contains(connection))
            {
                Connections.Add(connection);
            }
        }
        public void RemoveConnection(WireConnection connection)
        {
            Connections.Remove(connection);
        }

        public void SendSignal(WireNode sender, string message)
        {
            OnRecievedSignal?.Invoke(sender, message);
        }
    }
}
