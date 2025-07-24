using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using RPVoiceChat.Audio;
using RPVoiceChat.Networking;
using RPVoiceChat.DB;

namespace RPVoiceChat.API
{
    /// <summary>
    /// Main API class for the voice chat system that other mods can use
    /// </summary>    
    public class VoiceChatSystem : ModSystem, IVoiceChatUI
    {
        private static VoiceChatSystem instance;
        private ICoreAPI api;
        private AudioOutputManager audioOutputManager;
        private ClientSettingsRepository clientSettingsRepo;
        private Dictionary<string, IVoiceSource> voiceSources = new Dictionary<string, IVoiceSource>();
        private Dictionary<string, IVoiceSourceProvider> sourceProviders = new Dictionary<string, IVoiceSourceProvider>();
        private Dictionary<string, float> playerGains = new Dictionary<string, float>();
        
        public static VoiceChatSystem Instance => instance;
        
        public bool Loopback 
        { 
            get => audioOutputManager?.IsLoopbackEnabled ?? false;
            set 
            {
                if (audioOutputManager != null)
                    audioOutputManager.IsLoopbackEnabled = value;
            }
        }
        public bool IsMuted { get; set; }
        
        public event Action<string, bool> OnPlayerTalkingStateChanged;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            instance = this;
        }

        /// <summary>
        /// Initialize the system with client-side dependencies
        /// </summary>
        public void InitializeClient(ClientSettingsRepository settingsRepo)
        {
            if (api is ICoreClientAPI capi)
            {
                clientSettingsRepo = settingsRepo;
                audioOutputManager = new AudioOutputManager(capi, clientSettingsRepo);
                audioOutputManager.Launch();
            }
        }

        /// <summary>
        /// Register a new voice source provider that can create voice sources
        /// </summary>
        public void RegisterVoiceSourceProvider(string providerId, IVoiceSourceProvider provider)
        {
            if (!sourceProviders.ContainsKey(providerId))
            {
                sourceProviders[providerId] = provider;
            }
        }

        /// <summary>
        /// Create and register a new voice source
        /// </summary>
        public IVoiceSource CreateVoiceSource(string providerId, string sourceId)
        {
            if (sourceProviders.TryGetValue(providerId, out var provider))
            {
                var source = provider.CreateVoiceSource(sourceId);
                if (source != null)
                {
                    voiceSources[sourceId] = source;
                    return source;
                }
            }
            return null;
        }

        /// <summary>
        /// Get a registered voice source by its ID
        /// </summary>
        public IVoiceSource GetVoiceSource(string sourceId)
        {
            if (voiceSources.TryGetValue(sourceId, out var source))
                return source;
            else
                return null;
        }

        /// <summary>
        /// Remove a voice source from the system
        /// </summary>
        public void RemoveVoiceSource(string sourceId)
        {
            if (voiceSources.TryGetValue(sourceId, out var source))
            {
                source.Stop();
                voiceSources.Remove(sourceId);
            }
        }

        /// <summary>
        /// Handle incoming audio packets and route them to the appropriate voice source
        /// </summary>
        public void HandleAudioPacket(AudioPacket packet)
        {
            if (voiceSources.TryGetValue(packet.PlayerId, out var source))
            {
                source.HandleAudioPacket(packet);
            }
            else
            {
                // Fallback to legacy AudioOutputManager for compatibility
                audioOutputManager?.HandleAudioPacket(packet);
            }
        }

        /// <summary>
        /// Handle loopback audio packets
        /// </summary>
        public void HandleLoopback(AudioPacket packet)
        {
            audioOutputManager?.HandleLoopback(packet);
        }

        public override void Dispose()
        {
            foreach (var source in voiceSources.Values)
            {
                source.Stop();
            }
            voiceSources.Clear();
            sourceProviders.Clear();
            audioOutputManager?.Dispose();
            instance = null;
        }

        /// <summary>
        /// Determines whether the specified player is currently talking.
        /// </summary>
        /// <remarks>This method checks the talking state of a player based on their unique identifier.
        /// Ensure that the player ID is valid and corresponds to an existing player in the system.</remarks>
        /// <param name="playerId">The unique identifier of the player to check.</param>
        /// <returns><see langword="true"/> if the player is currently talking; otherwise, <see langword="false"/>. Returns <see
        /// langword="false"/> if the player is not found.</returns>
        public bool IsPlayerTalking(string playerId)
        {
            // Check if the playerId exists in the voiceSources dictionary
            if (voiceSources.TryGetValue(playerId, out var voiceSource))
            {
                // Assuming the voiceSource has a property or method to check if the player is talking
                return voiceSource.IsPlaying; // Replace with actual method to check talking state
            }
            
            // Fallback to legacy AudioOutputManager
            return audioOutputManager?.IsPlayerTalking(playerId) ?? false;
        }

        /// <summary>
        /// Sets the audio gain level for a specified player.
        /// </summary>
        /// <remarks>If the player already has a gain level set, it will be updated to the new value. If
        /// the player does not exist in the collection, a new entry will be added.</remarks>
        /// <param name="playerId">The unique identifier of the player. Cannot be null or empty.</param>
        /// <param name="gain">The gain level to set for the player. This value represents the audio gain and can be any valid float value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="playerId"/> is null or empty.</exception>
        public void SetPlayerGain(string playerId, float gain)
        {
            // Check if the playerId is valid
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player ID cannot be null or empty.", nameof(playerId));
            }

            if (playerGains.TryGetValue(playerId, out var existingGain))
            {
                playerGains[playerId] = gain;
            }
            else
            {
                playerGains.Add(playerId, gain);
            }
        }

        /// <summary>
        /// Retrieves the gain value associated with a specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose gain is to be retrieved. Cannot be null or empty.</param>
        /// <returns>The gain value for the specified player. Returns <see langword="1.0f"/> if the player's gain is not set.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="playerId"/> is null or empty.</exception>
        public float GetPlayerGain(string playerId)
        {
            // Check if the playerId is valid
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player ID cannot be null or empty.", nameof(playerId));
            }
            // Return the gain for the player, defaulting to 1.0f if not set
            return playerGains.TryGetValue(playerId, out var gain) ? gain : 1.0f;
        }

        /// <summary>
        /// Gets the audio output manager instance
        /// </summary>
        public AudioOutputManager GetAudioOutputManager()
        {
            return audioOutputManager;
        }

        /// <summary>
        /// Retrieves the voice source associated with the specified player ID.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose voice source is to be retrieved. Cannot be null or empty.</param>
        /// <returns>The <see cref="IVoiceSource"/> associated with the specified player ID, or <see langword="null"/> if no
        /// voice source is found for the player.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="playerId"/> is null or empty.</exception>
        public IVoiceSource GetPlayerVoiceSource(string playerId)
        {
            // Check if the playerId is valid
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player ID cannot be null or empty.", nameof(playerId));
            }
            // Return the voice source for the player, or null if not found
            if (voiceSources.TryGetValue(playerId, out var voiceSource))
            {
                return voiceSource;
            }
            return null; // Player voice source not found
        }
    }
}
