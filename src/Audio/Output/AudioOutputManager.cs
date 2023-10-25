using OpenTK.Audio.OpenAL;
using RPVoiceChat.DB;
using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
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
        RPVoiceChatConfig _config;
        private bool isLoopbackEnabled;
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

        private EffectsExtension effectsExtension;
        private ConcurrentDictionary<string, PlayerAudioSource> playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();
        private PlayerAudioSource localPlayerAudioSource;
        private ClientSettingsRepository clientSettings;

        public AudioOutputManager(ICoreClientAPI api, ClientSettingsRepository settingsRepository)
        {
            _config = ModConfig.Config;
            IsLoopbackEnabled = _config.IsLoopbackEnabled;
            capi = api;
            effectsExtension = new EffectsExtension();
            clientSettings = settingsRepository;
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
            if (packet.AudioData.Length != packet.Length)
            {
                Logger.client.Debug("Audio packet payload had invalid length, dropping packet");
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
            localPlayerAudioSource = new PlayerAudioSource(capi.World.Player, capi, clientSettings, effectsExtension)
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
            var source = new PlayerAudioSource(player, capi, clientSettings, effectsExtension);
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

        public void Dispose()
        {
            PlayerListener.Dispose();
            localPlayerAudioSource?.Dispose();
            foreach (var source in playerSources.Values)
                source?.Dispose();
        }
    }
}

