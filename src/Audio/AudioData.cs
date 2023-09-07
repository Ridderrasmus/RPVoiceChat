using OpenTK.Audio.OpenAL;
using RPVoiceChat.Networking;

namespace RPVoiceChat.Audio
{
    public class AudioData
    {
        public byte[] data;
        public int frequency;
        public ALFormat format;
        public double amplitude;
        public VoiceLevel voiceLevel;

        public AudioData() { }

        public AudioData(AudioPacket audioPacket)
        {
            data = audioPacket.AudioData;
            frequency = audioPacket.Frequency;
            format = audioPacket.Format;
            voiceLevel = audioPacket.VoiceLevel;
        }
    }
}
