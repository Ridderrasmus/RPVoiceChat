using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent
{
    /// <summary>
    /// Optional on a BlockEntity: the BELightable behavior uses this as the light origin
    /// instead of the block center. The block entity supplies the position (e.g. beacon supplies structure center).
    /// </summary>
    public interface IBlockEntityWithCustomLightPosition
    {
        /// <summary>World position where the point light should be placed. Supplied by the block entity.</summary>
        Vec3d GetLightOrigin();
    }
}
