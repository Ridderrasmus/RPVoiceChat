using System;
using System.IO;
using Concentus;
using Concentus.Enums;
using RPVoiceChat.Config;
using RPVoiceChat.Util;

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
        
        // Track last broadcast mode to avoid unnecessary parameter changes
        private bool lastWasBroadcast = false;

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
            // Only change encoder parameters when broadcast mode actually changes
            // This prevents unnecessary parameter changes that cause CPU spikes
            if (lastWasBroadcast != isBroadcast)
            {
                if (isBroadcast)
                    SetBroadcastQuality();
                else
                    SetNormalQuality();
                lastWasBroadcast = isBroadcast;
            }

            const int maxPacketSize = 1276;
            byte[] encodedBuffer = new byte[maxPacketSize];
            using var encoded = new MemoryStream();

            try
            {
                for (var pcmOffset = 0; pcmOffset < pcmData.Length; pcmOffset += FrameSize)
                {
                    // Check if we have enough samples for a complete frame
                    if (pcmOffset + FrameSize > pcmData.Length)
                        break;
                    
                    var pcmSpan = new Span<short>(pcmData, pcmOffset, FrameSize);
                    var encodedSpan = new Span<byte>(encodedBuffer);
                    int encodedLength = encoder.Encode(pcmSpan, FrameSize / Channels, encodedSpan, encodedBuffer.Length);

                    // Skip frames with 0 length (DTX silence) to avoid sending empty packets
                    // Empty packets can cause client crashes or disconnections
                    if (encodedLength <= 0)
                        continue;

                    byte[] packetSize = BitConverter.GetBytes(encodedLength);
                    encoded.Write(packetSize, 0, packetSize.Length);
                    encoded.Write(encodedBuffer, 0, encodedLength);
                }
            }
            catch (Exception e)
            {
                Logger.client.Error($"Couldn't encode audio:\n{e}");
            }

            byte[] result = encoded.ToArray();
            
            // If all frames were DTX (silence), return empty array to avoid sending malformed packets
            // This prevents client crashes when receiving empty or malformed broadcast packets
            if (result.Length == 0)
            {
                return new byte[0];
            }

            return result;
        }

        public byte[] Decode(byte[] encodedData)
        {
            if (encodedData == null || encodedData.Length == 0)
                return new byte[0];

            const int maxPacketSize = 1276; // Same as in Encode
            const int packetSizeHeader = sizeof(int);
            
            int samplesPerFrame = FrameSize / Channels;
            if (samplesPerFrame <= 0)
            {
                Logger.client.Error($"[OpusCodec] Invalid frame size calculation: FrameSize={FrameSize}, Channels={Channels}");
                return new byte[0];
            }
            
            short[] decodedBuffer = new short[FrameSize];
            using var decoded = new MemoryStream();
            using var stream = new MemoryStream(encodedData);
            using var reader = new BinaryReader(stream);

            long streamLength = stream.Length;
            
            try
            {
                while (stream.Position < streamLength)
                {
                    // Calculate remaining bytes once per iteration
                    long remainingBytes = streamLength - stream.Position;
                    
                    // Fast path: check if we have enough for header, then validate packet size
                    if (remainingBytes < packetSizeHeader)
                        break; // Incomplete header, silently skip

                    int packetSize = reader.ReadInt32();
                    
                    // Quick validation: packet size must be reasonable
                    if (packetSize <= 0 || packetSize > maxPacketSize)
                        break; // Invalid size, silently skip
                    
                    // Check if we have enough bytes for the packet
                    remainingBytes -= packetSizeHeader;
                    if (remainingBytes < packetSize)
                        break; // Incomplete packet, silently skip

                    byte[] encodedPacket = reader.ReadBytes(packetSize);
                    
                    // Validate packet is not empty
                    if (encodedPacket.Length == 0 || encodedPacket.Length != packetSize)
                        continue;
                    
                    var encodedSpan = new ReadOnlySpan<byte>(encodedPacket);
                    var decodedSpan = new Span<short>(decodedBuffer);
                    
                    // Decode with validation - OpusException should be rare with valid data
                    int decodedLength = decoder.Decode(encodedSpan, decodedSpan, samplesPerFrame, false);

                    // Only write if decode was successful (decodedLength > 0 and within bounds)
                    if (decodedLength > 0 && decodedLength <= samplesPerFrame)
                    {
                        byte[] decodedPacket = AudioUtils.ShortsToBytes(decodedBuffer, 0, decodedLength * Channels);
                        decoded.Write(decodedPacket, 0, decodedPacket.Length);
                    }
                    // Negative values indicate decode errors, silently skip
                }
            }
            catch (Concentus.OpusException)
            {
                // Rare Opus-specific errors, silently skip corrupted packet
            }
            catch (Exception e)
            {
                // Other errors (IO, etc.) - log but don't spam
                Logger.client.Error($"Couldn't decode audio:\n{e}");
            }

            return decoded.ToArray();
        }
    }
}
