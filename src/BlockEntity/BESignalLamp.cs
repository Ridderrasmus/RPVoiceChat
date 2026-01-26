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
            this.StartAnimationIfNotRunning("slates");
        }

        /// <summary>
        /// Stop the slates animation
        /// </summary>
        public void StopSlatesAnimation()
        {
            this.StopAnimation("slates");
        }
    }
}
