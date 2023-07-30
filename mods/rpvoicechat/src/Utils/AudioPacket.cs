using System;
using Lidgren.Network;

namespace rpvoicechat
{
    public class AudioPacket
    {
        public string PlayerId { get; set; }
        public byte[] AudioData { get; set; }
        public int Length { get; set; }
        public VoiceLevel voiceLevel { get; set; }

        public void WriteToMessage(NetOutgoingMessage message)
        {
            message.Write(PlayerId);
            message.Write((byte)voiceLevel);
            message.Write(Length);
            message.Write(AudioData);
        }

        public static AudioPacket ReadFromMessage(NetIncomingMessage message)
        {
            AudioPacket packet = new AudioPacket();
            packet.PlayerId = message.ReadString();
            packet.voiceLevel = (VoiceLevel)message.ReadByte();
            packet.Length = message.ReadInt32();
            packet.AudioData = message.ReadBytes(packet.Length);
            return packet;
        }
    }
}
