using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Block
{
    public class SpeakerBlock : WireNodeBlock
    {
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "rpvoicechat:Speaker.Interaction.Info",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }
    }
}
