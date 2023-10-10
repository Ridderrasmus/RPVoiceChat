using ProtoBuf;
using System;
using System.IO;

namespace RPVoiceChat.Networking
{
    public abstract class NetworkPacket
    {
        protected abstract PacketType Code { get; }

        public byte[] ToBytes()
        {
            var stream = new MemoryStream();
            stream.Write(BitConverter.GetBytes((int)Code), 0, 4);
            Serializer.Serialize(stream, this);
            return stream.ToArray();
        }

        public static T FromBytes<T>(byte[] data) where T: NetworkPacket
        {
            var stream = new MemoryStream(data);
            PacketType code = (PacketType)BitConverter.ToInt32(data, 0);
            stream.Position = 4;

            var packet = Serializer.Deserialize<T>(stream);
            if (code != packet.Code) throw new Exception($"Parsed network packet was of type {code} while expected {packet.Code}");

            return packet;
        }
    }
}
