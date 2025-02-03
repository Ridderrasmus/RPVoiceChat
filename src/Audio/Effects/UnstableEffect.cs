using System;
using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio.Effects
{
    public class UnstableEffect : SoundEffect
    {
        public UnstableEffect(int source) : base(source) { }

        protected override int GenerateEffect()
        {
            throw new NotImplementedException();
        }
    }
}
