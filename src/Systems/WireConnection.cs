using System;
using RPVoiceChat.GameContent.Blocks;

namespace RPVoiceChat.GameContent.Systems
{
    public class WireConnection : IEquatable<WireConnection>
    {
        public IWireConnectable Node1 { get; set; }
        public IWireConnectable Node2 { get; set; }

        public WireConnection(IWireConnectable node1)
        {
            Node1 = node1;
        }

        public WireConnection(IWireConnectable node1, IWireConnectable node2)
        {
            Node1 = node1;
            Node2 = node2;
        }

        /// <summary>
        /// Returns the other node in the connection.
        /// </summary>
        public WireNode GetOtherNode(IWireConnectable from)
        {
            if (from == Node1) return Node2 as WireNode;
            if (from == Node2) return Node1 as WireNode;
            return null;
        }

        /// <summary>
        /// Compares connections based on their nodes' positions, regardless of order.
        /// </summary>
        public bool Equals(WireConnection other)
        {
            if (other == null) return false;

            bool matchDirect = (Node1?.Position.Equals(other.Node1?.Position) ?? false) && (Node2?.Position.Equals(other.Node2?.Position) ?? false);
            bool matchInverse = (Node1?.Position.Equals(other.Node2?.Position) ?? false) && (Node2?.Position.Equals(other.Node1?.Position) ?? false);

            return matchDirect || matchInverse;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WireConnection);
        }

        /// <summary>
        /// Generates a hash code independent of node order, based on node positions.
        /// </summary>
        public override int GetHashCode()
        {
            int hash1 = Node1?.Position.GetHashCode() ?? 0;
            int hash2 = Node2?.Position.GetHashCode() ?? 0;

            // XOR makes the order irrelevant
            return hash1 ^ hash2;
        }
    }
}
