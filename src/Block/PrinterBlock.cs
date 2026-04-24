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
        if (blockSel == null)
        {
            return false;
        }

        if (world.Side == EnumAppSide.Client)
        {
            BlockEntityPrinter bePrinter = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPrinter;
            bePrinter?.NotifyClientInventoryOpenIntent();
        }

        BlockEntityPrinter printer = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPrinter;
        if (printer != null)
        {
            return printer.OnPlayerRightClick(byPlayer, blockSel);
        }

        return false;
    }
}
