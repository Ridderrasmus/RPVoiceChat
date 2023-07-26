namespace rpvoicechat
{
    public enum VoiceLevel
    {
        Whispering = 5,
        Normal = 15,
        Shouting = 25,

        SquareWhispering = Whispering* Whispering,
        SquareNormal = Normal* Normal,
        SquareShouting = Shouting* Shouting
    }
}
