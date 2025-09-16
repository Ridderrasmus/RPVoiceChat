using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRegistry
    {
        public static void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("ChurchBellPart", typeof(BlockEntityChurchBellPart));
            api.RegisterBlockEntityClass("ChurchBellLayer", typeof(BlockEntityChurchBellLayer));
            api.RegisterBlockEntityClass("BlockEntityTelegraph", typeof(BlockEntityTelegraph));
            api.RegisterBlockEntityClass("BlockEntityConnector", typeof(BlockEntityConnector));
        }
    }
}
