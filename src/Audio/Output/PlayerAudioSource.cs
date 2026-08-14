using OpenTK.Audio.OpenAL;
using RPVoiceChat.Audio.Effects;
using RPVoiceChat.Config;
using RPVoiceChat.DB;
using RPVoiceChat.Gui;
using RPVoiceChat.Util;
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
        public bool IsSyntheticSource { get; }
        public float MaxGain => ServerConfigManager.MaxAudioGain;

        private const int BufferCount = 20;
        /// <summary>~400ms of 10ms frames in OpenAL — enough to ride network jitter.</summary>
        private const int SyntheticBufferCount = 40;
        /// <summary>Max packets waiting to enter OpenAL (~800ms). Drop oldest only beyond this.</summary>
        private const int SyntheticMaxQueuedFrames = 80;
        /// <summary>Start playback once we have this many frames (or after the jitter timeout).</summary>
        private const int SyntheticPrimeFrames = 20;
        private const int SyntheticPrimeTimeoutMs = 250;
        private int source;
        public int SourceId => source;
        private CircularAudioBuffer buffer;
        private SortedList<long, AudioData> orderingQueue = new SortedList<long, AudioData>();
        private object ordering_queue_lock = new object();
        private object dequeue_audio_lock = new object();
        private int orderingDelay = 50; // Reduced from 100ms to 50ms for lower latency and fewer async tasks
        private bool syntheticPlaybackPrimed;
        private long lastAudioSequenceNumber = -1;
        private bool dequeueTaskRunning = false; // Prevent multiple concurrent dequeue tasks
        private bool playbackEndCheckRunning = false;
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
        private Vec3f lastSpeakerCoords;
        private DateTime? lastSpeakerUpdate;
        private AudioData currentAudio; // Store current audio data for distance factor calculation
        
        private int? _lastQueuedNametagRenderRange;

        // Performance optimization: throttle expensive calculations
        private DateTime? lastFullUpdate;
        private DateTime? lastWallThicknessUpdate;
        private float cachedWallThickness = 0f;
        private const int FullUpdateIntervalMs = 50; // Update position/velocity every 50ms (20 Hz)
        private const int WallThicknessUpdateIntervalMs = 200; // Update wall thickness every 200ms (5 Hz)

        // Sound Physics Adapted runs its raycaster on the main thread only, but UpdatePlayer
        // runs on a network thread. The query is queued and its result cached for the next pass.
        private volatile float cachedSoundPhysicsGainHF = 1f;
        private volatile bool soundPhysicsQueryPending;

        public PlayerAudioSource(IPlayer player, ICoreClientAPI capi, ClientSettingsRepository clientSettingsRepo)
            : this(player, capi, clientSettingsRepo, syntheticSourceId: null)
        {
        }

        public PlayerAudioSource(
            IPlayer player,
            ICoreClientAPI capi,
            ClientSettingsRepository clientSettingsRepo,
            string syntheticSourceId)
        {
            this.player = player;
            this.capi = capi;
            this.clientSettingsRepo = clientSettingsRepo;
            IsSyntheticSource = !string.IsNullOrWhiteSpace(syntheticSourceId);
            // Voice: low latency. Program streams use an explicit prime buffer in DequeueAudio.
            orderingDelay = IsSyntheticSource ? SyntheticPrimeTimeoutMs : 50;

            lastSpeakerCoords = player.Entity?.Pos?.XYZFloat;
            lastSpeakerUpdate = DateTime.Now;

            source = OALW.GenSource();
            buffer = new CircularAudioBuffer(source, IsSyntheticSource ? SyntheticBufferCount : BufferCount);
            buffer.OnEmptyingQueue += OnSourceStop;

            float gain = GetFinalGain();
            OALW.Source(source, ALSourceb.Looping, false);
            OALW.Source(source, ALSourceb.SourceRelative, true);
            OALW.Source(source, ALSourcef.Gain, gain);
            OALW.Source(source, ALSourcef.Pitch, 1.0f);
            // Distance fade is applied manually in UpdatePlayer (OpenAL inverse-distance
            // keeps full volume until ReferenceDistance, which feels stepped).
            OALW.Source(source, ALSourcef.ReferenceDistance, 1f);
            OALW.Source(source, ALSourcef.RolloffFactor, 0f);

            UpdateVoiceLevel(voiceLevel);
        }

        public void PrepareForPacket(AudioData audio)
        {
            currentAudio = audio;
        }

        public void UpdateVoiceLevel(VoiceLevel voiceLevel)
        {
            this.voiceLevel = voiceLevel;
            TryApplyNametagRenderRange();
        }

        private void TryApplyNametagRenderRange()
        {
            if (IsSyntheticSource) return;

            bool dynamicRange = WorldConfig.GetBool("use-nametag-dynamic-range", true);
            int targetRange = dynamicRange
                ? WorldConfig.GetInt(voiceLevel)
                : WorldConfig.GetInt("nametag-fallback-range", ServerConfigManager.NametagFallbackRenderRange);
            if (_lastQueuedNametagRenderRange == targetRange) return;
            _lastQueuedNametagRenderRange = targetRange;
            PlayerNameTagRenderer.SetNametagRenderRange(player, targetRange);
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

        public void UpdatePlayer()
        {
            EntityPos speakerPos = player.Entity?.Pos;
            EntityPos listenerPos = capi.World.Player.Entity?.Pos;
            if (speakerPos == null || listenerPos == null)
                return;

            TryApplyNametagRenderRange();

            Vec3d sourceOverride = currentAudio?.sourcePosOverride;
            Vec3d effectiveSpeakerPos = sourceOverride ?? new Vec3d(speakerPos.X, speakerPos.Y, speakerPos.Z);

            DateTime now = DateTime.Now;
            bool shouldDoFullUpdate = lastFullUpdate == null || 
                (now - lastFullUpdate.Value).TotalMilliseconds >= FullUpdateIntervalMs;
            bool shouldUpdateWallThickness = lastWallThicknessUpdate == null || 
                (now - lastWallThicknessUpdate.Value).TotalMilliseconds >= WallThicknessUpdateIntervalMs;

            // Cache wall thickness calculation (very expensive ray tracing)
            float wallThickness = cachedWallThickness;
            if (shouldUpdateWallThickness)
            {
                bool mufflingEnabled = ModConfig.ClientConfig.Muffling;
                if (mufflingEnabled)
                {
                    if (sourceOverride != null)
                    {
                        wallThickness = LocationUtils.GetWallThickness(capi, sourceOverride, LocationUtils.GetLocationOfPlayer(capi.World.Player));
                    }
                    else
                    {
                        wallThickness = LocationUtils.GetWallThickness(capi, player, capi.World.Player);
                    }
                    if (capi.World.Player.Entity.Swimming)
                        wallThickness += 1.0f;
                    cachedWallThickness = wallThickness;
                    lastWallThicknessUpdate = now;
                }
                else
                {
                    wallThickness = 0f;
                    cachedWallThickness = 0f;
                }
            }
            else if (capi.World.Player.Entity.Swimming && cachedWallThickness > 0)
            {
                // Apply swimming modifier to cached value
                wallThickness = cachedWallThickness + 1.0f;
            }

            // Update lowpass filter only when wall thickness changes
            if (shouldUpdateWallThickness)
            {
                bool mufflingEnabled = ModConfig.ClientConfig.Muffling;

                lowpassFilter?.Stop();
                if (mufflingEnabled)
                {
                    float gainHF = 1f;

                    // Prefer Sound Physics Adapted's material-aware occlusion when the mod is
                    // installed, allowed by the server, and currently available. It returns a
                    // drop-in gainHF in the same 0.001..1.0 range as our built-in muffling.
                    bool usedSoundPhysics = false;
                    if (WorldConfig.GetBool("use-sound-physics-adapted", true) && SoundPhysicsCompat.IsAvailable)
                    {
                        Vec3d speakerLocation = sourceOverride ?? LocationUtils.GetLocationOfPlayer(player);
                        Vec3d listenerLocation = LocationUtils.GetLocationOfPlayer(capi.World.Player);
                        QueueSoundPhysicsQuery(speakerLocation, listenerLocation);
                        gainHF = cachedSoundPhysicsGainHF;
                        usedSoundPhysics = true;
                    }

                    // Fall back to RPVoiceChat's built-in wall-thickness muffling.
                    if (!usedSoundPhysics && wallThickness != 0)
                    {
                        float wallThicknessWeighting = WorldConfig.GetFloat("wall-thickness-weighting");
                        gainHF = Math.Max(1.0f - (wallThickness / wallThicknessWeighting), 0.1f);
                    }

                    if (gainHF < 1f)
                    {
                        lowpassFilter = lowpassFilter ?? new LowpassFilter(source);
                        lowpassFilter.Start();
                        lowpassFilter.SetHFGain(gainHF);
                    }
                }
                else
                {
                    cachedSoundPhysicsGainHF = 1f;
                }
            }

            // Skip expensive position/velocity updates if not needed
            if (!shouldDoFullUpdate)
                return;

            lastFullUpdate = now;

            bool toBeImplementedToggle = false;
            // DEACTIVATED : TO BE IMPLEMENTED
            // If the player is in a reverberated area, then the player's voice should be reverberated
            reverbEffect?.Clear();
            if (toBeImplementedToggle && LocationUtils.IsReverbArea(capi, speakerPos))
            {
                reverbEffect = reverbEffect ?? new ReverbEffect(source);
                reverbEffect.Apply();
            }

            // DEACTIVATED : TO BE IMPLEMENTED
            // If the player has a temporal stability of less than 0.5, then the player's voice should be distorted
            // Values are temporary currently
            unstableEffect?.Clear();
            if (toBeImplementedToggle && player.Entity.WatchedAttributes.GetDouble("temporalStability") < 0.5)
            {
                unstableEffect = unstableEffect ?? new UnstableEffect(source);
                unstableEffect.Apply();
            }

            // DEACTIVATED : TO BE IMPLEMENTED
            // If the player is drunk, then the player's voice should be affected
            // Values are temporary currently
            intoxicatedEffect?.Clear();
            float drunkness = player.Entity.WatchedAttributes.GetFloat("intoxication");
            if (toBeImplementedToggle && drunkness > 0)
            {
                intoxicatedEffect = intoxicatedEffect ?? new IntoxicatedEffect(source);
                intoxicatedEffect.SetToxicRate(drunkness);
                intoxicatedEffect.Apply();
            }

            float gain = GetFinalGain() * GetDistanceAttenuationGain(effectiveSpeakerPos, listenerPos);
            var sourcePosition = new Vec3f();
            var velocity = new Vec3f();

            // For mono mode, preserve distance but center the audio (no stereo positioning)
            bool useLocationalAudio = IsLocational && !ModConfig.ClientConfig.IsMonoMode;

            if (useLocationalAudio)
            {
                sourcePosition = GetRelativeSourcePosition(effectiveSpeakerPos, listenerPos);
                velocity = GetRelativeVelocity(effectiveSpeakerPos, listenerPos, sourcePosition);
            }
            else if (ModConfig.ClientConfig.IsMonoMode)
            {
                // In mono mode, preserve distance but center the audio (no stereo positioning)
                float distance = (float)effectiveSpeakerPos.DistanceTo(listenerPos.XYZ);
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

        /// <summary>
        /// Ask Sound Physics Adapted for the occlusion between the speaker and the listener.
        /// Its raycaster reads the world and keeps shared state, so it must run on the main
        /// thread. The result lands in <see cref="cachedSoundPhysicsGainHF"/> for the next
        /// muffling update. Only one query per source is in flight at a time.
        /// </summary>
        private void QueueSoundPhysicsQuery(Vec3d speakerLocation, Vec3d listenerLocation)
        {
            if (soundPhysicsQueryPending) return;
            soundPhysicsQueryPending = true;

            capi.Event.EnqueueMainThreadTask(() =>
            {
                try
                {
                    cachedSoundPhysicsGainHF = SoundPhysicsCompat.GetOcclusionGainHF(speakerLocation, listenerLocation);
                }
                finally
                {
                    soundPhysicsQueryPending = false;
                }
            }, "rpvoicechat:SoundPhysicsOcclusion");
        }

        private float GetFinalGain()
        {
            var globalGain = Math.Clamp(PlayerListener.VoiceGain, 0, MaxGain);
            var sourceGain = IsSyntheticSource ? 1f : clientSettingsRepo.GetPlayerGain(player.PlayerUID);
            var finalGain = GameMath.Clamp(globalGain * sourceGain, 0, MaxGain);

            return finalGain;
        }

        /// <summary>
        /// Smooth distance fade from contact to max range (no near-field plateau).
        /// </summary>
        private float GetDistanceAttenuationGain(Vec3d speakerPos, EntityPos listenerPos)
        {
            if (currentAudio?.isGlobalBroadcast == true || currentAudio?.ignoreDistanceReduction == true)
            {
                return 1f;
            }

            float maxHearingDistance = currentAudio?.effectiveRange > 0
                ? currentAudio.effectiveRange
                : WorldConfig.GetInt(voiceLevel);

            if (maxHearingDistance <= 0.01f)
            {
                return 0f;
            }

            float distance = (float)speakerPos.DistanceTo(listenerPos.XYZ);
            float t = GameMath.Clamp(distance / maxHearingDistance, 0f, 1f);

            // Gradual from the first step away; ~6% remaining at max hearing distance.
            const float edgeGain = 0.06f;
            float shaped = (float)Math.Pow(1.0 - t, 1.35);
            return edgeGain + (1f - edgeGain) * shaped;
        }

        private Vec3f GetRelativeSourcePosition(Vec3d speakerPos, EntityPos listenerPos)
        {
            var relativeSourcePosition = LocationUtils.GetRelativeSpeakerLocation(speakerPos.ToVec3f(), listenerPos);
            return relativeSourcePosition;
        }

        private Vec3f GetRelativeVelocity(Vec3d speakerPos, EntityPos listenerPos, Vec3f relativeSpeakerPosition)
        {
            var speakerVelocity = GetVelocity(speakerPos);
            var futureSpeakerPosition = speakerPos.ToVec3f() + speakerVelocity;
            var relativeFuturePosition = LocationUtils.GetRelativeSpeakerLocation(futureSpeakerPosition, listenerPos);
            var relativeVelocity = relativeSpeakerPosition - relativeFuturePosition;

            return relativeVelocity;
        }

        private Vec3f GetVelocity(Vec3d speakerPos)
        {
            var currentTime = DateTime.Now;
            if (lastSpeakerUpdate == null) lastSpeakerUpdate = currentTime;
            var dt = (currentTime - (DateTime)lastSpeakerUpdate).TotalSeconds;
            dt = GameMath.Clamp(dt, 0.1, 1);

            var speakerCoords = speakerPos.ToVec3f();
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

                // New program session after stop/start: sequence jumps backward relative to previous run.
                if (lastAudioSequenceNumber >= 0 && sequenceNumber + 50 < lastAudioSequenceNumber)
                {
                    orderingQueue.Clear();
                    lastAudioSequenceNumber = -1;
                    syntheticPlaybackPrimed = false;
                }

                if (lastAudioSequenceNumber >= sequenceNumber)
                {
                    Logger.client.VerboseDebug($"Audio sequence {sequenceNumber} arrived too late, skipping enqueueing");
                    return;
                }

                orderingQueue.Add(sequenceNumber, audio);

                // Catch up to live only if flooded — do not trim the jitter buffer aggressively.
                if (IsSyntheticSource)
                {
                    while (orderingQueue.Count > SyntheticMaxQueuedFrames)
                    {
                        orderingQueue.RemoveAt(0);
                    }
                }
            }

            if (!dequeueTaskRunning)
            {
                DequeueAudio();
            }
        }

        public async void DequeueAudio()
        {
            lock (dequeue_audio_lock)
            {
                if (dequeueTaskRunning) return;
                dequeueTaskRunning = true;
            }

            try
            {
                if (IsSyntheticSource)
                {
                    await DrainSyntheticAudioAsync();
                }
                else
                {
                    await DrainVoiceAudioAsync();
                }
            }
            catch (Exception e)
            {
                Logger.client.Warning($"Error in DequeueAudio: {e.Message}");
            }
            finally
            {
                lock (dequeue_audio_lock)
                {
                    dequeueTaskRunning = false;
                }
            }
        }

        /// <summary>
        /// Continuous program/HLS path: prime a jitter buffer, then feed OpenAL as fast as it
        /// accepts frames. OpenAL clocks playback — never sleep between successful queues.
        /// </summary>
        private async Task DrainSyntheticAudioAsync()
        {
            if (!syntheticPlaybackPrimed)
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(SyntheticPrimeTimeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    lock (ordering_queue_lock)
                    {
                        if (orderingQueue.Count >= SyntheticPrimeFrames)
                        {
                            break;
                        }
                    }

                    await Task.Delay(10);
                }

                syntheticPlaybackPrimed = true;
            }

            while (true)
            {
                AudioData audio;
                lock (ordering_queue_lock)
                {
                    if (orderingQueue.Count == 0)
                    {
                        // Keep primed while OpenAL still has audio — brief packet gaps must not re-prime.
                        SchedulePlaybackEndCheck();
                        return;
                    }

                    lastAudioSequenceNumber = orderingQueue.Keys[0];
                    audio = orderingQueue[lastAudioSequenceNumber];
                    orderingQueue.RemoveAt(0);
                }

                if (!TryPreparePcm(ref audio))
                {
                    continue;
                }

                while (!buffer.TryQueueAudio(audio.data, audio.format, audio.frequency))
                {
                    await Task.Delay(5);
                }

                EnsurePlaying();
            }
        }

        private async Task DrainVoiceAudioAsync()
        {
            while (true)
            {
                await Task.Delay(orderingDelay);

                AudioData audio;
                lock (ordering_queue_lock)
                {
                    if (orderingQueue.Count == 0)
                    {
                        SchedulePlaybackEndCheck();
                        return;
                    }

                    lastAudioSequenceNumber = orderingQueue.Keys[0];
                    audio = orderingQueue[lastAudioSequenceNumber];
                    orderingQueue.RemoveAt(0);
                }

                if (!TryPreparePcm(ref audio))
                {
                    continue;
                }

                buffer.QueueAudio(audio.data, audio.format, audio.frequency);
                EnsurePlaying();
            }
        }

        private bool TryPreparePcm(ref AudioData audio)
        {
            currentAudio = audio;
            UpdateVoiceLevel(audio.voiceLevel);

            if (codec != null)
            {
                audio.data = codec.Decode(audio.data);
            }

            if (audio.data == null || audio.data.Length == 0)
            {
                Logger.client.Warning("Received empty audio data, skipping");
                return false;
            }

            float finalGain = GetFinalGain();
            PcmUtils.ApplyGainWithSoftClipping(ref audio.data, audio.format, finalGain);

            // Per-frame compressor/fade is for short voice bursts — skip on continuous program streams.
            if (!audio.isGlobalBroadcast && !IsSyntheticSource)
            {
                PcmUtils.ApplyCompressor(ref audio.data, audio.format);

                int maxFadeDuration = Math.Min(
                    2 * audio.frequency / 1000,
                    audio.data.Length / 4
                );
                if (audio.data.Length > maxFadeDuration * 2)
                {
                    AudioUtils.FadeEdges(audio.data, maxFadeDuration);
                }
            }

            return true;
        }

        private void EnsurePlaying()
        {
            if (source <= 0)
            {
                return;
            }

            var state = OALW.GetSourceState(source);
            if (state != ALSourceState.Playing)
            {
                StartPlaying();
                NotifyStartedSpeaking();
            }
        }

        private async void SchedulePlaybackEndCheck()
        {
            lock (dequeue_audio_lock)
            {
                if (playbackEndCheckRunning) return;
                playbackEndCheckRunning = true;
            }

            try
            {
                // Wait until OpenAL naturally drains the last queued buffers.
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(75);

                    bool hasPendingPackets;
                    lock (ordering_queue_lock)
                    {
                        hasPendingPackets = orderingQueue.Count > 0;
                    }
                    if (hasPendingPackets)
                    {
                        DequeueAudio();
                        return;
                    }

                    if (source <= 0) return;

                    var state = OALW.GetSourceState(source);
                    if (state != ALSourceState.Playing)
                    {
                        if (IsSyntheticSource)
                        {
                            syntheticPlaybackPrimed = false;
                        }

                        OnSourceStop();
                        return;
                    }
                }

                if (IsSyntheticSource)
                {
                    syntheticPlaybackPrimed = false;
                }
            }
            catch (Exception e)
            {
                Logger.client.Warning($"Error while checking playback end: {e.Message}");
            }
            finally
            {
                lock (dequeue_audio_lock)
                {
                    playbackEndCheckRunning = false;
                }
            }
        }


        public void StartPlaying()
        {
            if (source <= 0) return; // Source is invalid

            OALW.SourcePlay(source);
        }

        private void NotifyStartedSpeaking()
        {
            if (IsSyntheticSource) return;
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
            if (IsSyntheticSource) return;
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