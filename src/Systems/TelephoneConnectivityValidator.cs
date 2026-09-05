using System.Collections.Generic;
using RPVoiceChat.GameContent.BlockEntity;

namespace RPVoiceChat.Systems
{
    public static class TelephoneConnectivityValidator
    {
        public static void ValidateNode(BEWireNode node)
        {
            if (node is BlockEntityTelephone telephone)
            {
                ValidateTelephone(telephone);
            }
        }

        public static void ValidateTelephone(BlockEntityTelephone telephone)
        {
            telephone?.InvalidateCallIfDisconnected();
        }

        public static void ValidateNodesInComponents(IEnumerable<BEWireNode> nodes)
        {
            if (nodes == null)
            {
                return;
            }

            foreach (BEWireNode node in nodes)
            {
                ValidateNode(node);
            }
        }
    }
}
