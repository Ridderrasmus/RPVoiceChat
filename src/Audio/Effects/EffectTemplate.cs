using OpenTK.Audio.OpenAL;

namespace RPVoiceChat
{
    public class EffectTemplate
    {
        private EffectsExtension effectsExtension;
        private int source;
        public int filter;

        public bool IsEnabled { get; set; } = false;

        public EffectTemplate(EffectsExtension effectsExtension, int source)
        {
            this.source = source;
            this.effectsExtension = effectsExtension;
            GenerateEffect();
        }

        private void GenerateEffect()
        {

        }
    }
}
