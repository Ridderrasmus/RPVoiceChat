using RPVoiceChat.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Blocks
{
    public class ChurchBellLayerBlock : Block
    {

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityChurchBellLayer bigBellPart = blockAccessor.GetBlockEntity(pos) as BlockEntityChurchBellLayer;
            if (bigBellPart?.Inventory != null && bigBellPart.Inventory[2].Empty) return new Cuboidf[] { CollisionBoxes[0] };

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityChurchBellLayer bigBellPart = blockAccessor.GetBlockEntity(pos) as BlockEntityChurchBellLayer;
            if (bigBellPart?.Inventory != null && bigBellPart.Inventory[2].Empty) return new Cuboidf[] { SelectionBoxes[0] };

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityChurchBellLayer bigBellPart = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChurchBellLayer;
            bigBellPart?.OnInteract(byPlayer);

            return true;
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (!itemslot.Empty && itemslot.Itemstack.Collectible.FirstCodePart() == "hammer" && api.Side == EnumAppSide.Client)
            {
                BlockEntityChurchBellLayer bigBellPart = player.Entity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChurchBellLayer;
                bigBellPart?.OnInteract(player);

                return 800f;
            }

            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }
    }
}