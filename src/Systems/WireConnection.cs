using System;

namespace RPVoiceChat.GameContent.Systems
{
    public class WireConnection : IEquatable<WireConnection>
    {
        public IWireConnectable Node1 { get; }
        public IWireConnectable Node2 { get; }

        public WireConnection(IWireConnectable node1, IWireConnectable node2)
        {
            Node1 = node1;
            Node2 = node2;
        }

        public WireNode GetOtherNode(IWireConnectable from)
        {
            if (from == Node1) return Node2 as WireNode;
            if (from == Node2) return Node1 as WireNode;
            return null;
        }

        public bool Equals(WireConnection other)
        {
            if (other == null) return false;

            if (Node1 == null || Node2 == null || other.Node1 == null || other.Node2 == null)
                return false;

            bool matchDirect = Node1.Position.Equals(other.Node1.Position) && Node2.Position.Equals(other.Node2.Position);
            bool matchInverse = Node1.Position.Equals(other.Node2.Position) && Node2.Position.Equals(other.Node1.Position);

            return matchDirect || matchInverse;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WireConnection);
        }

        public override int GetHashCode()
        {
            int hash1 = Node1?.Position.GetHashCode() ?? 0;
            int hash2 = Node2?.Position.GetHashCode() ?? 0;
            // XOR order-independant
            return hash1 ^ hash2;
        }
    }
}