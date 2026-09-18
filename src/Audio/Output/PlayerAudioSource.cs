using OpenTK.Audio.OpenAL;
using RPVoiceChat.Audio.Effects;
using RPVoiceChat.Config;
using RPVoiceChat.DB;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Items;
using RPVoiceChat.Gui;
using RPVoiceChat.Util;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Audio
{
    public class PlayerAudioSource : IDisposable
    {
        public volatile bool IsDisposed = false;
        public bool IsPlaying { get => _IsPlaying(); }
        public bool IsSpeaking => speakingNotificationActive && !IsDisposed;
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
        private readonly VoicePlayoutBuffer voiceBuffer = new();
        private readonly object playbackLock = new();
        private SortedList<long, AudioData> orderingQueue = new SortedList<long, AudioData>();
        private object ordering_queue_lock = new object();
        private object dequeue_audio_lock = new object();
        private bool fadeVoiceOnset;
        private bool syntheticPlaybackPrimed;
        private long lastAudioSequenceNumber = -1;
        private bool dequeueTaskRunning = false; // Prevent multiple concurrent dequeue tasks
        private bool playbackEndCheckRunning = false;
        private volatile bool speakingNotificationActive;
        private long lastAudioReceivedAt;
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
        private bool forceFlatPlayback;

        private int? _lastQueuedNametagRenderRange;

        // Performance optimization: throttle expensive calculations
        private DateTime? lastFullUpdate;
        private DateTime? lastWallThicknessUpdate;
        private volatile float cachedWallThickness = 0f;
        private const int FullUpdateIntervalMs = 50; // Update position/velocity every 50ms (20 Hz)
        private const int WallThicknessUpdateIntervalMs = 200; // Update wall thickness every 200ms (5 Hz)

        // Occlusion queries use the shared main-thread budget; playback only reads the cache.
        private volatile float cachedSoundPhysicsGainHF = 1f;
        private volatile bool occlusionQueryPending;
        private readonly VoiceOcclusionScheduler occlusionScheduler;

        public PlayerAudioSource(IPlayer player, ICoreClientAPI capi, ClientSettingsRepository clientSettingsRepo)
            : this(player, capi, clientSettingsRepo, syntheticSourceId: null)
        {
        }

        public PlayerAudioSource(
            IPlayer player,
            ICoreClientAPI capi,
            ClientSettingsRepository clientSettingsRepo,
            string syntheticSourceId, VoiceOcclusionScheduler occlusionScheduler = null)
        {
            this.player = player;
            this.occlusionScheduler = occlusionScheduler;
            this.capi = capi;
            this.clientSettingsRepo = clientSettingsRepo;
            IsSyntheticSource = !string.IsNullOrWhiteSpace(syntheticSourceId);
            // Voice: low latency. Program streams use an explicit prime buffer in DequeueAudio.

            lastSpeakerCoords = player.Entity?.Pos?.XYZFloat;
            lastSpeakerUpdate = DateTime.Now;

            source = OALW.GenSource();
            buffer = new CircularAudioBuffer(source, IsSyntheticSource ? SyntheticBufferCount : BufferCount);

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
            if (listenerPos == null)
                return;

            if (forceFlatPlayback || currentAudio?.forceFlatPlayback == true)
            {
                ApplyFlatPlayback(GetFinalGain());
                return;
            }

            if (speakerPos == null && currentAudio?.sourcePosOverride == null)
            {
                ApplyFlatPlayback(0f);
                return;
            }

            TryApplyNametagRenderRange();

            Vec3d sourceOverride = currentAudio?.sourcePosOverride;
            Vec3d effectiveSpeakerPos = sourceOverride ?? new Vec3d(speakerPos.X, speakerPos.Y, speakerPos.Z);

            DateTime now = DateTime.Now;
            bool shouldDoFullUpdate = lastFullUpdate == null ||
                (now - lastFullUpdate.Value).TotalMilliseconds >= FullUpdateIntervalMs;
            bool shouldUpdateWallThickness = lastWallThicknessUpdate == null ||
                (now - lastWallThicknessUpdate.Value).TotalMilliseconds >= WallThicknessUpdateIntervalMs;

            bool mufflingEnabled = ModConfig.ClientConfig.Muffling;

            // Sound Physics Adapted returns a gainHF in the same range as our own muffling,
            // so the wall thickness raycast below is not necessary while it supplies the value.
            bool useSoundPhysics = mufflingEnabled
                && WorldConfig.GetBool("use-sound-physics-adapted", true)
                && SoundPhysicsCompatibility.IsAvailable;

            if (shouldUpdateWallThickness)
            {
                lastWallThicknessUpdate = now;
                if (mufflingEnabled)
                {
                    Vec3d speakerLocation = sourceOverride ?? LocationUtils.GetLocationOfPlayer(player);
                    Vec3d listenerLocation = LocationUtils.GetLocationOfPlayer(capi.World.Player);
                    QueueOcclusionQuery(speakerLocation, listenerLocation, useSoundPhysics);
                }
                else
                {
                    cachedWallThickness = 0f;
                    cachedSoundPhysicsGainHF = 1f;
                }

                lowpassFilter?.Stop();
                if (mufflingEnabled)
                {
                    float wallThickness = cachedWallThickness + (capi.World.Player.Entity.Swimming ? 1f : 0f);
                    float weighting = Math.Max(0.001f, WorldConfig.GetFloat("wall-thickness-weighting"));
                    float gainHF = useSoundPhysics ? cachedSoundPhysicsGainHF : Math.Max(1f - wallThickness / weighting, 0.1f);
                    if (gainHF < 1f)
                    {
                        lowpassFilter ??= new LowpassFilter(source);
                        lowpassFilter.Start();
                        lowpassFilter.SetHFGain(gainHF);
                    }
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
            if (sourceOverride != null && IsTalkieRfAtListener(sourceOverride, listenerPos))
            {
                gain *= ItemRadio.GetLocalTalkieListenVolumeGain(capi);
            }
            else if (sourceOverride != null)
            {
                float receiverGain = BlockEntityRadioReceiver.GetPlaybackVolumeGainAtSource(
                    capi,
                    sourceOverride,
                    listenerPos.Dimension);
                if (receiverGain >= 0f)
                {
                    gain *= receiverGain;
                }
            }
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

        private void ApplyFlatPlayback(float gain)
        {
            OALW.ClearError();
            OALW.Source(source, ALSourcef.Gain, gain);
            OALW.Source(source, ALSource3f.Position, 0f, 0f, 0f);
            OALW.Source(source, ALSource3f.Velocity, 0f, 0f, 0f);
            OALW.Source(source, ALSourceb.SourceRelative, true);
        }

        private bool _IsPlaying()
        {
            if (source <= 0) return false; // Source is invalid
            return OALW.GetSourceState(source) == ALSourceState.Playing;
        }

        private void QueueOcclusionQuery(Vec3d speakerLocation, Vec3d listenerLocation, bool useSoundPhysics)
        {
            if (occlusionQueryPending || occlusionScheduler == null) return;
            occlusionQueryPending = true;
            var steps = useSoundPhysics ? null : LocationUtils.GetWallThicknessSteps(capi, speakerLocation, listenerLocation).GetEnumerator();
            float thickness = 0;
            bool accepted = occlusionScheduler.TryEnqueue(() =>
            {
                if (IsDisposed || !ModConfig.ClientConfig.Muffling) return false;
                if (useSoundPhysics)
                {
                    cachedSoundPhysicsGainHF = SoundPhysicsCompatibility.GetOcclusionGainHF(speakerLocation, listenerLocation);
                    return false;
                }
                if (steps.MoveNext())
                {
                    thickness = steps.Current;
                    return true;
                }
                cachedWallThickness = thickness;
                return false;
            }, () =>
            {
                steps?.Dispose();
                occlusionQueryPending = false;
            });
            if (!accepted)
            {
                steps?.Dispose();
                occlusionQueryPending = false;
            }
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
            if (currentAudio?.isGlobalBroadcast == true)
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

            if (currentAudio != null && listenerPos.Dimension != currentAudio.sourceDimension) return 0f;
            if (distance >= maxHearingDistance) return 0f;
            if (currentAudio?.ignoreDistanceReduction == true) return 1f;
            return (float)Math.Pow(1.0 - t, 1.35);
        }

        private static bool IsTalkieRfAtListener(Vec3d sourceOverride, EntityPos listenerPos)
        {
            double dx = sourceOverride.X - listenerPos.X;
            double dy = sourceOverride.Y - listenerPos.Y;
            double dz = sourceOverride.Z - listenerPos.Z;
            return dx * dx + dy * dy + dz * dz <= 4.0;
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
            if (IsDisposed) return;
            Volatile.Write(ref lastAudioReceivedAt, Environment.TickCount64);
            if (!IsSyntheticSource)
            {
                voiceBuffer.Enqueue(audio, sequenceNumber);
                DequeueAudio();
                return;
            }
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

        public void SetForceFlatPlayback(bool forceFlat)
        {
            forceFlatPlayback = forceFlat;
        }

        public void DequeueAudio()
        {
            lock (dequeue_audio_lock)
            {
                if (IsDisposed || dequeueTaskRunning) return;
                dequeueTaskRunning = true;
            }

            // An async method runs on its caller until the first incomplete await. Timed
            // packets can already be due here, so explicitly leave the game/network thread.
            _ = Task.Run(DrainAudioAsync);
        }

        private async Task DrainAudioAsync()
        {
            try
            {
                if (IsSyntheticSource)
                {
                    await DrainSyntheticAudioAsync().ConfigureAwait(false);
                }
                else
                {
                    await DrainVoiceAudioAsync().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                voiceBuffer.Clear();
                Logger.client.Warning($"Error in DequeueAudio: {e.Message}");
            }
            finally
            {
                lock (dequeue_audio_lock)
                {
                    dequeueTaskRunning = false;
                }
                // Close the enqueue/worker-exit race without waiting for the end-of-playback poll.
                if (!IsDisposed && !IsSyntheticSource && voiceBuffer.HasPending) DequeueAudio();
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

                    await Task.Delay(10).ConfigureAwait(false);
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

                lock (playbackLock)
                {
                    if (IsDisposed) return;
                    if (!TryPreparePcm(ref audio)) continue;
                }
                while (!IsDisposed)
                {
                    lock (playbackLock)
                    {
                        if (IsDisposed) return;
                        if (buffer.TryQueueAudio(audio.data, audio.format, audio.frequency))
                        {
                            EnsurePlaying();
                            break;
                        }
                    }
                    await Task.Delay(5).ConfigureAwait(false);
                }
            }
        }

        private async Task DrainVoiceAudioAsync()
        {
            while (!IsDisposed)
            {
                AudioData audio;
                int waitMs;
                bool ready;
                lock (playbackLock)
                {
                    if (IsDisposed) return;
                    ready = voiceBuffer.TryTake(IsPlaying, out audio, out waitMs, out bool reset);
                    if (reset) { buffer.Reset(); codec = null; fadeVoiceOnset = true; }
                }
                if (!ready)
                {
                    if (waitMs == 0) { SchedulePlaybackEndCheck(); return; }
                    await Task.Delay(waitMs).ConfigureAwait(false);
                    continue;
                }
                lock (playbackLock)
                {
                    if (IsDisposed) return;
                    if (!TryPreparePcm(ref audio)) continue;
                }
                long deadline = Environment.TickCount64 + 100;
                bool queued = false;
                while (!IsDisposed && Environment.TickCount64 < deadline)
                {
                    lock (playbackLock)
                    {
                        if (IsDisposed) return;
                        if (buffer.TryQueueAudio(audio.data, audio.format, audio.frequency, 120))
                        {
                            EnsurePlaying();
                            queued = true;
                            break;
                        }
                    }
                    await Task.Delay(5).ConfigureAwait(false);
                }
                if (!queued && !IsDisposed) VoiceDiagnostics.Count("hardware-timeout-drop");
            }
        }

        private bool TryPreparePcm(ref AudioData audio)
        {
#if VOICE_DIAGNOSTICS
            long processingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            currentAudio = audio;
            forceFlatPlayback = audio.forceFlatPlayback;
            UpdateVoiceLevel(audio.voiceLevel);
            UpdateAudioFormat(audio.codec, audio.frequency, AudioUtils.ChannelsPerFormat(audio.format));
            UpdatePlayer();

            if (codec != null)
            {
                audio.data = codec.Decode(audio.data);
            }

            if (audio.data == null || audio.data.Length == 0)
            {
                Logger.client.Warning("Received empty audio data, skipping");
                return false;
            }

            if (!IsSyntheticSource && audio.data.Length > audio.frequency * AudioUtils.ChannelsPerFormat(audio.format) * 2 / 5)
                return false;
            float finalGain = GetFinalGain();
            PcmUtils.ApplyGainWithSoftClipping(ref audio.data, audio.format, finalGain);

            // Per-frame compressor/fade is for short voice bursts — skip on continuous program streams.
            if (!audio.isGlobalBroadcast && !IsSyntheticSource)
            {
                PcmUtils.ApplyCompressor(ref audio.data, audio.format);

                if (fadeVoiceOnset)
                {
                    int channels = AudioUtils.ChannelsPerFormat(audio.format);
                    int fadeSamples = Math.Min(audio.frequency * 2 / 1000, audio.data.Length / (2 * channels));
                    for (int frame = 0; frame < fadeSamples; frame++)
                    {
                        for (int channel = 0; channel < channels; channel++)
                        {
                            int offset = (frame * channels + channel) * 2;
                            short value = BitConverter.ToInt16(audio.data, offset);
                            short faded = (short)(value * frame / Math.Max(1, fadeSamples - 1));
                            audio.data[offset] = (byte)faded;
                            audio.data[offset + 1] = (byte)(faded >> 8);
                        }
                    }
                    fadeVoiceOnset = false;
                }
            }

#if VOICE_DIAGNOSTICS
            VoiceDiagnostics.Peak("prepare-pcm-ms", System.Diagnostics.Stopwatch.GetElapsedTime(processingStarted).TotalMilliseconds);
#endif
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
                    await Task.Delay(75).ConfigureAwait(false);

                    bool hasPendingPackets;
                    lock (ordering_queue_lock)
                    {
                        hasPendingPackets = IsSyntheticSource ? orderingQueue.Count > 0 : voiceBuffer.HasPending;
                    }
                    if (hasPendingPackets)
                    {
                        DequeueAudio();
                        return;
                    }

                    lock (playbackLock)
                    {
                        if (IsDisposed || source <= 0) return;
                        var state = OALW.GetSourceState(source);
                        if (state != ALSourceState.Playing)
                        {
                            // Short 20ms packet underruns are not speech transitions. Rebuilding
                            // nametag textures on each underrun can stall rendering and input.
                            if (Environment.TickCount64 - Volatile.Read(ref lastAudioReceivedAt) < 150) continue;
                            if (IsSyntheticSource) syntheticPlaybackPrimed = false;
                            OnSourceStop();
                            return;
                        }
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
            if (IsSyntheticSource || speakingNotificationActive) return;
            speakingNotificationActive = true;
            PlayerNameTagRenderer.UpdatePlayerNameTag(player, true);
        }

        public void StopPlaying()
        {
            lock (playbackLock)
            {
                if (IsDisposed) return;

                if (source <= 0) return;
                OALW.SourceStop(source);
                OnSourceStop();
            }
        }

        private void OnSourceStop()
        {
            if (IsSyntheticSource) return;
            if (!speakingNotificationActive) return;
            speakingNotificationActive = false;
            PlayerNameTagRenderer.UpdatePlayerNameTag(player, false);
        }

        public void Dispose()
        {
            lock (playbackLock)
            {
                if (IsDisposed) return;
                IsDisposed = true;
                voiceBuffer.Clear();
                lock (ordering_queue_lock) orderingQueue.Clear();
                currentSoundEffect?.Clear();
                buffer?.Dispose();
                if (source > 0) OALW.DeleteSource(source);
                source = 0;
            }
        }

        public void SetSoundEffect(string effectName)
        {
            lock (playbackLock)
            {
                if (IsDisposed) return;

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
        }

        public void ClearSoundEffect()
        {
            lock (playbackLock)
            {
                if (IsDisposed) return;

                currentSoundEffect?.Clear();
                currentSoundEffect = null;
                currentEffectName = null;
            }
        }
    }
}
