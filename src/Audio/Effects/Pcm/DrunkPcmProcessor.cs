using System;

namespace RPVoiceChat.Audio.Effects
{
    /// <summary>A fractional-delay double softens speech without changing the output clock.</summary>
    internal sealed class DrunkPcmProcessor : IPcmVoiceProcessor
    {
        private readonly PcmHistory history = new();
        private readonly VoiceEffectRandom random;
        private float[] lowpass = Array.Empty<float>();
        private float amount, wander, wanderTarget;
        private int wanderCountdown;
        private double phase;

        public DrunkPcmProcessor(uint seed) { random = new VoiceEffectRandom(seed); }

        public void Process(Span<short> samples, int sampleRate, int channels, float strength, float mixScale)
        {
            if (strength <= 0 && amount < 0.0001f) { if (amount != 0) Reset(); return; }
            if (history.Configure(sampleRate, channels)) { lowpass = new float[channels]; Reset(); }
            float ramp = 1f - MathF.Exp(-1f / (sampleRate * 0.1f));
            float wanderRamp = 1f - MathF.Exp(-1f / (sampleRate * 0.3f));
            float cutoff = 7500 - 3500 * strength;
            float filter = 1f - MathF.Exp(-2 * MathF.PI * Math.Min(cutoff, sampleRate * 0.4f) / sampleRate);
            for (int offset = 0; offset < samples.Length; offset += channels)
            {
                amount += (strength - amount) * ramp;
                if (--wanderCountdown <= 0)
                {
                    wanderTarget = random.Next() * 2 - 1;
                    wanderCountdown = sampleRate / 2;
                }
                wander += (wanderTarget - wander) * wanderRamp;
                double delay = (12 + amount * (4 * Math.Sin(phase) + 2 * wander)) * sampleRate / 1000;
                float wet = 0.25f * amount * mixScale;
                // At most 1dB of sway. Peak gain never exceeds unity.
                float sway = MathF.Pow(10, amount * ((float)Math.Sin(phase * 0.43) - 1) / 40);
                for (int channel = 0; channel < channels; channel++)
                {
                    float dry = samples[offset + channel] / 32768f;
                    history.Write(channel, dry);
                    float delayed = history.Read(history.Position - delay, channel);
                    lowpass[channel] += filter * (delayed - lowpass[channel]);
                    float output = ((1 - wet) * dry + wet * lowpass[channel]) * sway;
                    samples[offset + channel] = (short)Math.Clamp((int)MathF.Round(output * 32768), short.MinValue, short.MaxValue);
                }
                history.Advance();
                phase += 2 * Math.PI * 0.75 / sampleRate;
                if (phase > 200 * Math.PI) phase -= 200 * Math.PI;
            }
        }

        public void Reset()
        {
            history.Reset(); Array.Clear(lowpass);
            amount = wander = wanderTarget = 0; wanderCountdown = 0; phase = 0;
        }
    }
}
