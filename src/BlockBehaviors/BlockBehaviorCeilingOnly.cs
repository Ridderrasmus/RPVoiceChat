using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockBehaviors
{
    public class BlockBehaviorCeilingOnly: BlockBehavior
    {
        public BlockBehaviorCeilingOnly(Block block) : base(block) { }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            BlockFacing facing = BlockFacing.UP;
            BlockPos pos = blockSel.Position;

            if (world.BlockAccessor.IsSideSolid(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z, facing.Opposite))
            {
                handling = EnumHandling.PassThrough;
                return true;
            }

            handling = EnumHandling.PreventDefault;
            failureCode = "block_not_on_ceiling";
            return false; 
        }
    }
}
