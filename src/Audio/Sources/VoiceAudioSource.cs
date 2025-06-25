using Vintagestory.API.Common;

namespace RPVoiceChat.Audio
{
    public class VoiceAudioSource : IAudioSource
    {
        private readonly IPlayer player;
        private readonly AssetLocation soundLoc;
        private readonly float duration;
        private bool isPlaying;
        private float elapsed;

        public VoiceAudioSource(IPlayer player, AssetLocation soundLoc, float duration)
        {
            this.player = player;
            this.soundLoc = soundLoc;
            this.duration = duration;
        }

        public void Play()
        {
            if (isPlaying) return;

            float vol = RPVoiceChatModSystem.AudioSettings?.GetVolume("voice") ?? 1f;
            player.Entity.World.PlaySoundAt(soundLoc, player, null, false, 16f, vol);

            isPlaying = true;
            elapsed = 0f;
        }

        public void Update(float dt)
        {
            if (!isPlaying) return;
            elapsed += dt;
            if (elapsed >= duration) isPlaying = false;
        }

        public bool IsFinished() => !isPlaying;
    }
}