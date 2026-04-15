using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace RPVoiceChat.GameContent.Block
{
    public class SwitchboardBlock : WireNodeBlock, IMechanicalPowerBlock
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.Code.ToShortString() == "rpvoicechat:telegraphwire")
            {
                // Keep wire placement behavior on right click.
                return false;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            Vintagestory.API.Common.Block blockAtPos = world.BlockAccessor.GetBlock(pos);
            if (blockAtPos?.Variant == null || !blockAtPos.Variant.TryGetValue("side", out string sideStr)) return false;
            BlockFacing frontFace = BlockFacing.FromCode(sideStr);
            if (frontFace == null) return false;
            return face == frontFace.Opposite;
        }

        public void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
        }

        public MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            return world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>()?.Network;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (world.Side == EnumAppSide.Server)
            {
                var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySwitchboard;
                be?.TryDiscoverNetwork();
            }
        }
    }
}
