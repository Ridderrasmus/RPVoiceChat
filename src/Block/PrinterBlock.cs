using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;

public class PrinterBlock : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityPrinter bePrinter = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPrinter;
        if (bePrinter != null)
        {
            return bePrinter.OnPlayerRightClick(byPlayer, blockSel);
        }
        return false;
    }
}
