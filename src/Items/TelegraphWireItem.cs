using RPVoiceChat.GameContent.Blocks;
using RPVoiceChat.src.Systems;
using System.Text;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Items
{
    public class TelegraphWireItem : Item
    {
        private WireNode nodeConnection;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            WireNode node = blockSel.Block?.GetBlockEntity<WireNode>(blockSel);

            if (node == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (node is TelegraphBlockEntity telegraph)
            {
                handling = EnumHandHandling.PreventDefault;

                // Handle connection
                if (nodeConnection == null)
                {
                    nodeConnection = telegraph;
                }
                else
                {
                    telegraph.Connect(nodeConnection);
                    nodeConnection = null;
                }
            }

        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (nodeConnection != null)
            {
                dsc.AppendLine();
                dsc.AppendLine("Connecting:");
                dsc.AppendLine($"Node - {((nodeConnection != null) ? nodeConnection.Pos : "null")}");
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }
}