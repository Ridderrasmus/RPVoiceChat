using RPVoiceChat.Config;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRegistry
    {
        public static void RegisterBlockEntities(ICoreAPI api)
        {
            if (WorldConfig.GetBool("additional-content") == false) return;
            
            api.RegisterBlockEntityClass("ChurchBellPart", typeof(BlockEntityChurchBellPart));
            api.RegisterBlockEntityClass("ChurchBellLayer", typeof(BlockEntityChurchBellLayer));
            
            if (WorldConfig.GetBool("telegraph-content") != false)
            {
                api.RegisterBlockEntityClass("BlockEntityTelegraph", typeof(BlockEntityTelegraph));
                api.RegisterBlockEntityClass("BlockEntityConnector", typeof(BlockEntityConnector));
                api.RegisterBlockEntityClass("BlockEntityPrinter", typeof(BlockEntityPrinter));
            }
        }
    }
}
