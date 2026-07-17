using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Block
{
    public class RadioReceiverBlock : Vintagestory.API.Common.Block
    {
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "rpvoicechat:Radio.Receiver.Interaction.Use",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var receiver = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityRadioReceiver;
            receiver?.OnInteract(byPlayer);
            return true;
        }
    }
}
