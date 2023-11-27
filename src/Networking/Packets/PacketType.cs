namespace RPVoiceChat.Networking
{
    public enum PacketType
    {
        SelfPing = 0,
        Ping = 1,
        Pong = 2,
        ConnectionRequest = 10,
        ConnectionInfo = 11,
        Audio = 20
    }
}
