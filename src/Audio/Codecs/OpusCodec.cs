using System;
using System.IO;
using Concentus;
using Concentus.Enums;
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

        // Default bitrates
        private int NormalBitrate => ServerConfigManager.NormalBitrate; // 40 kbps
        private int BroadcastBitrate => ServerConfigManager.BroadcastBitrate; // 16 kbps 

        public OpusCodec(int frequency, int channelCount)
        {
            SampleRate = frequency;
            Channels = channelCount;
            FrameSize = SampleRate / 100 * Channels; // 10ms frame window

            encoder = OpusCodecFactory.CreateEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
            decoder = OpusCodecFactory.CreateDecoder(SampleRate, Channels);

            SetNormalQuality();
        }

        private void SetNormalQuality()
        {
            encoder.Bitrate = NormalBitrate;
            encoder.Complexity = 10;
            encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
            encoder.ForceMode = OpusMode.MODE_SILK_ONLY;
            encoder.UseDTX = false;
            encoder.UseInbandFEC = false;
            encoder.UseVBR = true;
        }

        private void SetBroadcastQuality()
        {
            encoder.Bitrate = BroadcastBitrate;
            encoder.Complexity = 5; // Reduce complexity to improve performance
            encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
            encoder.ForceMode = OpusMode.MODE_SILK_ONLY;
            encoder.UseDTX = true; // Enable DTX to save bandwidth (Discontinuous transmission)
            encoder.UseInbandFEC = false;
            encoder.UseVBR = true; // Variable bitrate
        }

        public byte[] Encode(short[] pcmData)
        {
            return EncodeInternal(pcmData, false);
        }

        public byte[] EncodeForBroadcast(short[] pcmData)
        {
            return EncodeInternal(pcmData, true);
        }

        private byte[] EncodeInternal(short[] pcmData, bool isBroadcast)
        {
            // Apply appropriate quality settings
            if (isBroadcast)
                SetBroadcastQuality();
            else
                SetNormalQuality();

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
