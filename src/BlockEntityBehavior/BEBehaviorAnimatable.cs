using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntityBehavior
{
    /// <summary>
    /// BlockEntityBehavior that handles animation methods and syncs animation state to clients (server→client).
    /// Add this behavior "in addition to" the vanilla Animatable behavior; configure which animation codes to sync via JSON.
    /// Tracks running state so sync works on server (server animator is not always updated).
    /// Big thanks to Sonz-ina (VintageStory-FoodShelves) for the sync pattern inspiration.
    /// </summary>
    public class BEBehaviorAnimatable : Vintagestory.API.Common.BlockEntityBehavior
    {
        private string[] _animationCodes = System.Array.Empty<string>();
        /// <summary>Track which animations we started, so ToTreeAttributes works on server (animUtil may be empty there).</summary>
        private HashSet<string> _runningAnimations = new HashSet<string>();
        /// <summary>One-shot codes to clear after next ToTreeAttributes so they are only synced once.</summary>
        private HashSet<string> _oneShotJustTriggered = new HashSet<string>();

        public BEBehaviorAnimatable(Vintagestory.API.Common.BlockEntity blockEntity) : base(blockEntity)
        {
        }

        /// <summary>Gets the underlying animator (from the vanilla Animatable behavior).</summary>
        public BlockEntityAnimationUtil AnimUtil =>
            Blockentity.GetBehavior<Vintagestory.GameContent.BEBehaviorAnimatable>()?.animUtil;

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            string codesStr = properties["animationCodes"].AsString(null);
            if (!string.IsNullOrWhiteSpace(codesStr))
            {
                _animationCodes = codesStr.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (_animationCodes.Length == 0) return;
            foreach (string code in _animationCodes)
            {
                tree.SetBool("anim_" + code, _runningAnimations.Contains(code));
            }
            // One-shots: clear after writing so we only sync once
            foreach (string code in _oneShotJustTriggered)
            {
                _runningAnimations.Remove(code);
            }
            _oneShotJustTriggered.Clear();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            if (_animationCodes.Length == 0) return;
            foreach (string code in _animationCodes)
            {
                bool shouldRun = tree.GetBool("anim_" + code, false);
                if (shouldRun)
                    _runningAnimations.Add(code);
                else
                    _runningAnimations.Remove(code);
                if (worldForResolving.Side == EnumAppSide.Client)
                {
                    if (shouldRun)
                        StartAnimationIfNotRunning(code);
                    else
                        StopAnimation(code);
                }
            }
        }

        /// <summary>Initialize animator with block rotation. Call from OnTesselation.</summary>
        public void InitializeAnimatorWithRotation(string shapeCode)
        {
            var animUtil = AnimUtil;
            if (animUtil?.animator == null)
            {
                var rotYDeg = Blockentity.Block?.GetRotationAngle() ?? 0;
                animUtil?.InitializeAnimator(shapeCode, null, null, new Vec3f(0, rotYDeg, 0));
            }
        }

        /// <summary>Start an animation if it's not already running.</summary>
        public void StartAnimationIfNotRunning(string animationName)
        {
            var animUtil = AnimUtil;
            if (animUtil == null) return;
            if (_animationCodes.Contains(animationName))
                _runningAnimations.Add(animationName);
            if (animUtil.activeAnimationsByAnimCode.ContainsKey(animationName)) return;
            animUtil.StartAnimation(new AnimationMetaData
            {
                Animation = animationName,
                Code = animationName
            });
        }

        /// <summary>Stop an animation by name.</summary>
        public void StopAnimation(string animationName)
        {
            if (_animationCodes.Contains(animationName))
                _runningAnimations.Remove(animationName);
            AnimUtil?.StopAnimation(animationName);
        }

        /// <summary>Play a single-shot animation (stops if already playing and restarts it).</summary>
        public void PlaySingleShotAnimation(string animationName)
        {
            var animUtil = AnimUtil;
            if (animUtil == null) return;
            if (_animationCodes.Contains(animationName))
            {
                _runningAnimations.Add(animationName);
                _oneShotJustTriggered.Add(animationName);
            }
            if (animUtil.activeAnimationsByAnimCode.ContainsKey(animationName))
                animUtil.StopAnimation(animationName);
            animUtil.StartAnimation(new AnimationMetaData
            {
                Animation = animationName,
                Code = animationName
            });
        }

        /// <summary>Whether any animations are currently active.</summary>
        public bool HasActiveAnimations()
        {
            var animUtil = AnimUtil;
            return animUtil?.activeAnimationsByAnimCode.Count > 0 ||
                   (animUtil?.animator != null && animUtil.animator.ActiveAnimationCount > 0);
        }
    }
}
