using RPVoiceChat.Config;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntityRegistry
    {
        public static void RegisterBlockEntities(ICoreAPI api)
        {
            // Toujours enregistrer les classes, même si le contenu est désactivé
            // Les blocks seront désactivés dans AssetsFinalize si nécessaire
            api.RegisterBlockEntityClass("ChurchBellPart", typeof(BlockEntityChurchBellPart));
            api.RegisterBlockEntityClass("ChurchBellLayer", typeof(BlockEntityChurchBellLayer));
            api.RegisterBlockEntityClass("BlockEntityTelegraph", typeof(BlockEntityTelegraph));
            api.RegisterBlockEntityClass("BlockEntityConnector", typeof(BlockEntityConnector));
            api.RegisterBlockEntityClass("BlockEntityPrinter", typeof(BlockEntityPrinter));
        }
    }
}
