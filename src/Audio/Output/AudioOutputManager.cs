using RPVoiceChat.Client;
using RPVoiceChat.Config;
using RPVoiceChat.DB;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Gui;
using RPVoiceChat.Networking;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using System;
using System.Collections.Concurrent;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
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
        public bool UsesExplicitDeliveryMetadata { get; set; }
        private PlayerAudioSource localPlayerAudioSource;
        private VoiceOcclusionScheduler occlusionScheduler;
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
            occlusionScheduler = new VoiceOcclusionScheduler(capi);
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
            if (!RadioProgramRouteKey.IsProgramSource(packet.PlayerId)
                && RPVoiceChatClient.VoiceBanManagerInstance != null
                && RPVoiceChatClient.VoiceBanManagerInstance.IsPlayerBanned(packet.PlayerId))
            {
                return;
            }

            if (IsOwnTalkieRfReception(packet))
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
            AudioData audioData = AudioData.FromPacket(packet);
            if (!UsesExplicitDeliveryMetadata)
            {
                // Older servers cannot distinguish group delivery; preserve their existing behavior.
                var speaker = capi.World.PlayerByUid(packet.PlayerId)?.Entity?.Pos;
                var listener = capi.World.Player?.Entity?.Pos;
                audioData.sourceDimension = speaker?.Dimension ?? listener?.Dimension ?? 0;
                audioData.forceFlatPlayback = packet.IsGlobalBroadcast || speaker == null
                    || (listener != null && speaker.DistanceTo(listener) > audioData.effectiveRange);
            }

            // Metadata is applied in playback order, not network arrival order.
            source.EnqueueAudio(audioData, packet.SequenceNumber);
        }

        public void HandleLoopback(AudioPacket packet)
        {
            if (!IsLoopbackEnabled) return;

            var audio = AudioData.FromPacket(packet);
            audio.forceFlatPlayback = true;
            localPlayerAudioSource?.EnqueueAudio(audio, packet.SequenceNumber);
        }

        private bool IsOwnTalkieRfReception(AudioPacket packet)
        {
            if (!packet.HasSourcePositionOverride || capi.World.Player == null)
            {
                return false;
            }

            if (packet.PlayerId != capi.World.Player.PlayerUID)
            {
                return false;
            }

            EntityPos listenerPos = capi.World.Player.Entity?.Pos;
            if (listenerPos == null)
            {
                return false;
            }

            var sourcePos = new Vec3d(packet.SourcePosX, packet.SourcePosY, packet.SourcePosZ);
            if (BlockEntityRadioReceiver.GetPlaybackVolumeGainAtSource(capi, sourcePos, listenerPos.Dimension) >= 0f)
            {
                return false;
            }

            double dx = packet.SourcePosX - listenerPos.X;
            double dy = packet.SourcePosY - listenerPos.Y;
            double dz = packet.SourcePosZ - listenerPos.Z;
            return dx * dx + dy * dy + dz * dz <= 4.0;
        }

        private void ClientLoaded()
        {
            localPlayerAudioSource = new PlayerAudioSource(capi.World.Player, capi, clientSettingsRepo, null, occlusionScheduler)
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

            if (RadioProgramRouteKey.IsProgramSource(playerId))
                return CreateSyntheticSource(playerId);

            var player = capi.World.PlayerByUid(playerId);
            if (player == null) return null;

            return CreatePlayerSource(player);
        }

        private PlayerAudioSource CreatePlayerSource(IPlayer player)
        {
            var source = new PlayerAudioSource(player, capi, clientSettingsRepo, null, occlusionScheduler);
            playerSources.AddOrUpdate(player.PlayerUID, source, (_, __) => source);
            return source;
        }

        private PlayerAudioSource CreateSyntheticSource(string sourceId)
        {
            // Program bus / RF block emission: no real player UID — position comes from packet override.
            var source = new PlayerAudioSource(capi.World.Player, capi, clientSettingsRepo, sourceId, occlusionScheduler);
            playerSources.AddOrUpdate(sourceId, source, (_, __) => source);
            return source;
        }

        private void PlayerSpawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId) return;

            CreatePlayerSource(player);
            PlayerNameTagRenderer.UpdatePlayerNameTag(player, false);
        }

        private void PlayerDespawned(IPlayer player)
        {
            if (player.ClientId == capi.World.Player.ClientId)
            {
                localPlayerAudioSource.Dispose();
                localPlayerAudioSource = null;
                PlayerNameTagRenderer.CleanupPlayerNametagCache(player.PlayerUID);
                return;
            }

            playerSources.TryGetValue(player.PlayerUID, out var source);
            source?.Dispose();
            playerSources.Remove(player.PlayerUID);

            PlayerNameTagRenderer.CleanupPlayerNametagCache(player.PlayerUID);
        }

        public bool IsPlayerTalking(string playerId)
        {
            if (playerSources.TryGetValue(playerId, out var source))
                return source.IsSpeaking;

            if (capi.World.Player?.PlayerUID == playerId)
                return localPlayerAudioSource?.IsSpeaking == true;

            // Group members can be silent, offline, or outside entity range; no source is normal.
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
            occlusionScheduler?.Dispose();
            try
            {
                PlayerListener.Dispose();
                localPlayerAudioSource?.Dispose();
                foreach (var source in playerSources.Values)
                    source?.Dispose();
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
