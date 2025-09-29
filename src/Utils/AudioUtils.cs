using OpenTK.Audio.OpenAL;
using System;

namespace RPVoiceChat.Utils
{
    public static class AudioUtils
    {
        public static int ChannelsPerFormat(ALFormat format)
        {
            int channels = format switch
            {
                ALFormat.Mono16 => 1,
                ALFormat.Stereo16 => 2,
                ALFormat.MultiQuad16Ext => 4,
                _ => throw new NotSupportedException($"Format {format} is not supported for capture")
            };

            return channels;
        }

        public static byte[] ShortsToBytes(short[] audio, int offset, int length)
        {
            byte[] byteBuffer = new byte[length * sizeof(short)];
            int bytesToCopy = (length - offset) * sizeof(short);
            Buffer.BlockCopy(audio, offset, byteBuffer, offset * sizeof(short), bytesToCopy);

            return byteBuffer;
        }

        public static float DBsToFactor(float dB)
        {
            return (float)Math.Pow(10, dB / 20);
        }

        public static float FactorToDBs(float factor)
        {
            return (float)Math.Log10(factor) * 20;
        }

        public static void FadeEdges(byte[] data, int maxFadeDuration)
        {
            if (data == null || data.Length == 0)
                return;

            int sampleCount = data.Length / sizeof(short);
            // S'assurer que le fade ne dépasse pas la moitié des données disponibles
            int safeFadeDuration = Math.Min(maxFadeDuration, sampleCount / 2);

            if (safeFadeDuration <= 0)
                return;

            FadeEdge(data, safeFadeDuration, false);
            FadeEdge(data, safeFadeDuration, true);
        }

        public static void FadeEdge(byte[] data, int maxFadeDuration, bool isRightEdge)
        {
            if (data == null || data.Length < sizeof(short))
                return;

            int sampleCount = data.Length / sizeof(short);

            // Protection : ensure that maxFadeDuration does not exceed the number of samples.
            if (maxFadeDuration > sampleCount)
            {
                maxFadeDuration = sampleCount;
            }

            int startIndex = 0;
            int endIndex = maxFadeDuration;
            TransformDelegate transform = e => e;

            if (isRightEdge)
            {
                startIndex = Math.Max(0, sampleCount - maxFadeDuration);
                endIndex = sampleCount - 1;
                transform = e => 1 - e;
            }

            // Final security check
            if (startIndex < 0 || startIndex >= sampleCount || endIndex >= sampleCount || startIndex >= endIndex)
            {
                Logger.client.Warning($"Invalid fade indices: start={startIndex}, end={endIndex}, sampleCount={sampleCount}");
                return;
            }

            int zeroCrossingIndex = -1;
            double? lastPcm = null;

            for (var i = startIndex; i < endIndex; i++)
            {
                // Additional protection when accessing bytes
                int byteIndex = i * sizeof(short);
                if (byteIndex + sizeof(short) > data.Length)
                    break;

                var pcm = (double)BitConverter.ToInt16(data, byteIndex);

                if (lastPcm == null || pcm * lastPcm > 0)
                {
                    lastPcm = pcm;
                    continue;
                }

                zeroCrossingIndex = i;
                if (!isRightEdge) break;
                lastPcm = pcm;
                continue;
            }

            if (zeroCrossingIndex == -1)
            {
                Fade(data, startIndex, endIndex, transform);
                return;
            }

            var silenceFrom = 0;
            var silenceTo = zeroCrossingIndex * sizeof(short);

            if (isRightEdge)
            {
                silenceFrom = zeroCrossingIndex * sizeof(short);
                silenceTo = sampleCount * sizeof(short);
            }

            // Protection : verify that the silence indices are valid
            if (silenceFrom < 0 || silenceTo > data.Length || silenceFrom >= silenceTo)
                return;

            var silenceBytes = silenceTo - silenceFrom;
            var silence = new byte[silenceBytes];
            Buffer.BlockCopy(silence, 0, data, silenceFrom, silenceBytes);
        }

        /// <summary>
        /// A function used to describe percentage change of some value
        /// </summary>
        /// <param name="progress">Normalized(percentage) distance in the interval with 0 corresponding to the first sample and 1 to last sample</param>
        /// <returns>Factor that an audio sample should be adjusted by</returns>
        public delegate double TransformDelegate(double progress);

        /// <summary>
        /// Iterates over specified closed interval and multiplies every value with the result of transform function
        /// </summary>
        /// <param name="data">Raw, unencoded, mono-channel PCM data</param>
        /// <param name="startIndex">Index of a sample(not the byte) to start fading from</param>
        /// <param name="endIndex">Index of a last sample(not the byte) that will be affected by fade</param>
        /// <param name="transform">A <see cref="TransformDelegate">TransformDelegate</see> that will be used to adjust fading speed or direction</param>
        public static void Fade(byte[] data, int startIndex, int endIndex, TransformDelegate transform)
        {
            int fadeDuration = endIndex - startIndex;
            short[] pcmBuffer = new short[fadeDuration + 1];
            for (var i = startIndex; i <= endIndex; i++)
            {
                var pcm = (double)BitConverter.ToInt16(data, i * sizeof(short));
                var pcmIndex = i - startIndex;
                var fadeMultiplier = (double)pcmIndex / fadeDuration;
                pcm *= transform(fadeMultiplier);
                pcmBuffer[pcmIndex] = (short)pcm;
            }
            Buffer.BlockCopy(pcmBuffer, 0, data, startIndex * sizeof(short), pcmBuffer.Length * sizeof(short));
        }
    }
}