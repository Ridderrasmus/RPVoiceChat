using Vintagestory.API.Common;

namespace RPVoiceChat.BlockEntities
{
    public class BlockEntityRegistry
    {
        public static void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("BigBellPart", typeof(BlockEntityBigBellPart));
        }
    }
}
