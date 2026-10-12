using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

public class TelegraphBlock : WireNodeBlock
{
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return new WorldInteraction[]
        {
            new WorldInteraction
            {
                ActionLangCode = "rpvoicechat:Telegraph.Interaction.SendReceive",
                MouseButton = EnumMouseButton.Right
            }
        };
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Code.ToShortString() == "rpvoicechat:telegraphwire")
            return false; // Not open the menu if the player holding a telegraph wire

        BlockEntityTelegraph telegraph = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityTelegraph;
        telegraph?.OnInteract();

        return true;
    }
}
