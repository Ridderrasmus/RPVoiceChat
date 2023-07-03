using Lidgren.Network;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace rpvoicechat
{
    public class AudioPacket
    {
        public string PlayerId { get; set; }
        public byte[] AudioData { get; set; }
        public VoiceLevel voiceLevel { get; set; }

        public void WriteToMessage(NetOutgoingMessage message)
        {
            message.Write(PlayerId);
            message.Write((byte)voiceLevel);
            message.Write(AudioData.Length);
            message.Write(AudioData);
        }

        public static AudioPacket ReadFromMessage(NetIncomingMessage message)
        {
            AudioPacket packet = new AudioPacket();
            packet.PlayerId = message.ReadString();
            packet.voiceLevel = (VoiceLevel)message.ReadByte();
            int audioDataLength = message.ReadInt32();
            packet.AudioData = message.ReadBytes(audioDataLength);
            return packet;
        }
    }
}
