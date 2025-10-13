using OpenTK.Audio.OpenAL;
using RPVoiceChat.Config;
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
        public string codec;
        public int transmissionRangeBlocks;
        public int effectiveRange;
        public bool ignoreDistanceReduction { get; set; } = false;
        public float wallThicknessOverride { get; set; } = -1f; // -1 = pas d'override
        public bool isGlobalBroadcast { get; set; } = false;

        public AudioData() { }

        public static AudioData FromPacket(AudioPacket audioPacket)
        {
            return new AudioData()
            {
                data = audioPacket.AudioData,
                frequency = audioPacket.Frequency,
                format = audioPacket.Format,
                voiceLevel = audioPacket.VoiceLevel,
                codec = audioPacket.Codec,
                transmissionRangeBlocks = audioPacket.TransmissionRangeBlocks,
                effectiveRange = audioPacket.TransmissionRangeBlocks > 0
                    ? audioPacket.TransmissionRangeBlocks
                    : WorldConfig.GetInt(audioPacket.VoiceLevel),
                ignoreDistanceReduction = audioPacket.IgnoreDistanceReduction,
                wallThicknessOverride = audioPacket.WallThicknessOverride,
                isGlobalBroadcast = audioPacket.IsGlobalBroadcast
            };
        }
    }
}