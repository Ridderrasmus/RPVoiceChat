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

        public static AudioData FromPacket(AudioPacket audioPacket, IAudioCodec codec = null)
        {
            var data = codec?.Decode(audioPacket.AudioData) ?? audioPacket.AudioData;

            return new AudioData()
            {
                data = data,
                frequency = audioPacket.Frequency,
                format = audioPacket.Format,
                voiceLevel = audioPacket.VoiceLevel,
            };
        }
    }
}
