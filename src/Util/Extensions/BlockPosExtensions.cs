using Vintagestory.API.MathTools;

namespace RPVoiceChat.Util
{
    /// <summary>Extension methods for <see cref="BlockPos"/>.</summary>
    public static class BlockPosExtensions
    {
        /// <summary>Returns the world position of the block center (pos + 0.5 in X, Y, Z).</summary>
        public static Vec3d ToWorldCenter(this BlockPos pos)
        {
            return new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5);
        }
    }
}
