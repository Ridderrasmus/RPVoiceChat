using Concentus.Enums;
using Concentus.Structs;
using RPVoiceChat.Audio;
using RPVoiceChat.Utils;
using System;
using System.Text;

namespace RPVoiceChat
{
    public class OpusCodec: IAudioCodec
    {
        private OpusEncoder encoder;
        private OpusDecoder decoder;
        private int sampleRate;
        private int channels;
        private int frameSize;

        public OpusCodec(int frequency, int channelCount)
        {
            sampleRate = frequency;
            channels = channelCount;
            frameSize = sampleRate / 100 * channels;

            encoder = new OpusEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
            decoder = new OpusDecoder(sampleRate, channels);
        }

        public byte[] Encode(short[] pcmData)
        {
            byte[] encoded = new byte[0];

            try
            {
                const int maxPacketSize = 1275;
                byte[] encodedData = new byte[maxPacketSize];
                int encodedLength = encoder.Encode(pcmData, 0, frameSize, encodedData, 0, maxPacketSize);
                encoded = new byte[encodedLength];
                Array.Copy(encodedData, encoded, encodedLength);
            }
            catch(Exception e)
            {
                Logger.client.Error($"Couldn't encode audio:\n{e}");
            }

            return encoded;
        }

        public byte[] Decode(byte[] encodedData)
        {
            int maxPacketSize = OpusPacketInfo.GetNumSamples(encodedData, 0, encodedData.Length, sampleRate);
            short[] decodedData = new short[maxPacketSize];
            int decodedLength = decoder.Decode(encodedData, 0, encodedData.Length, decodedData, 0, frameSize, false);
            short[] decoded = new short[decodedLength];
            Array.Copy(decodedData, decoded, decodedLength);

            return ToBytes(decoded);
        }

        private byte[] ToBytes(short[] audio)
        {
            byte[] byteBuffer = new byte[audio.Length * 2];
            for (int i = 0; i < audio.Length; i++)
            {
                short val = (short)(audio[i] * short.MaxValue);
                byteBuffer[i * 2] = (byte)(val & 0xFF);
                byteBuffer[i * 2 + 1] = (byte)(val >> 8);
            }

            return byteBuffer;
        }
    }
}
