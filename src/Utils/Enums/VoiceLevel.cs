namespace rpvoicechat
{
    public enum VoiceLevel
    {
        Whispering = 5,
        Normal = 15,
        Shouting = 25,
        SqrWhispering = Whispering* Whispering,
        SqrNormal = Normal * Normal,
        SqrShouting = Shouting * Shouting
    }
}
