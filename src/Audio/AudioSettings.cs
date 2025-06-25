namespace RPVoiceChat.Audio
{
    public class AudioSettings
    {
        public float VoiceVolume { get; set; } = 1.0f;
        public float BlockSoundVolume { get; set; } = 0.6f;
        public float ItemSoundVolume { get; set; } = 0.8f;

        public float GetVolume(string type)
        {
            return type switch
            {
                "voice" => VoiceVolume,
                "block" => BlockSoundVolume,
                "item" => ItemSoundVolume,
                _ => 1f,
            };
        }
    }
}