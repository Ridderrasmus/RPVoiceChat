using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio.Effects
{
    public class ReverbEffect
    {
        private int effect;
        private int slot;
        private EffectsExtension efx;
        public ReverbEffect(EffectsExtension efx, int source)
        {

            this.efx = efx;
            effect = efx.GenEffect();
            slot = efx.GenAuxiliaryEffectSlot();

            efx.BindEffect(effect, EfxEffectType.Reverb);
            efx.Effect(effect, EfxEffectf.ReverbDecayTime, 3.0f);
            efx.Effect(effect, EfxEffectf.ReverbDecayHFRatio, 0.91f);
            efx.Effect(effect, EfxEffectf.ReverbDensity, 0.7f);
            efx.Effect(effect, EfxEffectf.ReverbDiffusion, 0.9f);
            efx.Effect(effect, EfxEffectf.ReverbRoomRolloffFactor, 3.1f);
            efx.Effect(effect, EfxEffectf.ReverbReflectionsGain, 0.723f);
            efx.Effect(effect, EfxEffectf.ReverbReflectionsDelay, 0.03f);
            efx.Effect(effect, EfxEffectf.ReverbGain, 0.23f);

            efx.AuxiliaryEffectSlot(slot, EfxAuxiliaryi.EffectslotEffect, effect);
            efx.BindSourceToAuxiliarySlot(source, slot, 0, 0);
        }
    }
}
