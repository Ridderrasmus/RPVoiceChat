using RPVoiceChat.Config;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRegistry
    {
        public static void RegisterBlockEntities(ICoreAPI api)
        {
            // Always register classes, even if content is disabled
            // Blocks will be disabled in AssetsFinalize if necessary
            api.RegisterBlockEntityClass("ChurchBellPart", typeof(BlockEntityChurchBellPart));
            api.RegisterBlockEntityClass("ChurchBellLayer", typeof(BlockEntityChurchBellLayer));
            api.RegisterBlockEntityClass("BlockEntityTelegraph", typeof(BlockEntityTelegraph));
            api.RegisterBlockEntityClass("BlockEntityConnector", typeof(BlockEntityConnector));
            api.RegisterBlockEntityClass("BlockEntityPrinter", typeof(BlockEntityPrinter));
            api.RegisterBlockEntityClass("BlockEntitySignalLamp", typeof(BlockEntitySignalLamp));
            api.RegisterBlockEntityClass("BlockEntityCarillonBell", typeof(BlockEntityCarillonBell));
            api.RegisterBlockEntityClass("BlockEntityBellHammer", typeof(BlockEntityBellHammer));
            api.RegisterBlockEntityClass("BESoundEmitting", typeof(BESoundEmitting));
        }
    }
}
