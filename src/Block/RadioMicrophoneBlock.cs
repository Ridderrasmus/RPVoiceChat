using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Block
{
    public class RadioMicrophoneBlock : WireNodeBlock
    {
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "rpvoicechat:Radio.Microphone.Interaction.Use",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.Code.ToShortString() == "rpvoicechat:telegraphwire")
            {
                return false;
            }

            var microphone = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityRadioMicrophone;
            microphone?.OnInteract(byPlayer);
            return true;
        }
    }
}
