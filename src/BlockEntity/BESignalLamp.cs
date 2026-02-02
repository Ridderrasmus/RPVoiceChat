using RPVoiceChat;
using RPVoiceChat.GameContent.BlockEntityBehavior;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Client.Tesselation;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BlockEntitySignalLamp : Vintagestory.API.Common.BlockEntity
    {
        private RPVoiceChat.GameContent.BlockEntityBehavior.BEBehaviorAnimatable Animatable => GetBehavior<RPVoiceChat.GameContent.BlockEntityBehavior.BEBehaviorAnimatable>();

        public BlockEntitySignalLamp()
        {
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            Animatable?.InitializeAnimatorWithRotation("signallamp");
            return Animatable?.HasActiveAnimations() ?? false;
        }

        /// <summary>
        /// Start the slates animation
        /// </summary>
        public void StartSlatesAnimation()
        {
            bool wasRunning = Animatable?.AnimUtil?.activeAnimationsByAnimCode?.ContainsKey("slates") == true;
            Animatable?.StartAnimationIfNotRunning("slates");
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
            Animatable?.StopAnimation("slates");
            if (Api?.Side == EnumAppSide.Server)
            {
                MarkDirty();
            }
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
