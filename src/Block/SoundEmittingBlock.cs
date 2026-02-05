using RPVoiceChat.GameContent.BlockEntityBehavior;
using Vintagestory.API.Common;

public class SoundEmittingBlock : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        return true;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        return true;
    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        return true;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side.IsServer())
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be != null)
            {
                foreach (var behavior in be.Behaviors)
                {
                    if (behavior is IBlockEntityRungBehavior rung)
                        rung.OnRung();
                }
            }
        }

        base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
    }
}
