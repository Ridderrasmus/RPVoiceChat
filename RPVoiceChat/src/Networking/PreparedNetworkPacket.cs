using System.IO;
using ProtoBuf;

namespace RPVoiceChat.Networking
{
    /// <summary>One immutable delivery variant shared by all of its recipients. No pooled ownership escapes.</summary>
    public sealed class PreparedNetworkPacket
    {
        public NetworkPacket Packet { get; }
        private byte[] customBytes;
        private byte[] payload;

        public PreparedNetworkPacket(NetworkPacket packet) { Packet = packet; }

        public byte[] CustomBytes
        {
            get
            {
                if (customBytes != null) return customBytes;
                if (Packet is not AudioPacket) return customBytes = Packet.ToBytes();
                byte[] body = Payload;
                customBytes = new byte[body.Length + 4];
                System.BitConverter.GetBytes((int)PacketType.Audio).CopyTo(customBytes, 0);
                body.CopyTo(customBytes, 4);
                return customBytes;
            }
        }

        public byte[] Payload
        {
            get
            {
                if (payload != null) return payload;
                using var stream = new MemoryStream();
                Serializer.Serialize(stream, (AudioPacket)Packet);
                return payload = stream.ToArray();
            }
        }
    }
}
