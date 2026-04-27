using System;
using RPVoiceChat.GameContent.BlockEntityBehavior;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntity
{
    /// <summary>
    /// Block entity for the carillon bell: animator setup (Initialize, client-only) and "on rung" behaviour (sound + swing/clapper animations when rope shape).
    /// </summary>
    public class BlockEntityCarillonBell : Vintagestory.API.Common.BlockEntity
    {
        private const string SwingAnimationCode = "swing";
        private const string ClapperAnimationCode = "clapper";
        private string _shapePath;
        private int _ringSequence;
        private int _clientLastRingSequence = -1;
        private Vintagestory.GameContent.BEBehaviorAnimatable VanillaAnimatable => GetBehavior<Vintagestory.GameContent.BEBehaviorAnimatable>();
        private BlockEntityAnimationUtil AnimUtil => VanillaAnimatable?.animUtil;

        public BlockEntityCarillonBell()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _shapePath = ResolveShapePath();
            if (api.Side == EnumAppSide.Client)
                InitializeClientAnimator();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _ringSequence = tree.GetInt("rpvc:ringSequence", 0);
            _shapePath = ResolveShapePath();

            if (worldForResolving.Side != EnumAppSide.Client)
            {
                return;
            }

            InitializeClientAnimator();
            if (_clientLastRingSequence < 0)
            {
                _clientLastRingSequence = _ringSequence;
                return;
            }

            if (_ringSequence != _clientLastRingSequence && UsesRopeShape())
            {
                PlaySingleShotAnimation(SwingAnimationCode);
                PlaySingleShotAnimation(ClapperAnimationCode);
            }
            _clientLastRingSequence = _ringSequence;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("rpvc:ringSequence", _ringSequence);
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
            if (usesRopeShape)
            {
                _ringSequence++;
                MarkDirty(true);
            }
        }

        private void InitializeClientAnimator()
        {
            var animUtil = AnimUtil;
            if (animUtil == null || animUtil.animator != null || Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            if (Block?.Code != null && !string.IsNullOrWhiteSpace(_shapePath))
            {
                var assetLoc = new AssetLocation(Block.Code.Domain, "shapes/" + _shapePath + ".json");
                var shape = Shape.TryGet(Api, assetLoc);
                if (shape?.Animations != null && shape.Animations.Length > 0)
                {
                    shape.InitForAnimations(Api.Logger, _shapePath, Array.Empty<string>());
                }
            }

            float rotYDeg = GetBlockSideRotY();
            animUtil.InitializeAnimator(_shapePath, null, null, new Vec3f(0, rotYDeg, 0));
        }

        private void PlaySingleShotAnimation(string animationCode)
        {
            var animUtil = AnimUtil;
            if (animUtil == null) return;
            if (animUtil.activeAnimationsByAnimCode.ContainsKey(animationCode))
            {
                animUtil.StopAnimation(animationCode);
            }

            animUtil.StartAnimation(new AnimationMetaData
            {
                Animation = animationCode,
                Code = animationCode
            });
        }

        private float GetBlockSideRotY()
        {
            return Block?.Variant?.TryGetValue("side", out string side) == true
                ? side switch
                {
                    "north" => 0f,
                    "east" => 270f,
                    "west" => 90f,
                    "south" => 180f,
                    _ => 0f
                }
                : 0f;
        }

        private bool UsesRopeShape()
        {
            return Block?.Variant != null &&
                   Block.Variant.TryGetValue("v", out string v) &&
                   string.Equals(v, "down", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveShapePath()
        {
            return Block?.Shape?.Base?.Path ?? "block/carillonbell/carillonbell_rope";
        }
    }
}
