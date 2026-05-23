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
        private const double ReturnWireChance = 0.75;

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
            wireNode.MarkForUpdate();

            // Damage the item (reduce durability) - only on server side
            if (byEntity.World.Side == EnumAppSide.Server && connectionCount > 0)
            {
                DamageItem(byEntity.World, byEntity, slot);
                DropRecoveredCableLoot(byEntity.World, blockSel.Position, connectionCount);
            }

            // Show message on client side
            if (byEntity?.Api is ICoreClientAPI capi)
            {
                capi.TriggerChatMessage(UIUtils.I18n("Wire.ConnectionsRemoved", connectionCount));
            }
        }

        private static void DropRecoveredCableLoot(IWorldAccessor world, BlockPos atPos, int removedConnections)
        {
            var wireItem = world.GetItem(new AssetLocation(RPVoiceChatMod.modID, "telegraphwire"));
            var copperBits = world.GetItem(new AssetLocation("game", "metalbit-copper"));
            if (wireItem == null && copperBits == null) return;

            for (int i = 0; i < removedConnections; i++)
            {
                ItemStack dropStack = null;
                if (world.Rand.NextDouble() < ReturnWireChance)
                {
                    if (wireItem != null) dropStack = new ItemStack(wireItem, 1);
                }
                else
                {
                    if (copperBits != null)
                    {
                        int copperAmount = 3 + world.Rand.Next(5);
                        dropStack = new ItemStack(copperBits, copperAmount);
                    }
                }

                if (dropStack != null)
                {
                    world.SpawnItemEntity(dropStack, atPos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }
        }
    }
}

