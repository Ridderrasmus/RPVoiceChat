using RPVoiceChat.Config;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Block
{
    public class BlockRegistry
    {
        public static void RegisterBlocks(ICoreAPI api)
        {
            // Toujours enregistrer les classes, même si le contenu est désactivé
            // Les blocks seront désactivés dans AssetsFinalize si nécessaire
            api.RegisterBlockClass("soundemittingblock", typeof(SoundEmittingBlock));
            api.RegisterBlockClass("churchbellpart", typeof(ChurchBellPartBlock));
            api.RegisterBlockClass("churchbelllayer", typeof(ChurchBellLayerBlock));
            api.RegisterBlockClass("telegraphblock", typeof(TelegraphBlock));
            api.RegisterBlockClass("connectorblock", typeof(ConnectorBlock));
            api.RegisterBlockClass("printerblock", typeof(PrinterBlock));
        }
    }
}
