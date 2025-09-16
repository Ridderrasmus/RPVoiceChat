using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;

public class TelegraphBlock : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Code.ToShortString() == "rpvoicechat:telegraphwire")
            return false; // Not open the menu if the player holding a telegraph wire

        BlockEntityTelegraph telegraph = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityTelegraph;
        telegraph?.OnInteract();

        return true;
    }
}
