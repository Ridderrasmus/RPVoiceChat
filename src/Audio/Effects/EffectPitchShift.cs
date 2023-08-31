using OpenTK;
using OpenTK.Audio.OpenAL;

namespace RPVoiceChat
{
    public class EffectPitchShift
    {
        private EfxEffectType effectType = EfxEffectType.PitchShifter;
        private EffectsExtension effectsExtension;
        private int source;
        private int effect;
        private int slot;

        public bool IsEnabled { get; private set; } = false;

        public EffectPitchShift(EffectsExtension effectsExtension, int source)
        {
            this.effectsExtension = effectsExtension;
            this.source = source;

            GenerateEffect();
        }

        public void Start()
        {
            if (IsEnabled) return;
            IsEnabled = true;

            effectsExtension.BindEffect(effect, effectType);
        }

        public void Stop()
        {
            if (!IsEnabled) return;
            IsEnabled = false;

            effectsExtension.BindEffect(effect, EfxEffectType.Null);
        }

        public void SetPitchShift(int pitchshift)
        {
            SetEffectSetting(EfxEffecti.PitchShifterCoarseTune, pitchshift);
        }

        private void GenerateEffect()
        {
            effect = effectsExtension.GenEffect();
            slot = effectsExtension.GenAuxiliaryEffectSlot();

            effectsExtension.AuxiliaryEffectSlot(slot, EfxAuxiliaryi.EffectslotEffect, effect);
            effectsExtension.BindSourceToAuxiliarySlot(source, slot, 0, 0);
        }

        private void SetEffectSetting(EfxEffectf effectSetting, float param)
        {
            effectsExtension.Effect(effect, effectSetting, param);
        }

        private void SetEffectSetting(EfxEffecti effectSetting, int param)
        {
            effectsExtension.Effect(effect, effectSetting, param);
        }

        private void SetEffectSetting(EfxEffect3f effectSetting, float param1, float param2, float param3)
        {
            var params3f = new Vector3(param1, param2, param3);
            effectsExtension.Effect(effect, effectSetting, ref params3f);
        }
    }
}
