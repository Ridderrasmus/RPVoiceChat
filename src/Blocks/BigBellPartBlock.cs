using RPVoiceChat.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Blocks
{
    public class BigBellPartBlock : Block
    {

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityBigBellPart bigBellPart = blockAccessor.GetBlockEntity(pos) as BlockEntityBigBellPart;
            if (bigBellPart?.Inventory != null && bigBellPart.Inventory[2].Empty) return new Cuboidf[] { CollisionBoxes[0] };

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityBigBellPart bigBellPart = blockAccessor.GetBlockEntity(pos) as BlockEntityBigBellPart;
            if (bigBellPart?.Inventory != null && bigBellPart.Inventory[2].Empty) return new Cuboidf[] { SelectionBoxes[0] };

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityBigBellPart bigBellPart = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBigBellPart;
            bigBellPart?.OnInteract(byPlayer);

            return true;
        }
    }
}