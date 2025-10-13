using RPVoiceChat.Config;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Block
{
    public class BlockRegistry
    {
        public static void RegisterBlocks(ICoreAPI api)
        {
            if (WorldConfig.GetBool("additional-content") == false) return;
            api.RegisterBlockClass("soundemittingblock", typeof(SoundEmittingBlock));
            api.RegisterBlockClass("churchbellpart", typeof(ChurchBellPartBlock));
            api.RegisterBlockClass("churchbelllayer", typeof(ChurchBellLayerBlock));
            api.RegisterBlockClass("telegraphblock", typeof(TelegraphBlock));
            api.RegisterBlockClass("connectorblock", typeof(ConnectorBlock));
        }
    }
}
