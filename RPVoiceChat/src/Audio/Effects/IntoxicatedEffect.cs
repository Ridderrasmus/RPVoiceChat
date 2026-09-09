using System;
using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio.Effects
{
    public class IntoxicatedEffect : SoundEffect
    {
        protected float toxicRate;

        public IntoxicatedEffect(int source) : base("intoxicated", source) { }

        protected override int GenerateEffect()
        {
            // WARNING : changing pitch also changes audio playback speed, causing overflow of circular audio buffer.
            // More advanced implementation is required before using this effect
            float pitch = toxicRate <= 0.2 ? 1 : 1 - (toxicRate / 5);
            OALW.Source(source, ALSourcef.Pitch, pitch);

            throw new NotImplementedException();
        }

        public void SetToxicRate(float toxicRate)
        {
            this.toxicRate = toxicRate;
        }
    }
}
