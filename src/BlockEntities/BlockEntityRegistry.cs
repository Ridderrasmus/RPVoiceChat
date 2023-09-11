using Vintagestory.API.Common;

namespace RPVoiceChat.BlockEntities
{
    public class BlockEntityRegistry
    {
        public static void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("bigbellpart", typeof(BlockEntityBigBellPart));
        }
    }
}
