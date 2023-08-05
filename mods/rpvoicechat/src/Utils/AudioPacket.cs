using System;
using Lidgren.Network;

namespace rpvoicechat
{
    public class AudioPacket
    {
        public string PlayerId { get; set; }
        public byte[] AudioData { get; set; }
        public int Length { get; set; }
        public VoiceLevel VoiceLevel { get; set; }

        public void WriteToMessage(NetOutgoingMessage message)
        {
            message.Write(PlayerId);
            message.Write((byte)VoiceLevel);
            message.Write(Length);
            message.Write(AudioData, 0, Length);
        }

        public static AudioPacket ReadFromMessage(NetIncomingMessage message)
        {
            AudioPacket packet = new AudioPacket();
            packet.PlayerId = message.ReadString();
            packet.VoiceLevel = (VoiceLevel)message.ReadByte();
            packet.Length = message.ReadInt32();
            packet.AudioData = message.ReadBytes(packet.Length);
            return packet;
        }
    }
}
