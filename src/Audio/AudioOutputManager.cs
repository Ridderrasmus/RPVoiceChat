using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace rpvoicechat
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

        public EffectsExtension EffectsExtension;
        private ConcurrentDictionary<string, PlayerAudioSource> playerSources = new ConcurrentDictionary<string, PlayerAudioSource>();
        private PlayerAudioSource localPlayerAudioSource;

        public AudioOutputManager(ICoreClientAPI api)
        {
            _config = ModConfig.Config;
            IsLoopbackEnabled = _config.IsLoopbackEnabled;
            capi = api;
            capi.Event.PlayerEntitySpawn += PlayerSpawned;
            capi.Event.PlayerEntityDespawn += PlayerDespawned;

            EffectsExtension = new EffectsExtension();
        }

        // Called when the client receives an audio packet supplying the audio packet
        public async void HandleAudioPacket(AudioPacket packet)
        {
            await Task.Run(() =>
            {
                if (playerSources.TryGetValue(packet.PlayerId, out var source))
                {
                    // Update the voice level if it has changed
                    // Not sure about this one, might be better to just update the voice level every time we update the player
                    if (source.VoiceLevel != packet.VoiceLevel)
                        source.UpdateVoiceLevel(packet.VoiceLevel);

                    source.QueueAudio(packet.AudioData, packet.Length);
                }
                else
                {
                    var player = capi.World.PlayerByUid(packet.PlayerId);
                    if (player == null)
                    {
                        capi.Logger.Error("Could not find player for playerId !");
                        return;
                    }

                    var newSource = new PlayerAudioSource(player, this, capi);
                    newSource.QueueAudio(packet.AudioData, packet.Length);
                    if (!playerSources.TryAdd(packet.PlayerId, newSource))
                    {
                        capi.Logger.Error("Could not add new player to sources !");
                    }
                }
            });
        }

        public void HandleLoopback(byte[] audioData, int length, VoiceLevel voiceLevel)
        {
            if (!IsLoopbackEnabled)
                return;

            localPlayerAudioSource.QueueAudio(audioData, length);
        }

        public void PlayerSpawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId)
            {
                localPlayerAudioSource = new PlayerAudioSource(player, this, capi)
                {
                    IsMuffled = false,
                    IsReverberated = false,
                    IsLocational = false
                };

                if (isLoopbackEnabled)
                {
                    localPlayerAudioSource.StartPlaying();
                }
            }
            else
            {
                var playerSource = new PlayerAudioSource(player, this, capi)
                {
                    IsMuffled = false,
                    IsReverberated = false,
                    IsLocational = true
                };

                if (playerSources.TryAdd(player.PlayerUID, playerSource) == false)
                {
                    capi.Logger.Warning($"Failed to add player {player.PlayerName} as source !");
                }
                else
                {
                    playerSource.StartPlaying();
                }
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
                    capi.Logger.Warning($"Failed to remove player {player.PlayerName}");
                }
            }
        }
    }
}

