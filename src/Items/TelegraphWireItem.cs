using System.Text;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Items
{
    public class TelegraphWireItem : Item
    {
        private int MaxConnectionDistance => ServerConfigManager.TelegraphMaxConnectionDistance;
        private BlockPos firstNodePos;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null)
            {
                // Let base behaviors execute (like GroundStorable)
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            var node = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as IWireConnectable;
            if (node == null)
            {
                // No node detected, let base behaviors execute
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            // First click
            if (firstNodePos == null)
            {
                firstNodePos = blockSel.Position.Copy();
                (byEntity.World.Api as ICoreClientAPI)?.TriggerChatMessage(UIUtils.I18n("Wire.StartConnection"));
                return;
            }

            // Second click
            var firstNode = byEntity.World.BlockAccessor.GetBlockEntity(firstNodePos) as WireNode;

            if (firstNode != null && firstNode != node)
            {
                double dist = firstNodePos.DistanceTo(node.Position);
                if (dist > MaxConnectionDistance)
                {
                    (byEntity.Api as ICoreClientAPI)?.TriggerChatMessage(UIUtils.I18n("Wire.ConnectionTooFar", MaxConnectionDistance));
                }
                else
                {
                    WireConnection connection = new WireConnection(firstNode, node);
                    firstNode.Connect(connection); // Connect() method handles adding to network and fusion
                    (byEntity.Api as ICoreClientAPI)?.TriggerChatMessage(UIUtils.I18n("Wire.ConnectionSuccess"));

                    slot.TakeOut(1);
                    slot.MarkDirty();
                }
            }

            firstNodePos = null; // Reset
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            // TODO: not working
            if (firstNodePos != null)
            {
                dsc.AppendLine(UIUtils.I18n("Wire.Pending", firstNodePos));
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }
}
