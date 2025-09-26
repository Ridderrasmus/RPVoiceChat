using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio.Effects
{
    public class CheapMicEffect : SoundEffect
    {
        private int lowpassFilter = 0;

        public CheapMicEffect(int source) : base("cheapmic", source) { }

        protected override int GenerateEffect()
        {
            // Création de l'effet de réverbération
            effect = ALC.EFX.GenEffect();
            slot = ALC.EFX.GenAuxiliaryEffectSlot();

            ALC.EFX.Effect(effect, EffectInteger.EffectType, (int)EffectType.Reverb);

            ALC.EFX.Effect(effect, EffectFloat.ReverbDecayTime, 1.2f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDecayHFRatio, 0.3f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDensity, 0.2f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDiffusion, 0.2f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbRoomRolloffFactor, 0.5f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbReflectionsGain, 0.4f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbReflectionsDelay, 0.01f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbGain, 0.5f);

            ALC.EFX.AuxiliaryEffectSlot(slot, EffectSlotInteger.Effect, effect);

            lowpassFilter = ALC.EFX.GenFilter();
            ALC.EFX.Filter(lowpassFilter, FilterInteger.FilterType, (int)FilterType.Lowpass);

            ALC.EFX.Filter(lowpassFilter, FilterFloat.LowpassGain, 0.6f);  
            ALC.EFX.Filter(lowpassFilter, FilterFloat.LowpassGainHF, 0.2f); 

            return effect;
        }

        public override void Apply()
        {
            base.Apply();

            ALC.EFX.Source(source, EFXSourceInteger3.AuxiliarySendFilter, slot, 0, 0);

            ALC.EFX.Source(source, EFXSourceInteger.DirectFilter, lowpassFilter);
        }

        public override void Clear()
        {
            base.Clear();

            if (lowpassFilter != 0)
            {
                ALC.EFX.Source(source, EFXSourceInteger.DirectFilter, 0);

                ALC.EFX.DeleteFilter(lowpassFilter);
                lowpassFilter = 0;
            }
        }
    }
}
