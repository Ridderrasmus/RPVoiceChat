using Vintagestory.API.Client;
using System.Collections.Concurrent;
using Vintagestory.API.Common;
using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using System;

namespace RPVoiceChat.Audio
{
    public class AudioOutputManager : IDisposable
    {
        ICoreClientAPI capi;
        RPVoiceChatConfig _config;
        private bool isLoopbackEnabled;
        public bool IsLoopbackEnabled {
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

        public bool isReady = false;
        private ConcurrentDictionary<string, PlayerAudioSource> playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();
        private PlayerAudioSource localPlayerAudioSource;

        public AudioOutputManager(ICoreClientAPI api)
        {
            _config = ModConfig.Config;
            IsLoopbackEnabled = _config.IsLoopbackEnabled;
            capi = api;
        }

        public void Launch()
        {
            PlayerListener.Init(capi);
            isReady = true;
            capi.Event.PlayerEntitySpawn += PlayerSpawned;
            capi.Event.PlayerEntityDespawn += PlayerDespawned;
            ClientLoaded();
        }

        // Called when the client receives an audio packet supplying the audio packet
        public void HandleAudioPacket(AudioPacket packet)
        {
            if (!isReady) return;
            if (packet.AudioData.Length != packet.Length)
            {
                Logger.client.Debug("Audio packet payload had invalid length, dropping packet");
                return;
            }

            PlayerAudioSource source;
            string playerId = packet.PlayerId;

            if (!playerSources.TryGetValue(playerId, out source))
            {
                var player = capi.World.PlayerByUid(playerId);
                if (player == null)
                {
                    Logger.client.Error($"Could not find player for playerId {playerId}");
                    return;
                }

                source = new PlayerAudioSource(player, this, capi);
                if (!playerSources.TryAdd(playerId, source))
                {
                    Logger.client.Debug("Could not add new player to sources");
                }
            }

            HandleAudioPacket(packet, source);
        }

        public void HandleAudioPacket(AudioPacket packet, PlayerAudioSource source)
        {
            int frequency = packet.Frequency;
            int channels = AudioUtils.ChannelsPerFormat(packet.Format);

            IAudioCodec codec = source.GetOrCreateAudioCodec(frequency, channels);
            AudioData audioData = AudioData.FromPacket(packet, codec);

            // Update the voice level if it has changed
            if (source.voiceLevel != packet.VoiceLevel)
                source.UpdateVoiceLevel(packet.VoiceLevel);
            source.UpdatePlayer();
            source.EnqueueAudio(audioData, packet.SequenceNumber);
        }

        public void HandleLoopback(AudioPacket packet)
        {
            if (!IsLoopbackEnabled) return;

            HandleAudioPacket(packet, localPlayerAudioSource);
        }

        public void ClientLoaded()
        {
            localPlayerAudioSource = new PlayerAudioSource(capi.World.Player, this, capi)
            {
                IsLocational = false,
            };

            if (!isLoopbackEnabled) return;
            localPlayerAudioSource.StartPlaying();
        }

        public void PlayerSpawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId) return;

            var playerSource = new PlayerAudioSource(player, this, capi)
            {
                IsLocational = true,
            };

            if (playerSources.TryAdd(player.PlayerUID, playerSource) == false)
            {
                Logger.client.Warning($"Failed to add player {player.PlayerName} as source !");
            }
            else
            {
                playerSource.StartPlaying();
            }
        }

        public void PlayerDespawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId)
            {
                localPlayerAudioSource.Dispose();
                localPlayerAudioSource = null;
            }
            else
            {
                if (playerSources.TryRemove(player.PlayerUID, out var playerAudioSource))
                {
                    playerAudioSource.Dispose();
                }
                else
                {
                    Logger.client.Warning($"Failed to remove player {player.PlayerName}");
                }
            }
        }

        public void Dispose()
        {
            localPlayerAudioSource?.Dispose();
            foreach (var source in playerSources.Values)
                source?.Dispose();
        }
    }
}

