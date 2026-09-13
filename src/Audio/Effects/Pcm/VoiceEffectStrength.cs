using System;

namespace RPVoiceChat.Audio.Effects
{
    internal static class VoiceEffectStrength
    {
        public static float Drunk(float intoxication)
        {
            if (!float.IsFinite(intoxication)) return 0;
            float t = Math.Clamp((intoxication / 1.1f - 0.15f) / 0.75f, 0, 1);
            return t * t * (3 - 2 * t);
        }

        public static float Temporal(double stability, bool enabled = true)
            => enabled && double.IsFinite(stability) ? (float)Math.Clamp((0.2 - stability) / 0.2, 0, 1) : 0;

        public static float Sanitize(float strength) => float.IsFinite(strength) ? Math.Clamp(strength, 0, 1) : 0;
    }
}
