namespace RPVoiceChat
{
    public enum VoiceLevel
    {
        Whispering = 5,
        Talking = 15,
        Shouting = 25,

        SquareWhispering = Whispering * Whispering,
        SquareTalking = Talking * Talking,
        SquareShouting = Shouting * Shouting
    }
}
