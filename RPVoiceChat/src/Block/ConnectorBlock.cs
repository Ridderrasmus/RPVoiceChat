using Vintagestory.API.Common;

public class ConnectorBlock : WireNodeBlock
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.Code.ToShortString() == "rpvoicechat:telegraphwire")
        {
            // Don't interfere with WireItem interaction
            return false;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
