using OpenTK.Audio.OpenAL;
using System;

namespace RPVoiceChat.Utils
{
    public static class AudioUtils
    {
        public static int ChannelsPerFormat(ALFormat format)
        {
            switch (format)
            {
                case ALFormat.Mono16:
                    return 1;
                case ALFormat.MultiQuad16Ext:
                    return 4;
                default:
                    throw new NotSupportedException($"Format {format} is not supported for capture");
            }
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
    }
}