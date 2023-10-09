namespace RPVoiceChat.Networking
{
    public enum PacketType
    {
        SelfPing = 0,
        Ping = 1,
        Pong = 2,
        ConnectionInfo = 10,
        Audio = 20
    }
}
