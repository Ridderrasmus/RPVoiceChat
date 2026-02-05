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

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            // When the bell lands after falling (UnstableFalling), convert down -> up so it sits correctly
            if (Api?.Side != EnumAppSide.Server || Block == null) return;
            if (byItemStack != null) return; // Placed by player: keep orientation

            if (Block.Variant.TryGetValue("v", out var v) && v == "down")
            {
                var upPath = Block.Code.Path.Replace("-down-", "-up-");
                if (upPath == Block.Code.Path) return;
                var upBlock = Api.World.GetBlock(Block.Code.WithPath(upPath));
                if (upBlock != null)
                    Api.World.BlockAccessor.SetBlock(upBlock.BlockId, Pos);
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _shapePath = Block?.Shape?.Base?.Path ?? "block/carillonbell/carillonbell_rope";
            if (api.Side == EnumAppSide.Client)
                Animatable?.InitializeAnimatorWithRotation(_shapePath);
        }

        /// <summary>
        /// Called when the block is rung. Plays sound and, for rope variant, triggers swing + clapper animations.
        /// </summary>
        public void OnRung()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            GetBehavior<BEBehaviorSoundable>()?.OnRung();

            bool usesRopeShape = _shapePath != null &&
                _shapePath.IndexOf("carillonbell_rope", StringComparison.OrdinalIgnoreCase) >= 0;
            if (usesRopeShape && Animatable != null)
            {
                Animatable.PlaySingleShotAnimation("swing");
                Animatable.PlaySingleShotAnimation("clapper");
                MarkDirty();
            }
        }
    }
}
