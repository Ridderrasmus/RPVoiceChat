using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.BlockEntity
{
    /// <summary>
    /// Minimal block entity for blocks that only need sound (and optionally other) behaviors on interact (e.g. callbell, churchbell).
    /// Configure entityBehaviors in block JSON (e.g. BESoundable).
    /// </summary>
    public class BESoundEmitting : Vintagestory.API.Common.BlockEntity
    {
        public BESoundEmitting()
        {
        }
    }
}
