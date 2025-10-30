using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.API.Client.Tesselation;

namespace RPVoiceChat.Util
{
    /// <summary>
    /// Extension methods for managing animations in Block Entities.
    /// Provides utilities for animating blocks with proper orientation support.
    /// </summary>
    public static class AnimationExtensions
    {
        /// <summary>
        /// Get the BlockEntityAnimationUtil from a BlockEntity.
        /// </summary>
        /// <param name="blockEntity">The block entity</param>
        /// <returns>The animation util, or null if not available</returns>
        public static BlockEntityAnimationUtil GetAnimUtil(this BlockEntity blockEntity)
        {
            return blockEntity.GetBehavior<BEBehaviorAnimatable>()?.animUtil;
        }

        /// <summary>
        /// Initialize animator with block rotation. 
        /// Call this from OnTesselation override in your BlockEntity.
        /// </summary>
        /// <param name="blockEntity">The block entity</param>
        /// <param name="shapeCode">The shape code (e.g. "printer", "telegraphkey")</param>
        public static void InitializeAnimatorWithRotation(this BlockEntity blockEntity, string shapeCode)
        {
            var animUtil = blockEntity.GetAnimUtil();
            if (animUtil?.animator == null)
            {
                var rotYDeg = blockEntity.Block?.GetRotationAngle() ?? 0;
                animUtil?.InitializeAnimator(shapeCode, null, null, new Vec3f(0, rotYDeg, 0));
            }
        }

        /// <summary>
        /// Play a single-shot animation (stops if already playing and restarts it).
        /// Useful for click animations or button presses.
        /// </summary>
        /// <param name="blockEntity">The block entity</param>
        /// <param name="animationName">Name of the animation to play</param>
        public static void PlaySingleShotAnimation(this BlockEntity blockEntity, string animationName)
        {
            var animUtil = blockEntity.GetAnimUtil();
            if (animUtil == null) return;
            
            if (animUtil.activeAnimationsByAnimCode.ContainsKey(animationName))
            {
                animUtil.StopAnimation(animationName);
            }
            
            animUtil.StartAnimation(new AnimationMetaData
            {
                Animation = animationName,
                Code = animationName
            });
        }

        /// <summary>
        /// Start an animation if it's not already running.
        /// Useful for state-based animations like open/close.
        /// </summary>
        /// <param name="blockEntity">The block entity</param>
        /// <param name="animationName">Name of the animation to start</param>
        public static void StartAnimationIfNotRunning(this BlockEntity blockEntity, string animationName)
        {
            var animUtil = blockEntity.GetAnimUtil();
            if (animUtil == null) return;
            
            if (animUtil.activeAnimationsByAnimCode.ContainsKey(animationName))
            {
                return;
            }
            
            animUtil.StartAnimation(new AnimationMetaData
            {
                Animation = animationName,
                Code = animationName
            });
        }

        /// <summary>
        /// Stop an animation by name.
        /// </summary>
        /// <param name="blockEntity">The block entity</param>
        /// <param name="animationName">Name of the animation to stop</param>
        public static void StopAnimation(this BlockEntity blockEntity, string animationName)
        {
            var animUtil = blockEntity.GetAnimUtil();
            if (animUtil == null) return;
            animUtil.StopAnimation(animationName);
        }

        /// <summary>
        /// Check if animations are active. Used in OnTesselation return value.
        /// </summary>
        /// <param name="blockEntity">The block entity</param>
        /// <returns>True if any animations are active</returns>
        public static bool HasActiveAnimations(this BlockEntity blockEntity)
        {
            var animUtil = blockEntity.GetAnimUtil();
            return animUtil?.activeAnimationsByAnimCode.Count > 0 || 
                   (animUtil?.animator != null && animUtil.animator.ActiveAnimationCount > 0);
        }
    }
}

