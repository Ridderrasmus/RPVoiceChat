using Concentus.Enums;
using Concentus.Structs;
using RPVoiceChat.Utils;
using System;
using System.IO;

namespace RPVoiceChat.Audio
{
    public class OpusCodec : IAudioCodec
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
            frameSize = sampleRate / 100 * channels; // 10ms frame window

            encoder = new OpusEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
            decoder = new OpusDecoder(sampleRate, channels);

            encoder.Bitrate = 40000;
            encoder.Complexity = 10;
            encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
            encoder.ForceMode = OpusMode.MODE_SILK_ONLY;
            encoder.UseDTX = false;
            encoder.UseInbandFEC = false;
            encoder.UseVBR = true;
        }

        public byte[] Encode(short[] pcmData)
        {
            const int maxPacketSize = 1276;
            var encoded = new MemoryStream();

            try
            {
                for (var pcmOffset = 0; pcmOffset < pcmData.Length; pcmOffset += frameSize)
                {
                    byte[] encodedData = new byte[maxPacketSize];
                    int encodedLength = encoder.Encode(pcmData, pcmOffset, frameSize, encodedData, 0, maxPacketSize);
                    byte[] packetSize = BitConverter.GetBytes(encodedLength);

                    encoded.Write(packetSize, 0, packetSize.Length);
                    encoded.Write(encodedData, 0, encodedLength);
                }
            }
            catch (Exception e)
            {
                Logger.client.Error($"Couldn't encode audio:\n{e}");
            }

            return encoded.ToArray();
        }

        public byte[] Decode(byte[] encodedData)
        {
            var decoded = new MemoryStream();
            var stream = new MemoryStream(encodedData);
            using (var reader = new BinaryReader(stream))
            {
                try
                {
                    while (stream.Position < stream.Length)
                    {
                        int packetSize = reader.ReadInt32();
                        byte[] encodedPacket = reader.ReadBytes(packetSize);

                        short[] decodedData = new short[frameSize];
                        int decodedLength = decoder.Decode(encodedPacket, 0, packetSize, decodedData, 0, frameSize, false);
                        byte[] decodedPacket = ToBytes(decodedData, 0, decodedLength);

                        decoded.Write(decodedPacket, 0, decodedPacket.Length);
                    }
                }
                catch (Exception e)
                {
                    Logger.client.Error($"Couldn't decode audio:\n{e}");
                }
            }

            return decoded.ToArray();
        }

        public int GetFrameSize()
        {
            return frameSize;
        }

        private byte[] ToBytes(short[] audio, int offset, int length)
        {
            int typeSize = sizeof(short);
            byte[] byteBuffer = new byte[length * typeSize];
            int bytesToCopy = (length - offset) * typeSize;
            Buffer.BlockCopy(audio, offset, byteBuffer, offset * typeSize, bytesToCopy);

            return byteBuffer;
        }
    }
}
