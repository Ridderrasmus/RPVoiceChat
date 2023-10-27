using RPVoiceChat.Utils;

namespace RPVoiceChat.Audio
{
    public class DummyCodec : IAudioCodec
    {
        public const string _Name = "Dummy";
        public string Name { get; } = _Name;
        public int SampleRate { get; }
        public int Channels { get; }
        public int FrameSize { get; } = 1;

        public DummyCodec(int frequency, int channelCount)
        {
            SampleRate = frequency;
            Channels = channelCount;
        }

        public byte[] Encode(short[] pcmData)
        {
            var encoded = AudioUtils.ShortsToBytes(pcmData, 0, pcmData.Length);
            return encoded;
        }

        public byte[] Decode(byte[] encodedData)
        {
            return encodedData;
        }
    }
}
