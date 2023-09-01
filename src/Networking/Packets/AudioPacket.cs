using ProtoBuf;
using RPVoiceChat;
using System.IO;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AudioPacket: INetworkPacket
    {
        public string PlayerId { get; set; }
        public byte[] AudioData { get; set; }
        public int Length { get; set; }
        public VoiceLevel VoiceLevel { get; set; }

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
