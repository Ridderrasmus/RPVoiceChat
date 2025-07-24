using RPVoiceChat.API;
using RPVoiceChat.Audio;
using RPVoiceChat.Audio.Sources;
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
    // TODO: Audio sources need to be split into different kinds of sources, e.g. locational and non-locational
    // TODO: Create support for voice chat groups, so that players can talk to each other without being in the same location


    public class AudioOutputManager : IDisposable
    {
        ICoreClientAPI capi;
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
                    localPlayerAudioSource.Start();
                }
                else
                {
                    localPlayerAudioSource.Stop();
                }
            }
        }

        private ConcurrentDictionary<string, PlayerAudioSource> playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();
        private IVoiceSource localPlayerAudioSource;
        private ClientSettingsRepository clientSettingsRepo;

        public AudioOutputManager(ICoreClientAPI api, ClientSettingsRepository settingsRepository)
        {
            IsLoopbackEnabled = ClientSettings.Loopback;
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

            // Handle loopback through the VoiceChatSystem for local playback
            VoiceChatSystem.Instance.HandleAudioPacket(packet);
        }

        private void ClientLoaded()
        {
            localPlayerAudioSource = VoiceChatSystem.Instance.CreateVoiceSource("localplayer", capi.World.Player.PlayerUID);

            if (localPlayerAudioSource != null && isLoopbackEnabled)
            {
                localPlayerAudioSource.Start();
            }
        }

        private PlayerAudioSource GetOrCreatePlayerSource(string playerId)
        {
            // Try to get the voice source from the new system first
            var voiceSource = VoiceChatSystem.Instance.GetVoiceSource(playerId);
            
            if (voiceSource is PlayerVoiceSource playerVoiceSource)
            {
                // Return the underlying PlayerAudioSource if available
                return playerVoiceSource.GetAudioSource();
            }

            // Fallback: create a legacy PlayerAudioSource if the new system doesn't have it
            return playerSources.GetOrAdd(playerId, (id) =>
            {
                var player = capi.World.PlayerByUid(id);
                if (player != null)
                {
                    return new PlayerAudioSource(player, capi, clientSettingsRepo);
                }
                return null;
            });
        }

        private void PlayerSpawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId) return;

            VoiceChatSystem.Instance.CreateVoiceSource("player", player.PlayerUID);
        }

        private void PlayerDespawned(IPlayer player)
        {
            VoiceChatSystem.Instance.RemoveVoiceSource(player.PlayerUID);
        }

        public bool IsPlayerTalking(string playerId)
        {
            return VoiceChatSystem.Instance.IsPlayerTalking(playerId);
        }

        public void Dispose()
        {
            capi.Event.PlayerEntitySpawn -= PlayerSpawned;
            capi.Event.PlayerEntityDespawn -= PlayerDespawned;

            VoiceChatSystem.Instance.Dispose();

            PlayerListener.Dispose();
        }
    }
}

