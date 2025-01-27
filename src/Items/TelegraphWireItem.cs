using RPVoiceChat.GameContent.Blocks;
using RPVoiceChat.src.Systems;
using System.Text;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Items
{
    public class TelegraphWireItem : Item
    {
        private WireConnection connection;

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
                if (connection == null)
                {
                    connection = new WireConnection(telegraph);
                    node.Connect(connection);
                }
                else
                {
                    node.Connect(connection);
                    connection = null;
                }
            }

        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (connection != null)
            {
                dsc.AppendLine();
                dsc.AppendLine("Connections:");
                dsc.AppendLine($"Node1 - {((connection.Node1 != null) ? connection.Node1.Pos : "null")}");
                dsc.AppendLine($"Node2 - {((connection.Node2 != null) ? connection.Node2.Pos : "null")}");
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }
}