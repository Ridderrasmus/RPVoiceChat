using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Text;

namespace RPVoiceChat
{
    public class OpusHandler
    {
        private OpusEncoder encoder;
        private OpusDecoder decoder;
        private int frameSize;
        private int sampleRate;
        private int channels;
        private int maxPacketSize;
        private byte[] encodedData;

        public OpusHandler(int sampleRate, int channels)
        {
            this.frameSize = 960;
            this.sampleRate = sampleRate;
            this.channels = channels;
            maxPacketSize = 4000;
            encodedData = new byte[maxPacketSize];

            encoder = new OpusEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
            decoder = new OpusDecoder(sampleRate, channels);

            encoder.Bitrate = 16000;
        }

        public byte[] Encode(short[] pcmData)
        {
            int encodedLength = encoder.Encode(pcmData, 0, frameSize, encodedData, 0, maxPacketSize);
            byte[] encoded = new byte[encodedLength];
            Array.Copy(encodedData, encoded, encodedLength);
            return encoded;
        }

        public short[] Decode(byte[] encodedData)
        {
            short[] decodedData = new short[maxPacketSize];
            int decodedLength = decoder.Decode(encodedData, 0, encodedData.Length, decodedData, 0, frameSize, false);
            short[] decoded = new short[decodedLength];
            Array.Copy(decodedData, decoded, decodedLength);
            return decoded;
        }

        public void SetBitrate(int bitrate)
        {
            encoder.Bitrate = bitrate;
        }
    }
}
