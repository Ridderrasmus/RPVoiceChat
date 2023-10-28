using Concentus.Enums;
using Concentus.Structs;
using RPVoiceChat.Utils;
using System;
using System.IO;

namespace RPVoiceChat.Audio
{
    public class OpusCodec : IAudioCodec
    {
        public int SampleRate { get; }
        public int Channels { get; }
        public int FrameSize { get; }
        private OpusEncoder encoder;
        private OpusDecoder decoder;

        public OpusCodec(int frequency, int channelCount)
        {
            SampleRate = frequency;
            Channels = channelCount;
            FrameSize = SampleRate / 100 * Channels; // 10ms frame window

            encoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
            decoder = new OpusDecoder(SampleRate, Channels);

            encoder.Bitrate = 40 * 1024;
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
            byte[] encodedBuffer = new byte[maxPacketSize];
            var encoded = new MemoryStream();
            try
            {
                for (var pcmOffset = 0; pcmOffset < pcmData.Length; pcmOffset += FrameSize)
                {
                    int encodedLength = encoder.Encode(pcmData, pcmOffset, FrameSize, encodedBuffer, 0, maxPacketSize);
                    byte[] packetSize = BitConverter.GetBytes(encodedLength);

                    encoded.Write(packetSize, 0, packetSize.Length);
                    encoded.Write(encodedBuffer, 0, encodedLength);
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
            short[] decodedBuffer = new short[FrameSize];
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

                        int decodedLength = decoder.Decode(encodedPacket, 0, packetSize, decodedBuffer, 0, FrameSize, false);
                        byte[] decodedPacket = ShortsToBytes(decodedBuffer, 0, decodedLength);

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

        private byte[] ShortsToBytes(short[] audio, int offset, int length)
        {
            byte[] byteBuffer = new byte[length * sizeof(short)];
            int bytesToCopy = (length - offset) * sizeof(short);
            Buffer.BlockCopy(audio, offset, byteBuffer, offset * sizeof(short), bytesToCopy);

            return byteBuffer;
        }
    }
}
