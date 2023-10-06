using OpenTK.Audio.OpenAL;
using ProtoBuf;
using RPVoiceChat.Audio;
using System.IO;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AudioPacket : INetworkPacket
    {
        public string PlayerId { get; set; }
        public byte[] AudioData { get; set; }
        public int Length { get; set; }
        public VoiceLevel VoiceLevel { get; set; }
        public int Frequency { get; set; }
        public ALFormat Format { get; set; }
        public long SequenceNumber { get; set; }

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
        }

        public byte[] ToBytes()
        {
            var stream = new MemoryStream();
            Serializer.Serialize(stream, this);
            return stream.ToArray();
        }

        INetworkPacket INetworkPacket.FromBytes(byte[] data)
        {
            return FromBytes(data);
        }

        public static AudioPacket FromBytes(byte[] data)
        {
            var packet = Serializer.Deserialize<AudioPacket>(new MemoryStream(data));
            return packet;
        }
    }
}
