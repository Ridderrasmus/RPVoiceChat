using RPVoiceChat.Client;
using RPVoiceChat.Config;
using RPVoiceChat.DB;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking;
using RPVoiceChat.Util;
using System;
using System.Collections.Concurrent;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace RPVoiceChat.Audio
{
    public class AudioOutputManager : IDisposable
    {
        ICoreClientAPI capi;
        private bool isLoopbackEnabled;

        private ConcurrentDictionary<string, string> playerEffects = new();

        public bool IsLoopbackEnabled
        {
            get => isLoopbackEnabled;

            set
            {
                isLoopbackEnabled = value;
                if (localPlayerAudioSource == null)
                    return;

                if (isLoopbackEnabled)
                {
                    localPlayerAudioSource.StartPlaying();
                }
                else
                {
                    localPlayerAudioSource.StopPlaying();
                }
            }
        }

        private ConcurrentDictionary<string, PlayerAudioSource> playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();
        private PlayerAudioSource localPlayerAudioSource;
        private ClientSettingsRepository clientSettingsRepo;

        public AudioOutputManager(ICoreClientAPI api, ClientSettingsRepository settingsRepository)
        {
            IsLoopbackEnabled = ModConfig.ClientConfig.Loopback;
            capi = api;
            clientSettingsRepo = settingsRepository;
        }

        public void Launch()
        {
            PlayerListener.Init(capi);
            capi.Event.PlayerEntitySpawn += PlayerSpawned;
            capi.Event.PlayerEntityDespawn += PlayerDespawned;
            ClientLoaded();
        }

        // Called when the client receives an audio packet supplying the audio packet
        public void HandleAudioPacket(AudioPacket packet)
        {
            // Reject null or empty packets to prevent client crashes
            if (packet.AudioData == null || packet.AudioData.Length == 0)
            {
                Logger.client.Debug("Received empty audio packet, dropping");
                return;
            }

            if (packet.AudioData.Length != packet.Length)
            {
                Logger.client.Debug("Audio packet payload had invalid length, dropping packet");
                return;
            }

            // Check if the player is banned - don't process their audio (additional client-side security)
            if (RPVoiceChatClient.VoiceBanManagerInstance != null && 
                RPVoiceChatClient.VoiceBanManagerInstance.IsPlayerBanned(packet.PlayerId))
            {
                return;
            }

            PlayerAudioSource source = GetOrCreatePlayerSource(packet.PlayerId);
            if (source == null)
            {
                Logger.client.Debug("Unable to resolve player ID into player source, dropping packet");
                return;
            }

            HandleAudioPacket(packet, source);
        }

        public void HandleAudioPacket(AudioPacket packet, PlayerAudioSource source)
        {
            string codec = packet.Codec;
            int frequency = packet.Frequency;
            int channels = AudioUtils.ChannelsPerFormat(packet.Format);
            AudioData audioData = AudioData.FromPacket(packet);

            // The server has already calculated the effective range and sent packets only to players within range
            // Here we just need to update the voice level for audio quality

            if (source.voiceLevel != packet.VoiceLevel)
                source.UpdateVoiceLevel(packet.VoiceLevel);

            source.UpdatePlayer();
            source.UpdateAudioFormat(codec, frequency, channels);
            source.EnqueueAudio(audioData, packet.SequenceNumber);
        }

        public void HandleLoopback(AudioPacket packet)
        {
            if (!IsLoopbackEnabled) return;

            HandleAudioPacket(packet, localPlayerAudioSource);
        }

        private void ClientLoaded()
        {
            localPlayerAudioSource = new PlayerAudioSource(capi.World.Player, capi, clientSettingsRepo)
            {
                IsLocational = false,
            };

            if (!isLoopbackEnabled) return;
            localPlayerAudioSource.StartPlaying();
        }

        private PlayerAudioSource GetOrCreatePlayerSource(string playerId)
        {
            PlayerAudioSource source;
            if (playerSources.TryGetValue(playerId, out source) && !source.IsDisposed)
                return source;

            var player = capi.World.PlayerByUid(playerId);
            if (player == null) return null;

            return CreatePlayerSource(player);
        }

        private PlayerAudioSource CreatePlayerSource(IPlayer player)
        {
            var source = new PlayerAudioSource(player, capi, clientSettingsRepo);
            playerSources.AddOrUpdate(player.PlayerUID, source, (_, __) => source);
            source.StartPlaying();

            return source;
        }

        private void PlayerSpawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId) return;

            CreatePlayerSource(player);
        }

        private void PlayerDespawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId)
            {
                localPlayerAudioSource.Dispose();
                localPlayerAudioSource = null;
                return;
            }

            playerSources.TryGetValue(player.PlayerUID, out var source);
            source?.Dispose();
            playerSources.Remove(player.PlayerUID);
            
            // Clean up cached name tag textures for this player
            PlayerNameTagRenderer.CleanupPlayerCache(player.PlayerUID);
        }

        public bool IsPlayerTalking(string playerId)
        {
            if (playerSources.TryGetValue(playerId, out var source))
                return source.IsPlaying;

            if (capi.World.Player.PlayerUID == playerId)
                return localPlayerAudioSource.IsPlaying;

            Logger.client.Warning($"Could not find player audio source for {playerId}, assuming player isn't talking");
            return false;
        }

        public bool SetVoiceLevelForPlayer(string playerId, VoiceLevel voiceLevel)
        {
            var source = GetOrCreatePlayerSource(playerId);
            if (source == null) return false;

            source.UpdateVoiceLevel(voiceLevel);
            return true;
        }

        public void Dispose()
        {
            try
            {
                PlayerListener.Dispose();
                localPlayerAudioSource?.Dispose();
                foreach (var source in playerSources.Values)
                    source?.Dispose();
                
                // Clean up all cached name tag textures
                PlayerNameTagRenderer.CleanupAllCache();
            }
            catch (Exception e)
            {
                Logger.client.Warning($"Error disposing audio output manager: {e.Message}");
            }
            finally
            {
                // Always unsubscribe from events, even if disposal fails
                try
                {
                    capi.Event.PlayerEntitySpawn -= PlayerSpawned;
                    capi.Event.PlayerEntityDespawn -= PlayerDespawned;
                }
                catch (Exception e)
                {
                    Logger.client.Warning($"Error unsubscribing audio output events: {e.Message}");
                }
            }
        }

        public bool ApplyEffectToPlayer(string playerId, string effectName)
        {
            var source = GetOrCreatePlayerSource(playerId);
            if (source == null) return false;

            source.SetSoundEffect(effectName);
            playerEffects[playerId] = effectName;

            return true;
        }

        public bool ClearEffectForPlayer(string playerId)
        {
            if (playerEffects.TryRemove(playerId, out _))
            {
                var source = GetOrCreatePlayerSource(playerId);
                source?.ClearSoundEffect();
                return true;
            }

            return false;
        }
    }
}