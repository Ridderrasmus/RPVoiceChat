using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class BlockRegistry
    {
        public static void RegisterBlocks(ICoreAPI api)
        {
            if (!api.World.Config.GetBool("rpvoicechat:extra-content")) return;
            api.RegisterBlockClass("soundemittingblock", typeof(SoundEmittingBlock));
        }
    }
}
