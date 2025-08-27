using System;
using RPVoiceChat.GameContent.Blocks;

namespace RPVoiceChat.GameContent.Systems
{
    public class WireConnection : IEquatable<WireConnection>
    {
        public WireNode Node1 { get; set; }
        public WireNode Node2 { get; set; }

        public WireConnection(WireNode node1)
        {
            Node1 = node1;
        }

        public WireConnection(WireNode node1, WireNode node2)
        {
            Node1 = node1;
            Node2 = node2;
        }

        /// <summary>
        /// Returns the other node in the connection.
        /// </summary>
        public WireNode GetOtherNode(WireNode from)
        {
            if (from == Node1) return Node2;
            if (from == Node2) return Node1;
            return null;
        }

        /// <summary>
        /// Compares connections based on their nodes' positions, regardless of order.
        /// </summary>
        public bool Equals(WireConnection other)
        {
            if (other == null) return false;

            bool matchDirect = (Node1?.Pos.Equals(other.Node1?.Pos) ?? false) && (Node2?.Pos.Equals(other.Node2?.Pos) ?? false);
            bool matchInverse = (Node1?.Pos.Equals(other.Node2?.Pos) ?? false) && (Node2?.Pos.Equals(other.Node1?.Pos) ?? false);

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
            int hash1 = Node1?.Pos.GetHashCode() ?? 0;
            int hash2 = Node2?.Pos.GetHashCode() ?? 0;

            // XOR makes the order irrelevant
            return hash1 ^ hash2;
        }
    }
}
