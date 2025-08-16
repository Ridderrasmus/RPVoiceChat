using System;
using System.IO;
using Concentus;
using Concentus.Enums;
using Concentus.Native; // nécessaire pour OpusCodecFactory
using RPVoiceChat.Utils;

namespace RPVoiceChat.Audio
{
    public class OpusCodec : IAudioCodec
    {
        public const string _Name = "Opus";
        public string Name { get; } = _Name;
        public int SampleRate { get; }
        public int Channels { get; }
        public int FrameSize { get; }
        private IOpusEncoder encoder;
        private IOpusDecoder decoder;

        public OpusCodec(int frequency, int channelCount)
        {
            SampleRate = frequency;
            Channels = channelCount;
            FrameSize = SampleRate / 100 * Channels; // 10ms frame window

            encoder = OpusCodecFactory.CreateEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
            decoder = OpusCodecFactory.CreateDecoder(SampleRate, Channels);

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
                    var pcmSpan = new Span<short>(pcmData, pcmOffset, FrameSize);
                    var encodedSpan = new Span<byte>(encodedBuffer);

                    int encodedLength = encoder.Encode(pcmSpan, FrameSize / Channels, encodedSpan, encodedBuffer.Length);

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

                        var encodedSpan = new ReadOnlySpan<byte>(encodedPacket);
                        var decodedSpan = new Span<short>(decodedBuffer);

                        int decodedLength = decoder.Decode(encodedSpan, decodedSpan, FrameSize / Channels, false);
                        byte[] decodedPacket = AudioUtils.ShortsToBytes(decodedBuffer, 0, decodedLength);

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
    }
}
