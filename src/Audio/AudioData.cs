using OpenTK.Audio.OpenAL;

namespace RPVoiceChat.Audio
{
    public struct AudioData
    {
        public byte[] data;
        public int frequency;
        public ALFormat format;
        public double amplitude;
        public VoiceLevel voiceLevel;
    }
}
