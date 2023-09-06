using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using Concentus.Structs;

namespace RPVoiceChat
{
    public class AudioOutputManager
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
        public EffectsExtension EffectsExtension;
        private OpusDecoder decoder;
        private ConcurrentDictionary<string, PlayerAudioSource> playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();
        private PlayerAudioSource localPlayerAudioSource;

        public AudioOutputManager(ICoreClientAPI api)
        {
            _config = ModConfig.Config;
            IsLoopbackEnabled = _config.IsLoopbackEnabled;
            capi = api;

            EffectsExtension = new EffectsExtension();
            decoder = new OpusDecoder(MicrophoneManager.Frequency, 1);
        }

        public void Launch()
        {
            isReady = true;
            capi.Event.PlayerEntitySpawn += PlayerSpawned;
            capi.Event.PlayerEntityDespawn += PlayerDespawned;
            ClientLoaded();
        }

        // Called when the client receives an audio packet supplying the audio packet
        public async void HandleAudioPacket(AudioPacket packet)
        {
            if (!isReady) return;

            await Task.Run(() =>
            {
                PlayerAudioSource source;
                string playerId = packet.PlayerId;

                if (!playerSources.TryGetValue(playerId, out source))
                {
                    var player = capi.World.PlayerByUid(playerId);
                    if (player == null)
                    {
                        Logger.client.Error("Could not find player for playerId !");
                        return;
                    }

                    source = new PlayerAudioSource(player, this, capi);
                    if (!playerSources.TryAdd(playerId, source))
                    {
                        Logger.client.Error("Could not add new player to sources !");
                    }
                }

                // Update the voice level if it has changed
                if (source.voiceLevel != packet.VoiceLevel)
                    source.UpdateVoiceLevel(packet.VoiceLevel);
                source.UpdatePlayer();

                // Decode the audio data
                float[] outBuffer = new float[960];
                int samples = decoder.Decode(packet.AudioData, 0, packet.Length, outBuffer, 0, 960, false);

                // Convert the audio data to a byte array
                byte[] byteBuffer = new byte[samples * 2];
                for (int i = 0; i < samples; i++)
                {
                    short val = (short)(outBuffer[i] * short.MaxValue);
                    byteBuffer[i * 2] = (byte)(val & 0xFF);
                    byteBuffer[i * 2 + 1] = (byte)(val >> 8);
                }

                // Queue the audio data
                source.QueueAudio(byteBuffer, samples * 2);
            });
        }

        public void HandleLoopback(AudioPacket packet)
        {
            if (!IsLoopbackEnabled) return;

            localPlayerAudioSource.UpdatePlayer();
            localPlayerAudioSource.QueueAudio(packet.AudioData, packet.Length);
        }

        public void ClientLoaded()
        {
            localPlayerAudioSource = new PlayerAudioSource(capi.World.Player, this, capi)
            {
                IsMuffled = false,
                IsReverberated = false,
                IsLocational = false
            };

            if (!isLoopbackEnabled) return;
            localPlayerAudioSource.StartPlaying();
        }

        public void PlayerSpawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId) return;

            var playerSource = new PlayerAudioSource(player, this, capi)
            {
                IsMuffled = false,
                IsReverberated = false,
                IsLocational = true
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
    }
}

