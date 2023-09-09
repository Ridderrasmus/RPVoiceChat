namespace RPVoiceChat.Audio
{
    public interface IAudioCodec
    {
        public byte[] Encode(short[] pcmData);
        public byte[] Decode(byte[] encodedData);
    }
}
