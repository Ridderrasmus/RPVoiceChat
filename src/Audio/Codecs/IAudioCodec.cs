namespace RPVoiceChat.Audio
{
    public interface IAudioCodec
    {
        public string Name { get; }
        public int SampleRate { get; }
        public int Channels { get; }
        public int FrameSize { get; }
        public byte[] Encode(short[] pcmData);
        public byte[] Decode(byte[] encodedData);
    }
}
