using Vintagestory.API.Client;
using Vintagestory.API.Common;

public class TelephoneBlock : WireNodeBlock
{
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return new WorldInteraction[]
        {
            new WorldInteraction
            {
                ActionLangCode = "rpvoicechat:Telephone.Interaction.Use",
                MouseButton = EnumMouseButton.Right
            }
        };
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Code.ToShortString() == "rpvoicechat:telegraphwire")
            return false;

        var telephone = world.BlockAccessor.GetBlockEntity(blockSel.Position) as RPVoiceChat.GameContent.BlockEntity.BlockEntityTelephone;
        telephone?.OnInteract(byPlayer);
        return true;
    }
}
