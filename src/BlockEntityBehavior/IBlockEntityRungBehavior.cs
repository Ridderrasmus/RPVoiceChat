using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.BlockEntityBehavior
{
    /// <summary>
    /// Implemented by block entity behaviors that react when the block is "rung" (interaction completed).
    /// The block class invokes OnRung on all behaviors implementing this interface.
    /// </summary>
    public interface IBlockEntityRungBehavior
    {
        void OnRung();
    }
}
