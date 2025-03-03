using RPVoiceChat.DB;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Audio.AudioSources
{
    public class BlockAudioSource : BaseAudioSource
    {
        private BlockPos blockPos;

        public BlockAudioSource(ICoreClientAPI capi, ClientSettingsRepository clientSettingsRepo, BlockPos blockPos) : base(capi, clientSettingsRepo, blockPos.ToVec3f())
        {
            this.blockPos = blockPos;
        }

        public override string GetSourceId()
        {
            return $"player-{blockPos.ToVec3f()}";
        }

        public override float GetFinalGain()
        {
            var globalGain = Math.Min(PlayerListener.gain, 1);
            var sourceGain = 1;
            var finalGain = GameMath.Clamp(globalGain * sourceGain, 0, 1);

            return finalGain;
        }

        public override Vec3d? GetSourcePosition()
        {
            return blockPos.ToVec3d();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}