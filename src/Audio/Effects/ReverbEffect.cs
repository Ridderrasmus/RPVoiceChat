using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio.Effects
{
    public class ReverbEffect : SoundEffect
    {
        public ReverbEffect(int source) : base("reverb", source) { }

        protected override int GenerateEffect()
        {
            effect = ALC.EFX.GenEffect();
            slot = ALC.EFX.GenAuxiliaryEffectSlot();

            ALC.EFX.Effect(effect, EffectInteger.EffectType, (int)EffectType.Reverb);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDecayTime, 3.0f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDecayHFRatio, 0.91f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDensity, 0.7f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDiffusion, 0.9f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbRoomRolloffFactor, 3.1f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbReflectionsGain, 0.723f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbReflectionsDelay, 0.03f);
            ALC.EFX.Effect(effect, EffectFloat.ReverbGain, 0.23f);

            ALC.EFX.AuxiliaryEffectSlot(slot, EffectSlotInteger.Effect, effect);

            return effect;
        }
    }
}
