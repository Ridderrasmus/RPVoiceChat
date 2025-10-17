using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;

public class PrinterBlock : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityPrinter printer = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPrinter;
        printer?.OnInteract(byPlayer);

        return true;
    }
}
