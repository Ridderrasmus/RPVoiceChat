using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Block
{
    /// <summary>
    /// Block class for the carillon bell. Delegates "on rung" to the block entity (sound + animations when rope shape).
    /// </summary>
    public class CarillonBellBlock : Vintagestory.API.Common.Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (base.OnBlockInteractStart(world, byPlayer, blockSel)) return true;
            return true; // capture interaction so OnBlockInteractStop is called
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel) || true;
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
            return true;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!world.Side.IsServer()) return;

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCarillonBell;
            be?.OnRung();

            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }
    }
}
