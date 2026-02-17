using System;
using RPVoiceChat.GameContent.BlockEntityBehavior;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.BlockEntity
{
    /// <summary>
    /// Block entity for the carillon bell: animator setup (Initialize, client-only) and "on rung" behaviour (sound + swing/clapper animations when rope shape).
    /// </summary>
    public class BlockEntityCarillonBell : Vintagestory.API.Common.BlockEntity
    {
        private string _shapePath;
        private BEBehaviorAnimatable Animatable => GetBehavior<BEBehaviorAnimatable>();

        public BlockEntityCarillonBell()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _shapePath = Block?.Shape?.Base?.Path ?? "block/carillonbell/carillonbell_rope";
            if (api.Side == EnumAppSide.Client)
                Animatable?.InitializeAnimatorWithRotation(_shapePath);
        }

        /// <summary>
        /// Called when the block is rung. Plays sound and, for rope variant (ceiling), triggers swing + clapper animations.
        /// </summary>
        public void OnRung()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            GetBehavior<BEBehaviorSoundable>()?.OnRung();

            // Rope shape = ceiling placement (v=down). Use variant so it works regardless of _shapePath init order.
            bool usesRopeShape = Block?.Variant != null &&
                Block.Variant.TryGetValue("v", out string v) && string.Equals(v, "down", StringComparison.OrdinalIgnoreCase);
            if (usesRopeShape && Animatable != null)
            {
                Animatable.PlaySingleShotAnimation("swing");
                Animatable.PlaySingleShotAnimation("clapper");
                MarkDirty();
            }
        }
    }
}
