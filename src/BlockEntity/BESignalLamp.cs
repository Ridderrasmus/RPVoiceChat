using RPVoiceChat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntitySignalLamp : Vintagestory.API.Common.BlockEntity
    {
        private const string SignalLampShapeCode = "block/signallamp";
        private const string SlatesAnimationCode = "slates";
        private BEBehaviorAnimatable Animatable => GetBehavior<BEBehaviorAnimatable>();
        private BlockEntityAnimationUtil AnimUtil => Animatable?.animUtil;

        public BlockEntitySignalLamp()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Client)
            {
                InitializeClientAnimator();
            }
        }

        /// <summary>
        /// Start the slates animation
        /// </summary>
        public void StartSlatesAnimation()
        {
            var animUtil = AnimUtil;
            bool wasRunning = animUtil?.activeAnimationsByAnimCode?.ContainsKey(SlatesAnimationCode) == true;
            if (animUtil != null && !wasRunning)
            {
                animUtil.StartAnimation(new AnimationMetaData
                {
                    Animation = SlatesAnimationCode,
                    Code = SlatesAnimationCode
                });
            }
            if (!wasRunning && Api?.Side == EnumAppSide.Server)
            {
                MarkDirty();
                PlayShutterSound();
            }
        }

        /// <summary>
        /// Stop the slates animation
        /// </summary>
        public void StopSlatesAnimation()
        {
            AnimUtil?.StopAnimation(SlatesAnimationCode);
            if (Api?.Side == EnumAppSide.Server)
            {
                MarkDirty();
            }
        }

        private void InitializeClientAnimator()
        {
            var animUtil = AnimUtil;
            if (animUtil == null || animUtil.animator != null || Api?.Side != EnumAppSide.Client)
            {
                return;
            }

            if (Block?.Code != null)
            {
                var assetLoc = new AssetLocation(Block.Code.Domain, "shapes/" + SignalLampShapeCode + ".json");
                var shape = Shape.TryGet(Api, assetLoc);
                if (shape?.Animations != null && shape.Animations.Length > 0)
                {
                    shape.InitForAnimations(Api.Logger, SignalLampShapeCode, System.Array.Empty<string>());
                }
            }

            float rotYDeg = GetBlockSideRotY();
            animUtil.InitializeAnimator(SignalLampShapeCode, null, null, new Vec3f(0, rotYDeg, 0));
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

        private void PlayShutterSound()
        {
            Api.World.PlaySoundAt(
                new AssetLocation(RPVoiceChatMod.modID, "sounds/block/signallamp/shutter.ogg"),
                Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5,
                null,
                false,
                6,
                0.25f
            );
        }
    }
}
