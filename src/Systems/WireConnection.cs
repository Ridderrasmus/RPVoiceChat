using RPVoiceChat.GameContent.Blocks;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.src.Systems
{
    public class WireConnection
    {
        public WireConnection(WireNode node1)
        {
            Node1 = node1;
        }

        public WireConnection(WireNode node1, WireNode node2)
        {
            Node1 = node1;
            Node2 = node2;
        }

        public WireNode Node1 { get; set; }
        public WireNode Node2 { get; set; }

        // Helper to check if the given node is part of this connection
        public bool InvolvesNode(WireNode node) => Node1 == node || Node2 == node;
    }
}
