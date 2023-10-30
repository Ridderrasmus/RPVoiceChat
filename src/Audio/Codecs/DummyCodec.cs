using RPVoiceChat.Utils;
using Vintagestory.Common;

namespace RPVoiceChat.Audio
{
    public class DummyCodec : IAudioCodec
    {
        public const string _Name = "Dummy";
        public string Name { get; } = _Name;
        public int SampleRate { get; }
        public int Channels { get; }
        public int FrameSize { get; } = 1;
        private ICompression compressor;

        public DummyCodec(int frequency, int channelCount)
        {
            SampleRate = frequency;
            Channels = channelCount;
            compressor = new CompressionGzip();
        }

        public byte[] Encode(short[] pcmData)
        {
            var encoded = AudioUtils.ShortsToBytes(pcmData, 0, pcmData.Length);
            var compressed = compressor.Compress(encoded);
            return compressed;
        }

        public byte[] Decode(byte[] encodedData)
        {
            var decompressed = compressor.Decompress(encodedData);
            return decompressed;
        }
    }
}
