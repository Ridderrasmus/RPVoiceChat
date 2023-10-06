using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class BlockRegistry
    {
        public static void RegisterBlocks(ICoreAPI api)
        {
            if (WorldConfig.GetBool("extra-content") == false) return;
            api.RegisterBlockClass("soundemittingblock", typeof(SoundEmittingBlock));
        }
    }
}
