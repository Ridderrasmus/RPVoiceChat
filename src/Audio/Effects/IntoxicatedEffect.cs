using System;
using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio.Effects
{
    public class IntoxicatedEffect : SoundEffect
    {
        protected float toxicRate;

        public IntoxicatedEffect(int source) : base(source) { }

        protected override int GenerateEffect()
        {
            int effect = ALC.EFX.GenEffect();

            ALC.EFX.Effect(effect, EffectInteger.EffectType, 5);

            // Adjust pitch according to toxicRate (more toxic = greater shift)
            // CoarseTune = semitones, FineTune = cents
            float coarseTune = (toxicRate - 0.5f) * 12f;  // roughly from -6 to +6 semitones
            float fineTune = (toxicRate - 0.5f) * 100f;   // roughly from -50 to +50 cents

            ALC.EFX.Effect(effect, EffectInteger.PitchShifterCoarseTune, (int)coarseTune);
            ALC.EFX.Effect(effect, EffectInteger.PitchShifterFineTune, (int)fineTune);

            slot = ALC.EFX.GenAuxiliaryEffectSlot();
            ALC.EFX.AuxiliaryEffectSlot(slot, EffectSlotInteger.Effect, effect);

            this.effect = effect;
            return effect;
        }

        public void SetToxicRate(float toxicRate)
        {
            this.toxicRate = toxicRate;
        }
    }
}
