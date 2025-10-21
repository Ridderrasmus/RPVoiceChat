using OpenTK.Audio.OpenAL;
using RPVoiceChat.Audio.Effects;
using RPVoiceChat.Config;
using RPVoiceChat.DB;
using RPVoiceChat.Gui;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Audio
{
    public class PlayerAudioSource : IDisposable
    {
        public bool IsDisposed = false;
        public bool IsPlaying { get => _IsPlaying(); }
        public float MaxGain => ServerConfigManager.MaxAudioGain;

        private const int BufferCount = 20;
        private int source;
        public int SourceId => source;
        private CircularAudioBuffer buffer;
        private SortedList<long, AudioData> orderingQueue = new SortedList<long, AudioData>();
        private object ordering_queue_lock = new object();
        private object dequeue_audio_lock = new object();
        private int orderingDelay = 100;
        private long lastAudioSequenceNumber = -1;
        private string currentEffectName;

        private IAudioCodec codec;
        private LowpassFilter lowpassFilter;
        private ReverbEffect reverbEffect;
        private IntoxicatedEffect intoxicatedEffect;
        private UnstableEffect unstableEffect;
        private ICoreClientAPI capi;
        private IPlayer player;
        private ClientSettingsRepository clientSettingsRepo;
        private SoundEffect currentSoundEffect;

        public bool IsLocational { get; set; } = true;
        public VoiceLevel voiceLevel { get; private set; } = VoiceLevel.Talking;
        private Dictionary<VoiceLevel, float> referenceDistanceByVoiceLevel = new Dictionary<VoiceLevel, float>()
        {
            { VoiceLevel.Whispering, 1.25f },
            { VoiceLevel.Talking, 2.25f },
            { VoiceLevel.Shouting, 6.25f },
        };
        private Vec3f lastSpeakerCoords;
        private DateTime? lastSpeakerUpdate;
        private AudioData currentAudio; // Store current audio data for distance factor calculation

        public PlayerAudioSource(IPlayer player, ICoreClientAPI capi, ClientSettingsRepository clientSettingsRepo)
        {
            this.player = player;
            this.capi = capi;
            this.clientSettingsRepo = clientSettingsRepo;

            lastSpeakerCoords = player.Entity?.SidedPos?.XYZFloat;
            lastSpeakerUpdate = DateTime.Now;

            source = OALW.GenSource();
            buffer = new CircularAudioBuffer(source, BufferCount);
            buffer.OnEmptyingQueue += OnSourceStop;

            float gain = GetFinalGain();
            OALW.Source(source, ALSourceb.Looping, false);
            OALW.Source(source, ALSourceb.SourceRelative, true);
            OALW.Source(source, ALSourcef.Gain, gain);
            OALW.Source(source, ALSourcef.Pitch, 1.0f);

            UpdateVoiceLevel(voiceLevel);
        }

        public void UpdateVoiceLevel(VoiceLevel voiceLevel)
        {
            this.voiceLevel = voiceLevel;

            float baseReferenceDistance = referenceDistanceByVoiceLevel[voiceLevel];
            float distanceFactor = GetDistanceFactor();
            float rolloffFactor = baseReferenceDistance * distanceFactor;

            OALW.Source(source, ALSourcef.ReferenceDistance, baseReferenceDistance);
            OALW.Source(source, ALSourcef.RolloffFactor, rolloffFactor);
        }

        public void UpdateAudioFormat(string codecName, int frequency, int channels)
        {
            if (codec?.Name == codecName && codec?.SampleRate == frequency && codec?.Channels == channels) return;

            codec = codecName switch
            {
                OpusCodec._Name => new OpusCodec(frequency, channels),
                DummyCodec._Name => new DummyCodec(frequency, channels),
                _ => null
            };
        }

        // Cache wall thickness calculation to prevent expensive repeated calculations
        private float cachedWallThickness = 0f;
        private long lastWallThicknessUpdate = 0;
        private const long WallThicknessCacheMs = 100; // Update every 100ms instead of every frame

        public void UpdatePlayer()
        {
            EntityPos speakerPos = player.Entity?.SidedPos;
            EntityPos listenerPos = capi.World.Player.Entity?.SidedPos;
            if (speakerPos == null || listenerPos == null)
                return;

            // For global broadcasts, disable muffling and positioning
            bool isGlobalBroadcast = currentAudio?.isGlobalBroadcast == true;

            // If the player is on the other side of something to the listener, then the player's voice should be muffled
            bool mufflingEnabled = ModConfig.ClientConfig.Muffling && !isGlobalBroadcast; // Disable muffling for global broadcast
            float wallThickness = 0f;

            float wallThicknessWeighting = WorldConfig.GetFloat("wall-thickness-weighting");

            if (!isGlobalBroadcast)
            {
                // Check if the current audio has a wall thickness override
                if (currentAudio?.wallThicknessOverride >= 0f)
                {
                    wallThickness = currentAudio.wallThicknessOverride;
                }
                else
                {
                    // Fixed: Cache expensive wall thickness calculation to prevent CPU spikes
                    long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (currentTime - lastWallThicknessUpdate > WallThicknessCacheMs)
                    {
                        cachedWallThickness = LocationUtils.GetWallThickness(capi, player, capi.World.Player);
                        lastWallThicknessUpdate = currentTime;
                    }
                    wallThickness = cachedWallThickness;
                }

                if (capi.World.Player.Entity.Swimming)
                    wallThickness += 1.0f;
            }

            lowpassFilter?.Stop();
            if (mufflingEnabled && wallThickness > 0)
            {
                lowpassFilter = lowpassFilter ?? new LowpassFilter(source);
                lowpassFilter.Start();
                lowpassFilter.SetHFGain(Math.Max(1.0f - (wallThickness / wallThicknessWeighting), 0.1f));
            }

            bool toBeImplementedToggle = false;
            // DEACTIVATED : TO BE IMPLEMENTED
            // If the player is in a reverberated area, then the player's voice should be reverberated
            reverbEffect?.Clear();
            if (toBeImplementedToggle && !isGlobalBroadcast && LocationUtils.IsReverbArea(capi, speakerPos))
            {
                reverbEffect = reverbEffect ?? new ReverbEffect(source);
                reverbEffect.Apply();
            }

            // DEACTIVATED : TO BE IMPLEMENTED
            // If the player has a temporal stability of less than 0.5, then the player's voice should be distorted
            // Values are temporary currently
            unstableEffect?.Clear();
            if (toBeImplementedToggle && !isGlobalBroadcast && player.Entity.WatchedAttributes.GetDouble("temporalStability") < 0.5)
            {
                unstableEffect = unstableEffect ?? new UnstableEffect(source);
                unstableEffect.Apply();
            }

            // DEACTIVATED : TO BE IMPLEMENTED
            // If the player is drunk, then the player's voice should be affected
            // Values are temporary currently
            intoxicatedEffect?.Clear();
            float drunkness = player.Entity.WatchedAttributes.GetFloat("intoxication");
            if (toBeImplementedToggle && !isGlobalBroadcast && drunkness > 0)
            {
                intoxicatedEffect = intoxicatedEffect ?? new IntoxicatedEffect(source);
                intoxicatedEffect.SetToxicRate(drunkness);
                intoxicatedEffect.Apply();
            }

            float gain = GetFinalGain();
            var sourcePosition = new Vec3f();
            var velocity = new Vec3f();

            // For global broadcasts, disable audio positioning
            bool useLocationalAudio = IsLocational && !isGlobalBroadcast && !ModConfig.ClientConfig.IsMonoMode;

            if (useLocationalAudio)
            {
                sourcePosition = GetRelativeSourcePosition(speakerPos, listenerPos);
                velocity = GetRelativeVelocity(speakerPos, listenerPos, sourcePosition);
            }
            else if (ModConfig.ClientConfig.IsMonoMode && !isGlobalBroadcast)
            {
                // In mono mode, preserve distance but center the audio (no stereo positioning)
                float distance = (float)speakerPos.DistanceTo(listenerPos);
                sourcePosition = new Vec3f(0, 0, distance); // Position in front of listener at correct distance
                velocity = new Vec3f(); // No velocity in mono mode
            }

            OALW.ClearError();
            OALW.Source(source, ALSourcef.Gain, gain);
            OALW.Source(source, ALSource3f.Position, sourcePosition.X, sourcePosition.Y, sourcePosition.Z);
            OALW.Source(source, ALSource3f.Velocity, velocity.X, velocity.Y, velocity.Z);
            OALW.Source(source, ALSourceb.SourceRelative, true);
        }

        private bool _IsPlaying()
        {
            if (source <= 0) return false; // Source is invalid
            return OALW.GetSourceState(source) == ALSourceState.Playing;
        }

        private float GetFinalGain()
        {
            var globalGain = Math.Clamp(PlayerListener.VoiceGain, 0, MaxGain);
            var sourceGain = clientSettingsRepo.GetPlayerGain(player.PlayerUID);
            var finalGain = GameMath.Clamp(globalGain * sourceGain, 0, MaxGain);

            return finalGain;
        }

        private float GetDistanceFactor()
        {
            //  If it is a global broadcast, completely disable distance reduction
            if (currentAudio?.isGlobalBroadcast == true)
            {
                return 0f;
            }

            // If the current audio ignores distance reduction, return 0 (no reduction)
            if (currentAudio?.ignoreDistanceReduction == true)
            {
                return 0f;
            }

            const float quietDistance = 10;

            float maxHearingDistance = WorldConfig.GetInt(voiceLevel);
            var exponent = quietDistance < maxHearingDistance ? 2 : -0.33;
            var distanceFactor = Math.Pow(quietDistance / maxHearingDistance, exponent);
            return (float)distanceFactor;
        }

        private Vec3f GetRelativeSourcePosition(EntityPos speakerPos, EntityPos listenerPos)
        {
            var relativeSourcePosition = LocationUtils.GetRelativeSpeakerLocation(speakerPos, listenerPos);
            return relativeSourcePosition;
        }

        private Vec3f GetRelativeVelocity(EntityPos speakerPos, EntityPos listenerPos, Vec3f relativeSpeakerPosition)
        {
            var speakerVelocity = GetVelocity(speakerPos);
            var futureSpeakerPosition = speakerPos.XYZFloat + speakerVelocity;
            var relativeFuturePosition = LocationUtils.GetRelativeSpeakerLocation(futureSpeakerPosition, listenerPos);
            var relativeVelocity = relativeSpeakerPosition - relativeFuturePosition;

            return relativeVelocity;
        }

        private Vec3f GetVelocity(EntityPos speakerPos)
        {
            var currentTime = DateTime.Now;
            if (lastSpeakerUpdate == null) lastSpeakerUpdate = currentTime;
            var dt = (currentTime - (DateTime)lastSpeakerUpdate).TotalSeconds;
            dt = GameMath.Clamp(dt, 0.1, 1);

            var speakerCoords = speakerPos.XYZFloat;
            if (lastSpeakerCoords == null || dt == 1) lastSpeakerCoords = speakerCoords;

            var velocity = (lastSpeakerCoords - speakerCoords) / (float)dt;
            lastSpeakerCoords = speakerCoords;
            lastSpeakerUpdate = currentTime;

            return velocity;
        }

        public void EnqueueAudio(AudioData audio, long sequenceNumber)
        {
            lock (ordering_queue_lock)
            {
                if (orderingQueue.ContainsKey(sequenceNumber)) return;

                if (lastAudioSequenceNumber >= sequenceNumber)
                {
                    Logger.client.VerboseDebug($"Audio sequence {sequenceNumber} arrived too late, skipping enqueueing");
                    return;
                }

                orderingQueue.Add(sequenceNumber, audio);
            }

            // Use synchronous processing instead of fire-and-forget async tasks
            // This prevents creating N+ concurrent async tasks that cause CPU spikes
            ProcessNextAudioPacket();
        }

        // Synchronous processing to prevent CPU spikes from concurrent async tasks
        private void ProcessNextAudioPacket()
        {
            try
            {
                AudioData audio;
                lock (ordering_queue_lock)
                {
                    if (orderingQueue.Count == 0) return;

                    lastAudioSequenceNumber = orderingQueue.Keys[0];
                    audio = orderingQueue[lastAudioSequenceNumber];
                    orderingQueue.RemoveAt(0);
                }

                currentAudio = audio;
                UpdateVoiceLevel(audio.voiceLevel);

                if (codec != null)
                    audio.data = codec.Decode(audio.data);

                if (audio.data == null || audio.data.Length == 0)
                {
                    Logger.client.Warning("Received empty audio data, skipping");
                    return;
                }

                float finalGain = GetFinalGain();

                PcmUtils.ApplyGainWithSoftClipping(ref audio.data, audio.format, finalGain);
                PcmUtils.ApplyCompressor(ref audio.data, audio.format);

                // Consistent audio processing to prevent CPU spikes
                // Apply fade edges for all audio types to maintain consistent processing load
                int maxFadeDuration = Math.Min(
                    2 * audio.frequency / 1000,
                    audio.data.Length / 4
                );

                if (audio.data.Length > maxFadeDuration * 2)
                {
                    // Reduce fade intensity for global broadcasts instead of skipping entirely
                    if (audio.isGlobalBroadcast)
                    {
                        AudioUtils.FadeEdges(audio.data, maxFadeDuration / 2); // Reduced fade
                    }
                    else
                    {
                        AudioUtils.FadeEdges(audio.data, maxFadeDuration); // Normal fade
                    }
                }

                buffer.QueueAudio(audio.data, audio.format, audio.frequency);

                // The source can stop playing if it finishes everything in queue
                if (source > 0) // Check if source is still valid
                {
                    var state = OALW.GetSourceState(source);
                    if (state != ALSourceState.Playing)
                        StartPlaying();
                }
            }
            catch (Exception e)
            {
                Logger.client.Warning($"Error in ProcessNextAudioPacket: {e.Message}");
            }
        }

        // Keep the async method for backward compatibility, but make it call the sync version
        public async Task DequeueAudio()
        {
            await Task.Delay(orderingDelay);
            ProcessNextAudioPacket();
        }

        public void StartPlaying()
        {
            if (source <= 0) return; // Source is invalid
            OALW.SourcePlay(source);
            PlayerNameTagRenderer.UpdatePlayerNameTag(player, true);
        }

        public void StopPlaying()
        {
            if (source <= 0) return; // Source is invalid
            OALW.SourceStop(source);
            OnSourceStop();
        }

        private void OnSourceStop()
        {
            PlayerNameTagRenderer.UpdatePlayerNameTag(player, false);
        }

        public void Dispose()
        {
            if (IsDisposed) return;

            OALW.SourceStop(source);
            OALW.DeleteSource(source);
            source = 0; // Mark source as invalid
            buffer.OnEmptyingQueue -= OnSourceStop;
            currentSoundEffect?.Clear();
            buffer?.Dispose();

            IsDisposed = true;
        }

        public void SetSoundEffect(string effectName)
        {
            if (string.IsNullOrWhiteSpace(effectName) || currentEffectName == effectName)
                return;

            // Check if source is still valid before creating effects
            if (source <= 0)
            {
                Logger.client.Warning("Cannot apply sound effect: source is invalid");
                return;
            }

            currentSoundEffect?.Clear();

            currentSoundEffect = SoundEffect.Create(effectName, source);
            currentSoundEffect?.Apply();

            currentEffectName = effectName;
        }

        public void ClearSoundEffect()
        {
            currentSoundEffect?.Clear();
            currentSoundEffect = null;
            currentEffectName = null;
        }
    }
}