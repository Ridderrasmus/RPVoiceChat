using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RPVoiceChat.Audio.Effects;

namespace RPVoiceChat.src.Audio.Effects
{
    public class UnstableEffect : SoundEffect
    {
        public UnstableEffect(int source) : base(source) { }

        protected override void GenerateEffect()
        {
        }
    }
}
