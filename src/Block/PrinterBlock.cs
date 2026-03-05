using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

public class PrinterBlock : Block
{
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return new WorldInteraction[]
        {
            new WorldInteraction
            {
                ActionLangCode = "rpvoicechat:Printer.Interaction.CheckContents",
                MouseButton = EnumMouseButton.Right
            }
        };
    }

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
