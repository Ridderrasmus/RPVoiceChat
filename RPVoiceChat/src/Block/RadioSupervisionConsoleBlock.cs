using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Block
{
    public class RadioSupervisionConsoleBlock : WireNodeBlock
    {
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "rpvoicechat:Radio.Console.Interaction.Use",
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

            var console = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityRadioSupervisionConsole;
            console?.OnInteract(byPlayer);
            return true;
        }
    }
}
