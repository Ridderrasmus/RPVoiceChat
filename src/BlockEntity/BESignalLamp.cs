using RPVoiceChat;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntity
{
    public class BESignalLamp : Vintagestory.API.Common.BlockEntity
    {
        public BlockEntityAnimationUtil animUtil { get { return this.GetAnimUtil(); } }

        public BESignalLamp()
        {
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            this.InitializeAnimatorWithRotation("signallamp");
            return this.HasActiveAnimations();
        }

        /// <summary>
        /// Start the slates animation
        /// </summary>
        public void StartSlatesAnimation()
        {
            bool wasRunning = animUtil?.activeAnimationsByAnimCode?.ContainsKey("slates") == true;
            this.StartAnimationIfNotRunning("slates");
            if (!wasRunning && Api?.Side == EnumAppSide.Server)
            {
                PlayShutterSound();
            }
        }

        /// <summary>
        /// Stop the slates animation
        /// </summary>
        public void StopSlatesAnimation()
        {
            this.StopAnimation("slates");
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
