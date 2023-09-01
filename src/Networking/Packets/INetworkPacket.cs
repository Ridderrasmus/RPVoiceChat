namespace RPVoiceChat.Networking
{
    public interface INetworkPacket
    {
        public byte[] ToBytes();
        public INetworkPacket FromBytes(byte[] data);
    }
}
