namespace RPVoiceChat.Audio.Effects
{
    public class EffectTemplate
    {
        private int source;
        public int filter;

        public bool IsEnabled { get; set; } = false;

        public EffectTemplate(int source)
        {
            this.source = source;
            GenerateEffect();
        }

        private void GenerateEffect()
        {

        }
    }
}
