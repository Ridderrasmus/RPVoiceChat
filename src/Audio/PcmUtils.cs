using NVorbis;
using System;
using System.IO;
using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio
{
    public static class PcmUtils
    {
        public static void ApplyGainWithSoftClipping(ref byte[] data, ALFormat format, float gain)
        {
            if (gain <= 1.0f) return;
            if (data == null || data.Length < 2) return;

            int channels = format switch
            {
                ALFormat.Mono16 => 1,
                ALFormat.Stereo16 => 2,
                _ => throw new NotSupportedException($"Unsupported format: {format}")
            };

            for (int i = 0; i < data.Length; i += 2)
            {
                short sample = (short)(data[i] | (data[i + 1] << 8));
                float amplified = sample * gain;

                // Soft clipping
                float clipped = SoftClip(amplified);

                short result = (short)Math.Clamp(clipped, short.MinValue, short.MaxValue);
                data[i] = (byte)(result & 0xFF);
                data[i + 1] = (byte)((result >> 8) & 0xFF);
            }
        }

        public static void ApplyCompressor(ref byte[] data, ALFormat format, float threshold = 0.6f, float ratio = 4.0f)
        {
            if (data == null || data.Length < 2) return;

            int channels = format switch
            {
                ALFormat.Mono16 => 1,
                ALFormat.Stereo16 => 2,
                _ => throw new NotSupportedException($"Unsupported format: {format}")
            };

            for (int i = 0; i < data.Length; i += 2)
            {
                short sample = (short)(data[i] | (data[i + 1] << 8));
                float normalized = sample / 32768f;

                float compressed = CompressSample(normalized, threshold, ratio);
                short result = (short)Math.Clamp(compressed * 32768f, short.MinValue, short.MaxValue);

                data[i] = (byte)(result & 0xFF);
                data[i + 1] = (byte)((result >> 8) & 0xFF);
            }
        }

        public static byte[] LoadOggToPCM(Stream stream, out int sampleRate, out int channels)
        {
            using var vorbis = new VorbisReader(stream, false);

            sampleRate = vorbis.SampleRate;
            channels = vorbis.Channels;

            float[] buffer = new float[vorbis.TotalSamples * channels];
            int samplesRead = vorbis.ReadSamples(buffer, 0, buffer.Length);

            byte[] pcmData = new byte[samplesRead * sizeof(short)];
            for (int i = 0; i < samplesRead; i++)
            {
                short sample = (short)Math.Clamp(buffer[i] * short.MaxValue, short.MinValue, short.MaxValue);
                pcmData[i * 2] = (byte)(sample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return pcmData;
        }

        private static float SoftClip(float x)
        {
            float norm = x / 32768f;

            if (Math.Abs(norm) < 0.95f)
                return x; // no need to clip

            // Soft clipping (sinusoidal fold)
            float clipped = (float)Math.Sin(norm * Math.PI / 2.0f);
            return clipped * 32768f;
        }

        private static float CompressSample(float sample, float threshold, float ratio)
        {
            float abs = Math.Abs(sample);
            if (abs < threshold) return sample;

            float excess = abs - threshold;
            float compressed = threshold + excess / ratio;
            return Math.Sign(sample) * compressed;
        }
    }
}