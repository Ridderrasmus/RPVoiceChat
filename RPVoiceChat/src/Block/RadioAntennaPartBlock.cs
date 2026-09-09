using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Block
{
    public class RadioAntennaPartBlock : Vintagestory.API.Common.Block
    {
        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            BlockPos placePos = ResolvePlacePos(world, blockSel);
            BlockSelection placeSel = blockSel.Clone();
            placeSel.Position = placePos;
            placeSel.DidOffset = false;

            // Validate the empty cell above the stack, not the thin antenna hitbox that was clicked.
            if (!base.CanPlaceBlock(world, byPlayer, placeSel, ref failureCode))
            {
                return false;
            }

            return CanPlaceOnSupport(world, placePos.DownCopy(), ref failureCode);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockPos placePos = ResolvePlacePos(world, blockSel);
            BlockSelection placeSel = blockSel.Clone();
            placeSel.Position = placePos;
            placeSel.DidOffset = false;

            bool placed = DoPlaceBlock(world, byPlayer, placeSel, itemstack);
            if (placed)
            {
                world.BlockAccessor.GetBlockEntity(placePos.DownCopy())?.MarkDirty(true);
            }

            return placed;
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Vintagestory.API.Common.Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            // Allow Unstable stacking: thin antenna is not SideSolid, but parts attach on top.
            if (blockFace == BlockFacing.UP && IsAntennaPartBlock(block))
            {
                return true;
            }

            return base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (neibpos.Y == pos.Y + 1)
            {
                world.BlockAccessor.GetBlockEntity(pos)?.MarkDirty(true);
            }
        }

        private static bool IsAntennaPartBlock(Vintagestory.API.Common.Block block)
        {
            return block is RadioAntennaPartBlock
                || block?.Class == "radioantenna_partblock"
                || block?.Class == "radioantennapartblock";
        }

        /// <summary>
        /// Always stack on top of the antenna/emitter column. Side clicks on the thin tip must not
        /// place beside it (that used to fail with NeedSupport).
        /// </summary>
        private static BlockPos ResolvePlacePos(IWorldAccessor world, BlockSelection blockSel)
        {
            BlockPos clicked = blockSel.DidOffset
                ? blockSel.Position.AddCopy(blockSel.Face.Opposite)
                : blockSel.Position;

            if (IsAntennaSupport(world, clicked))
            {
                return FindStackTip(world, clicked).UpCopy();
            }

            BlockPos below = blockSel.Position.DownCopy();
            if (world.BlockAccessor.GetBlock(blockSel.Position).Replaceable >= 6000 && IsAntennaSupport(world, below))
            {
                return FindStackTip(world, below).UpCopy();
            }

            var blockAtSel = world.BlockAccessor.GetBlock(blockSel.Position);
            return blockAtSel.Replaceable >= 6000
                ? blockSel.Position.Copy()
                : blockSel.Position.AddCopy(blockSel.Face);
        }

        private static BlockPos FindStackTip(IWorldAccessor world, BlockPos supportOrPart)
        {
            BlockPos tip = supportOrPart.Copy();
            while (IsAntennaPart(world, tip.UpCopy()))
            {
                tip = tip.UpCopy();
            }

            return tip;
        }

        private static bool IsAntennaSupport(IWorldAccessor world, BlockPos pos)
        {
            return IsAntennaPart(world, pos) || IsRadioEmitter(world, pos);
        }

        private static bool IsAntennaPart(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityRadioAntennaPart)
            {
                return true;
            }

            Vintagestory.API.Common.Block block = world.BlockAccessor.GetBlock(pos);
            return block is RadioAntennaPartBlock
                || block?.Class == "radioantenna_partblock"
                || block?.Class == "radioantennapartblock";
        }

        private static bool IsRadioEmitter(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityRadioEmitter)
            {
                return true;
            }

            return world.BlockAccessor.GetBlock(pos)?.Class == "radioemitterblock";
        }

        private static bool CanPlaceOnSupport(IWorldAccessor world, BlockPos supportPos, ref string failureCode)
        {
            if (IsAntennaSupport(world, supportPos))
            {
                return true;
            }

            if (world.BlockAccessor.GetBlockEntity(supportPos) is BlockEntityRadioSupervisionConsole)
            {
                failureCode = RPVoiceChat.RPVoiceChatMod.modID + ":Radio.AntennaPart.PlaceFailure.WrongBlock";
                return false;
            }

            failureCode = RPVoiceChat.RPVoiceChatMod.modID + ":Radio.AntennaPart.PlaceFailure.NeedSupport";
            return false;
        }
    }
}
