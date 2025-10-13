using OpenTK.Audio.OpenAL;
using ProtoBuf;
using RPVoiceChat.Audio;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AudioPacket : NetworkPacket
    {
        public string PlayerId { get; set; }
        public byte[] AudioData { get; set; }
        public int Length { get; set; }
        public VoiceLevel VoiceLevel { get; set; }
        public int Frequency { get; set; }
        public ALFormat Format { get; set; }
        public long SequenceNumber { get; set; }
        public string Codec { get; set; }
        public int TransmissionRangeBlocks { get; set; } = 0;
        public int EffectiveRange { get; set; } = 0;
        public bool IgnoreDistanceReduction { get; set; } = false;
        public float WallThicknessOverride { get; set; } = -1f;
        protected override PacketType Code { get => PacketType.Audio; }
        public bool IsGlobalBroadcast { get; set; } = false;

        public AudioPacket() { }

        public AudioPacket(string playerId, AudioData audioData, long sequenceNumber)
        {
            PlayerId = playerId;
            AudioData = audioData.data;
            Length = audioData.data.Length;
            VoiceLevel = audioData.voiceLevel;
            Frequency = audioData.frequency;
            Format = audioData.format;
            SequenceNumber = sequenceNumber;
            Codec = audioData.codec;
            TransmissionRangeBlocks = audioData.transmissionRangeBlocks;
            IgnoreDistanceReduction = audioData.ignoreDistanceReduction;
            WallThicknessOverride = audioData.wallThicknessOverride;
            IsGlobalBroadcast = audioData.isGlobalBroadcast;
        }
    }
}