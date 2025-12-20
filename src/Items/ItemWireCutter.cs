using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.GameContent.Items
{
    public class ItemWireCutter : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            // Check if the block is a BEWireNode
            var wireNode = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BEWireNode;
            if (wireNode == null)
            {
                // Not a BEWireNode, let base behavior handle it
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            // Count connections before removing them
            int connectionCount = wireNode.GetConnections().Count;

            // Remove all connections (works on both server and client)
            var connections = new System.Collections.Generic.List<WireConnection>(wireNode.GetConnections());
            
            foreach (var connection in connections)
            {
                BEWireNode other = connection.GetOtherNode(wireNode);
                if (other != null)
                {
                    other.RemoveConnection(connection);
                    other.MarkForUpdate();
                }
                wireNode.RemoveConnection(connection);
            }
            wireNode.MarkForUpdate(); // MarkForUpdate already calls MarkDirty(true)

            // Show message on client side
            if (byEntity?.Api is ICoreClientAPI capi)
            {
                capi.TriggerChatMessage(UIUtils.I18n("Wire.ConnectionsRemoved", connectionCount));
            }
        }
    }
}

