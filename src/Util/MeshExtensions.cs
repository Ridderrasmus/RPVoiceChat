using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Util
{
    /// <summary>
    /// Extension methods for MeshData, inspired by FoodShelves MeshExtensions
    /// Source: https://github.com/SONZ-INA/VintageStory-FoodShelves/blob/master/code/Utility/Extensions/MeshExtensions.cs
    /// </summary>
    public static class MeshExtensions
    {
        /// <summary>
        /// Rotates the mesh around the Y-axis based on the block's predefined rotateY value.
        /// Useful for aligning meshes with the block's in-world orientation.
        /// Inspired by FoodShelves MeshExtensions.BlockYRotation
        /// </summary>
        public static MeshData BlockYRotation(this MeshData mesh, Block block)
        {
            if (mesh == null || block?.Shape == null) return mesh;

            // Use GetRotationAngle extension method which handles block path-based rotation
            int rotationDegrees = block.GetRotationAngle();
            float rotationY = rotationDegrees * GameMath.DEG2RAD;

            return mesh?.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, rotationY, 0);
        }
    }
}
