using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Block
{
    public class RadioAntennaPartBlock : Vintagestory.API.Common.Block
    {
        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockPos supportPos = ResolveSupportPos(world, blockSel);
            return CanPlaceOnSupport(world, supportPos, ref failureCode);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            return DoPlaceBlock(world, byPlayer, blockSel, itemstack);
        }

        private static BlockPos ResolveSupportPos(IWorldAccessor world, BlockSelection blockSel)
        {
            BlockPos placePos = ResolvePlacePos(world, blockSel);
            return placePos.DownCopy();
        }

        private static BlockPos ResolvePlacePos(IWorldAccessor world, BlockSelection blockSel)
        {
            var blockAtSel = world.BlockAccessor.GetBlock(blockSel.Position);
            return blockAtSel.Replaceable >= 6000
                ? blockSel.Position
                : blockSel.Position.AddCopy(blockSel.Face);
        }

        private static bool CanPlaceOnSupport(IWorldAccessor world, BlockPos supportPos, ref string failureCode)
        {
            var supportEntity = world.BlockAccessor.GetBlockEntity(supportPos);
            if (supportEntity is BlockEntityRadioEmitter || supportEntity is BlockEntityRadioAntennaPart)
            {
                return true;
            }

            if (supportEntity is BlockEntityRadioSupervisionConsole)
            {
                failureCode = RPVoiceChat.RPVoiceChatMod.modID + ":Radio.AntennaPart.PlaceFailure.WrongBlock";
                return false;
            }

            Vintagestory.API.Common.Block supportBlock = world.BlockAccessor.GetBlock(supportPos);
            if (supportBlock?.Class == "radioemitterblock" || supportBlock?.Class == "radioantennapartblock")
            {
                return true;
            }

            failureCode = RPVoiceChat.RPVoiceChatMod.modID + ":Radio.AntennaPart.PlaceFailure.NeedSupport";
            return false;
        }
    }
}
