using System;

namespace RPVoiceChat.Audio.Effects
{
    /// <summary>State belongs to one playback stream. Processing must preserve the PCM sample count.</summary>
    internal interface IPcmVoiceProcessor
    {
        void Process(Span<short> samples, int sampleRate, int channels, float strength, float mixScale);
        void Reset();
    }

    internal sealed class PcmHistory
    {
        private float[] samples = Array.Empty<float>();
        private int frames, channels, sampleRate;
        public long Position { get; private set; }

        public bool Configure(int rate, int channelCount)
        {
            if (sampleRate == rate && channels == channelCount) return false;
            sampleRate = rate;
            channels = channelCount;
            frames = rate * 150 / 1000 + 2;
            samples = new float[frames * channels];
            Position = 0;
            return true;
        }

        public void Write(int channel, float sample) => samples[(int)(Position % frames) * channels + channel] = sample;
        public void Advance() => Position++;

        public float Read(double position, int channel)
        {
            long frame = (long)Math.Floor(position);
            if (frame < Math.Max(0, Position - frames + 1) || frame + 1 > Position) return 0;
            float a = samples[(int)(frame % frames) * channels + channel];
            float b = samples[(int)((frame + 1) % frames) * channels + channel];
            return a + (b - a) * (float)(position - frame);
        }

        public void Reset() { Array.Clear(samples); Position = 0; }
    }

    internal sealed class VoiceEffectRandom
    {
        private uint state;
        public VoiceEffectRandom(uint seed) { state = seed == 0 ? 1u : seed; }
        public float Next()
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state >> 8) / 16777216f;
        }
    }
}
