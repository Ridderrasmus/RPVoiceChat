using System;

namespace RPVoiceChat.Audio.Effects
{
    /// <summary>Brief, crossfaded history fragments over an uninterrupted dry voice.</summary>
    internal sealed class TemporalGlitchProcessor : IPcmVoiceProcessor
    {
        private readonly PcmHistory history = new();
        private readonly VoiceEffectRandom random;
        private float amount, eventStrength;
        private int cooldown, eventFrame, eventLength, fragmentLength, kind, quietFrames;
        private float eventBudget = 1;
        private double fragmentStart;

        public TemporalGlitchProcessor(uint seed) { random = new VoiceEffectRandom(seed); }

        public void Process(Span<short> samples, int sampleRate, int channels, float strength, float mixScale)
        {
            if (strength <= 0 && amount < 0.0001f) { if (amount != 0) Reset(); return; }
            if (history.Configure(sampleRate, channels)) Reset();
            float ramp = 1f - MathF.Exp(-1f / (sampleRate * 0.1f));
            int fadeFrames = Math.Max(1, sampleRate * 3 / 1000);
            for (int offset = 0; offset < samples.Length; offset += channels)
            {
                amount += (strength - amount) * ramp;
                int peak = 0;
                for (int channel = 0; channel < channels; channel++) peak = Math.Max(peak, Math.Abs((int)samples[offset + channel]));
                quietFrames = peak < 12 ? Math.Min(sampleRate / 5, quietFrames + 1) : 0;
                if (eventLength == 0 && --cooldown <= 0 && quietFrames < sampleRate / 50 && amount > 0.01f)
                {
                    eventBudget -= 3.6f * amount * amount / sampleRate;
                    if (eventBudget <= 0 && history.Position > sampleRate / 10) BeginEvent(sampleRate);
                }

                float envelope = 0;
                if (eventLength > 0)
                {
                    float edge = Math.Min(1f, Math.Min(eventFrame, eventLength - 1 - eventFrame) / (float)fadeFrames);
                    envelope = 0.5f - 0.5f * MathF.Cos(MathF.PI * Math.Max(0, edge));
                    // A looped fragment gets its own crossfade at the wrap, too.
                    if (kind == 0)
                    {
                        int fragmentFrame = eventFrame % fragmentLength;
                        float loopEdge = Math.Min(1f, Math.Min(fragmentFrame, fragmentLength - 1 - fragmentFrame) / (float)fadeFrames);
                        envelope *= 0.5f - 0.5f * MathF.Cos(MathF.PI * Math.Max(0, loopEdge));
                    }
                    envelope *= Math.Clamp(1f - (quietFrames - sampleRate * 0.04f) / (sampleRate * 0.01f), 0, 1);
                }
                float wet = envelope * (0.06f + 0.19f * eventStrength) * mixScale * Math.Min(1, amount / Math.Max(0.001f, eventStrength));
                for (int channel = 0; channel < channels; channel++)
                {
                    float dry = samples[offset + channel] / 32768f;
                    history.Write(channel, dry);
                    float corrupted = 0;
                    if (wet > 0)
                    {
                        double read = kind switch
                        {
                            0 => fragmentStart + eventFrame % fragmentLength,
                            1 => history.Position - sampleRate * (0.0125 + 0.0075 * Math.Sin(history.Position * 2 * Math.PI / sampleRate)),
                            2 => fragmentStart - eventFrame,
                            4 => fragmentStart - 1.1 * eventFrame,
                            _ => history.Position - sampleRate * 0.015
                        };
                        corrupted = history.Read(read, channel);
                        if (kind == 3)
                        {
                            float levels = 1 << (int)(11 - 4 * eventStrength);
                            corrupted = MathF.Round(corrupted * levels) / levels;
                        }
                        if (kind == 5) corrupted *= (float)Math.Sin(history.Position * 2 * Math.PI * 33 / sampleRate);
                    }
                    // Wet is capped at .25; current speech is never paused, repeated or reordered.
                    float output = dry * (1 - wet) + corrupted * wet;
                    samples[offset + channel] = (short)Math.Clamp((int)MathF.Round(output * 32768), short.MinValue, short.MaxValue);
                }
                history.Advance();
                if (eventLength > 0 && ++eventFrame >= eventLength)
                {
                    eventLength = 0;
                    // Around 10% wet duty cycle at maximum severity, with at least 120ms of dry speech between events.
                    cooldown = sampleRate * 120 / 1000;
                    eventBudget = 0.5f + random.Next();
                }
                if (quietFrames == sampleRate * 150 / 1000)
                {
                    history.Reset(); eventLength = 0;
                }
            }
        }

        private void BeginEvent(int sampleRate)
        {
            eventStrength = amount;
            kind = (int)(random.Next() * (amount < 0.5f ? 2 : amount < 0.8f ? 5 : 6));
            float duration = Math.Min(kind == 2 ? 25 : kind == 3 ? 35 : 45, 12 + 33 * amount);
            eventLength = Math.Max(1, (int)(sampleRate * duration / 1000));
            fragmentLength = Math.Max(1, (int)(sampleRate * (12 + 28 * random.Next()) / 1000));
            fragmentStart = history.Position - sampleRate * 0.05;
            eventFrame = 0;
        }

        public void Reset()
        {
            history.Reset(); amount = eventStrength = 0;
            cooldown = eventFrame = eventLength = quietFrames = 0;
            eventBudget = 0.5f + random.Next();
        }
    }
}
