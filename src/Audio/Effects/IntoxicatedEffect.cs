using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Audio.OpenAL;
using RPVoiceChat.Audio;
using RPVoiceChat.Audio.Effects;

namespace RPVoiceChat.src.Audio.Effects
{
    public class IntoxicatedEffect : SoundEffect
    {
        protected float toxicRate;

        public IntoxicatedEffect(int source) : base(source) {}

        protected override void GenerateEffect()
        {
            float pitch = toxicRate <= 0.2 ? 1 : 1 - (toxicRate / 5);
            OALW.Source(source, ALSourcef.Pitch, pitch);
        }

        public void SetToxicRate(float toxicRate)
        {
            this.toxicRate = toxicRate;
        }
    }
}
