using System.Text;
using RPVoiceChat.GameContent.Blocks;
using RPVoiceChat.GameContent.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Items
{
    public class TelegraphWireItem : Item
    {
        private const int MaxConnectionDistance = 20;
        private BlockPos firstNodePos;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null)
                return;

            var node = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as WireNode;

            if (node == null)
                return;

            handling = EnumHandHandling.PreventDefault;

            // First click
            if (firstNodePos == null)
            {
                firstNodePos = blockSel.Position.Copy();
                (byEntity.World.Api as ICoreClientAPI)?.TriggerChatMessage("Starting point recorded.");
                return;
            }

            // Second click
            var firstNode = byEntity.World.BlockAccessor.GetBlockEntity(firstNodePos) as WireNode;

            if (firstNode != null && firstNode != node)
            {
                double dist = firstNodePos.DistanceTo(node.Pos);
                if (dist > MaxConnectionDistance)
                {
                    (byEntity.Api as ICoreClientAPI)?.TriggerChatMessage($"Too far! Max distance is {MaxConnectionDistance} blocks.");
                }
                else
                {
                    WireConnection connection = new WireConnection(firstNode, node);
                    firstNode.Connect(connection); // Connect() method handles adding to network and fusion
                    (byEntity.Api as ICoreClientAPI)?.TriggerChatMessage("Connection successful!");
                }
            }

            firstNodePos = null; // Reset
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (firstNodePos != null)
            {
                dsc.AppendLine($"Connection pending since:  {firstNodePos}");
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }
}
