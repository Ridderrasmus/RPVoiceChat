using System;

namespace RPVoiceChat.Systems
{
    public static class RadioProgramMixer
    {
        public const float MusicGain = 0.7f;
        public const float MicGain = 1.0f;
        public const float DuckMusicGain = 0.1f;
        public const double MicActivityThreshold = 0.012;

        public static void MixFrames(ReadOnlySpan<short> music, ReadOnlySpan<short> mic, Span<short> output)
        {
            int count = Math.Min(Math.Min(music.Length, mic.Length), output.Length);
            double micRms = ComputeRms(mic, count);
            float musicMultiplier = micRms >= MicActivityThreshold ? DuckMusicGain : 1f;

            for (int i = 0; i < count; i++)
            {
                float mixed = music[i] * MusicGain * musicMultiplier + mic[i] * MicGain;
                output[i] = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
            }
        }

        public static double ComputeRms(ReadOnlySpan<short> samples, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                double normalized = samples[i] / (double)short.MaxValue;
                sum += normalized * normalized;
            }

            return Math.Sqrt(sum / count);
        }
    }
}
