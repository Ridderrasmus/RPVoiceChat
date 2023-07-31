using Vintagestory.API.Client;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
        public void HandleAudioPacket(AudioPacket packet)
        {
            if (playerSources.TryGetValue(packet.PlayerId, out var source))
            {
                //packet.AudioData as short[]
                //source.QueueAudio(packet.AudioData, packet.Length);
            }
        }

        public void HandleLoopback(byte[] audioData, int length, VoiceLevel voiceLevel)
        {
            if (!IsLoopbackEnabled)
                return;

            localPlayerAudioSource.QueueAudio(audioData, length);
        }

        public void ClearAudio()
        {
            localPlayerAudioSource.StopPlaying();

            foreach (var pair in playerSources)
            {
                pair.Value.StopPlaying();
            }
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
                if (playerSources.TryAdd(player.PlayerUID, new PlayerAudioSource(player, this, capi)) == false)
                {
                    capi.ShowChatMessage("Failed to add new player to source !");
                }
            }
        }
        public void PlayerDespawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId)
            {
                localPlayerAudioSource = null;
            }
            else
            {
                if (playerSources.TryRemove(player.PlayerUID, out var playerAudioSource) == false)
                {
                    capi.ShowChatMessage("Failed to add new player to source !");
                }

                playerAudioSource.Dispose();
            }
        }

        public string[] GetInputDeviceNames()
        {
            return AudioContext.AvailableDevices.Distinct().ToArray();
        }

        private int GetVoiceDistance(VoiceLevel voiceLevel)
        {
            switch (voiceLevel)
            {
                case VoiceLevel.Whispering:
                    return capi.World.Config.GetInt("rpvoicechat:distance-whisper", 5);
                case VoiceLevel.Normal:
                    return capi.World.Config.GetInt("rpvoicechat:distance-talk", 15);
                case VoiceLevel.Shouting:
                    return capi.World.Config.GetInt("rpvoicechat:distance-shout", 25);
                default:
                    return 15;
            }
        }

    }
}

