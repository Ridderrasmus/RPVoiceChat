using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RPVoiceChat.Audio
{
    public class AudioSourceManager
    {
        private readonly List<IAudioSource> audioSources = new();
        private readonly ICoreAPI api;

        public AudioSourceManager(ICoreAPI api)
        {
            this.api = api;
            api.Event.RegisterGameTickListener(OnTick, 50);
        }

        public void PlayVoice(IPlayer player, AssetLocation soundLoc, float duration)
        {
            var src = new VoiceAudioSource(player, soundLoc, duration);
            src.Play();
            audioSources.Add(src);
        }

        private void OnTick(float dt)
        {
            for (int i = audioSources.Count - 1; i >= 0; i--)
            {
                var src = audioSources[i];
                src.Update(dt);
                if (src.IsFinished()) audioSources.RemoveAt(i);
            }
        }
    }
}