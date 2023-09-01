using ProtoBuf;
using System.IO;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ConnectionInfo : INetworkPacket
    {
        public string Address { get; set; }
        public int Port { get; set; }

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

        public static ConnectionInfo FromBytes(byte[] data)
        {
            var packet = Serializer.Deserialize<ConnectionInfo>(new MemoryStream(data));
            return packet;
        }
    }
}
