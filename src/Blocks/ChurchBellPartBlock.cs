using RPVoiceChat.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Blocks
{
    public class ChurchBellPartBlock : Block
    {

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityChurchBellPart bigBellPart = blockAccessor.GetBlockEntity(pos) as BlockEntityChurchBellPart;
            if (bigBellPart?.Inventory != null && bigBellPart.Inventory[2].Empty) return new Cuboidf[] { CollisionBoxes[0] };

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityChurchBellPart bigBellPart = blockAccessor.GetBlockEntity(pos) as BlockEntityChurchBellPart;
            if (bigBellPart?.Inventory != null && bigBellPart.Inventory[2].Empty) return new Cuboidf[] { SelectionBoxes[0] };

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityChurchBellPart bigBellPart = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChurchBellPart;
            bigBellPart?.OnInteract(byPlayer);

            return true;
        }
    }
}