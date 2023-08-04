namespace rpvoicechat
{
    public enum VoiceLevel
    {
        Whispering = 50,
        Talking = 150,
        Shouting = 250,

        SquareWhispering = Whispering* Whispering,
        SquareTalking = Talking* Talking,
        SquareShouting = Shouting* Shouting
    }
}
