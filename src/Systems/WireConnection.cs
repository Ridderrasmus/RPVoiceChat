using System;
using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Systems
{
    public class WireConnection : IEquatable<WireConnection>
    {
        public IWireConnectable Node1 { get; }
        public IWireConnectable Node2 { get; }

        /// <summary>
        /// Endpoints copied at construction so wires can still be drawn when a neighbour chunk
        /// is unloaded (live Node references may be stale or null).
        /// </summary>
        public BlockPos BlockPos1 { get; }
        public BlockPos BlockPos2 { get; }

        public WireConnection(IWireConnectable node1, IWireConnectable node2)
        {
            Node1 = node1;
            Node2 = node2;
            BlockPos1 = node1?.Position?.Copy();
            BlockPos2 = node2?.Position?.Copy();
        }

        public BEWireNode GetOtherNode(IWireConnectable from)
        {
            if (from == Node1) return Node2 as BEWireNode;
            if (from == Node2) return Node1 as BEWireNode;
            return null;
        }

        /// <summary>
        /// The other endpoint block position for a node at <paramref name="fromPos"/>.
        /// </summary>
        public BlockPos GetOtherBlockPos(BlockPos fromPos)
        {
            if (fromPos == null || BlockPos1 == null || BlockPos2 == null)
                return null;
            if (fromPos.Equals(BlockPos1)) return BlockPos2;
            if (fromPos.Equals(BlockPos2)) return BlockPos1;
            return null;
        }

        public bool Equals(WireConnection other)
        {
            if (other == null) return false;

            if (BlockPos1 == null || BlockPos2 == null || other.BlockPos1 == null || other.BlockPos2 == null)
                return false;

            bool matchDirect = BlockPos1.Equals(other.BlockPos1) && BlockPos2.Equals(other.BlockPos2);
            bool matchInverse = BlockPos1.Equals(other.BlockPos2) && BlockPos2.Equals(other.BlockPos1);

            return matchDirect || matchInverse;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WireConnection);
        }

        public override int GetHashCode()
        {
            int hash1 = BlockPos1?.GetHashCode() ?? 0;
            int hash2 = BlockPos2?.GetHashCode() ?? 0;
            return hash1 ^ hash2;
        }
    }
}