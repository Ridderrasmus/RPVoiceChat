using Cairo;
using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio.Effects
{
    public abstract class SoundEffect
    {
        protected int source;
        protected int effect;
        protected int slot;
        private int nullEffect;

        public bool IsEnabled { get; set; } = false;

        protected SoundEffect(int source)
        {
            this.source = source;
            this.nullEffect = ALC.EFX.GenEffect();
            GenerateEffect();
        }

        public void Start()
        {
            if (IsEnabled)
                return;

            ALC.EFX.Source(source, EFXSourceInteger3.AuxiliarySendFilter, slot, 0, 0);
            IsEnabled = true;
        }

        public void Stop()
        {
            if (!IsEnabled)
                return;

            ALC.EFX.Source(source, EFXSourceInteger3.AuxiliarySendFilter, nullEffect, 0, 0);
            IsEnabled = false;
        }

        protected abstract void GenerateEffect();
    }
}
