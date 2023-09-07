using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Text;

namespace RPVoiceChat
{
    public static class OpusHandler
    {
        private static OpusEncoder encoder;
        private static OpusDecoder decoder;
        private static int frameSize;
        private static int sampleRate;
        private static int channels;
        private static int maxPacketSize;

        static OpusHandler()
        {
            sampleRate = MicrophoneManager.Frequency;
            channels = 1;
            frameSize = 960;
            maxPacketSize = 4000;

            encoder = new OpusEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
            decoder = new OpusDecoder(sampleRate, channels);

            encoder.Bitrate = 16000;
        }

        public static byte[] Encode(short[] pcmData)
        {
            byte[] encodedData = new byte[maxPacketSize];
            int encodedLength = encoder.Encode(pcmData, 0, frameSize, encodedData, 0, maxPacketSize);
            byte[] encoded = new byte[encodedLength];
            Array.Copy(encodedData, encoded, encodedLength);
            return encoded;
        }

        public static short[] Decode(byte[] encodedData)
        {
            short[] decodedData = new short[maxPacketSize];
            int decodedLength = decoder.Decode(encodedData, 0, encodedData.Length, decodedData, 0, frameSize, false);
            short[] decoded = new short[decodedLength];
            Array.Copy(decodedData, decoded, decodedLength);
            return decoded;
        }

        public static void SetBitrate(int bitrate)
        {
            encoder.Bitrate = bitrate;
        }
    }
}
