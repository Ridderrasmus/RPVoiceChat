using Vintagestory.API.Common;

namespace RPVoiceChat.Util
{
    /// <summary>
    /// Extension methods for Block class, inspired by FoodShelves LocationalExtensions
    /// Source: https://github.com/SONZ-INA/VintageStory-FoodShelves/blob/master/code/Utility/Extensions/LocationalExtensions.cs
    /// </summary>
    public static class BlockExtensions
    {
        /// <summary>
        /// Returns the rotation angle in degrees for the block based on its variant suffix (e.g., "-north", "-south").
        /// For this method to work properly, it *must* align with the rotation defined in the block's type.
        /// Inspired by FoodShelves LocationalExtensions.GetRotationAngle
        /// </summary>
        public static int GetRotationAngle(this Block block)
        {
            if (block?.Code == null) return 0;
            
            string blockPath = block.Code.Path;
            return blockPath switch
            {
                var path when path.EndsWith("-north") => 0,
                var path when path.EndsWith("-south") => 180,
                var path when path.EndsWith("-east") => 270,
                var path when path.EndsWith("-west") => 90,
                _ => 0
            };
        }
    }
}

