using RPVoiceChat.Config;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Block
{
    public class BlockRegistry
    {
        public static void RegisterBlocks(ICoreAPI api)
        {
            // Always register all block classes - blocks will be disabled via JSON patches if needed
            // This prevents "no such class registered" errors when JSON files reference these classes
            api.RegisterBlockClass("soundemittingblock", typeof(SoundEmittingBlock));
            api.RegisterBlockClass("carillonbellblock", typeof(CarillonBellBlock));
            api.RegisterBlockClass("churchbellpart", typeof(ChurchBellPartBlock));
            api.RegisterBlockClass("churchbelllayer", typeof(ChurchBellLayerBlock));
            api.RegisterBlockClass("telegraphblock", typeof(TelegraphBlock));
            api.RegisterBlockClass("connectorblock", typeof(ConnectorBlock));
            api.RegisterBlockClass("printerblock", typeof(PrinterBlock));
            api.RegisterBlockClass("signallampblock", typeof(SignalLampBlock));
            api.RegisterBlockClass("bellhammerblock", typeof(BellHammerBlock));
        }
    }
}
