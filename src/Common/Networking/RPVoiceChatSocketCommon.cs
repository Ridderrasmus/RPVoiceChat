using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    public class RPVoiceChatSocketCommon
    {
        public int bufferSize = 4096;

        protected Socket clientSocket { get; set; }
        protected Socket serverSocket { get; set; }
        public bool IsServer { get; set; }
        public EndPoint RemoteEndPoint { get; set; } = null;


        internal byte[] SerializePacket(PlayerAudioPacket packet)
        {
            MemoryStream stream = new MemoryStream();
            ProtoBuf.Serializer.Serialize(stream, packet);
            byte[] bytes = stream.ToArray();
            stream.Close();
            return bytes;
        }

        internal PlayerAudioPacket DeserializePacket(byte[] bytes)
        {
            PlayerAudioPacket packet = ProtoBuf.Serializer.Deserialize<PlayerAudioPacket>(new MemoryStream(bytes));

            return packet;
        }
    }
}