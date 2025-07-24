using System;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using RPVoiceChat.Audio;
using RPVoiceChat.DB;
using RPVoiceChat.Networking;
using RPVoiceChat.API;
using RPVoiceChat.Utils;
using Vintagestory.API.Common.Entities;

namespace RPVoiceChat.Audio.Sources
{
    public class PlayerVoiceSource : IVoiceSource
    {        
        private readonly PlayerAudioSource audioSource;
        private readonly IPlayer player;
        private readonly ICoreClientAPI capi;
        private float currentGain = 1.0f;
        
        public string SourceId => player.PlayerUID;
        public bool IsPlaying => audioSource.IsPlaying;
        public VoiceLevel VoiceLevel => audioSource.voiceLevel;
        public EntityPos Position => player.Entity?.SidedPos;
        public bool IsLocational { get; set; } = true;

        public event Action<AudioData> OnAudioReceived;
        public event Action<bool> OnActiveStateChanged;

        public PlayerVoiceSource(IPlayer player, ICoreClientAPI capi, ClientSettingsRepository settings)
        {
            this.player = player;
            this.capi = capi;
            audioSource = new PlayerAudioSource(player, capi, settings);

            // Forward audio source state changes
            audioSource.OnAudioDataReceived += data => OnAudioReceived?.Invoke(data);
            audioSource.OnPlayingStateChanged += state => OnActiveStateChanged?.Invoke(state);
        }

        public void Start()
        {
            audioSource.StartPlaying();
        }

        public void Stop()
        {
            audioSource.StopPlaying();
        }

        public void HandleAudioPacket(AudioPacket packet)
        {
            // Update audio format and voice level if needed
            string codec = packet.Codec;
            int frequency = packet.Frequency;
            int channels = AudioUtils.ChannelsPerFormat(packet.Format);
            AudioData audioData = AudioData.FromPacket(packet);

            if (audioSource.voiceLevel != packet.VoiceLevel)
                audioSource.UpdateVoiceLevel(packet.VoiceLevel);
            
            audioSource.UpdateAudioFormat(codec, frequency, channels);
            audioSource.EnqueueAudio(audioData, packet.SequenceNumber);
        }

        public void Update()
        {
            audioSource.UpdatePlayer();
        }

        public void UpdateGain(float gain)
        {
            currentGain = gain;
            audioSource?.UpdateGain(gain);
        }

        /// <summary>
        /// Exposes the underlying PlayerAudioSource for legacy compatibility
        /// </summary>
        public PlayerAudioSource GetAudioSource()
        {
            return audioSource;
        }

        public void Dispose()
        {
            audioSource.Dispose();
        }
    }
}
