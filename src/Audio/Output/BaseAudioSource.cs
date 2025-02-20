using OpenTK.Audio.OpenAL;
using RPVoiceChat.Audio;
using RPVoiceChat.Audio.Effects;
using RPVoiceChat.DB;
using RPVoiceChat.Gui;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.src.Audio.Output
{
    public abstract class BaseAudioSource : IDisposable
    {
        public bool IsDisposed = false;
        public bool IsPlaying { get => _IsPlaying(); }
        private const int BufferCount = 20;
        private int source;
        private CircularAudioBuffer buffer;
        private SortedList<long, AudioData> orderingQueue = new SortedList<long, AudioData>();
        private object ordering_queue_lock = new object();
        private object dequeue_audio_lock = new object();
        private int orderingDelay = 100;
        private long lastAudioSequenceNumber = -1;
        private IAudioCodec codec;
        private LowpassFilter lowpassFilter;
        private ReverbEffect reverbEffect;
        private IntoxicatedEffect intoxicatedEffect;
        private UnstableEffect unstableEffect;

        protected ICoreClientAPI capi;
        protected ClientSettingsRepository clientSettingsRepo;

        public Guid AudioSourceGuid { get; private set; }

        public bool IsLocational { get; set; } = true;
        public VoiceLevel voiceLevel { get; private set; } = VoiceLevel.Talking;
        private Dictionary<VoiceLevel, float> referenceDistanceByVoiceLevel = new Dictionary<VoiceLevel, float>()
        {
            { VoiceLevel.Whispering, 1.25f },
            { VoiceLevel.Talking, 2.25f },
            { VoiceLevel.Shouting, 6.25f },
        };
        private Vec3f lastSourceCoords;
        private DateTime? lastSourceUpdate;

        protected event Action OnSourceStopPlaying;
        protected event Action OnSourceStartPlaying;

        public BaseAudioSource(ICoreClientAPI capi, ClientSettingsRepository clientSettingsRepo, Vec3f sourceLocation)
        {
            AudioSourceGuid = Guid.NewGuid();

            this.capi = capi;
            this.clientSettingsRepo = clientSettingsRepo;

            lastSourceCoords = sourceLocation;
            lastSourceUpdate = DateTime.Now;

            source = OALW.GenSource();
            buffer = new CircularAudioBuffer(source, BufferCount);
            buffer.OnEmptyingQueue += OnSourceStopPlaying.Invoke;

            float gain = GetFinalGain();
            OALW.Source(source, ALSourceb.Looping, false);
            OALW.Source(source, ALSourceb.SourceRelative, true);
            OALW.Source(source, ALSourcef.Gain, gain);
            OALW.Source(source, ALSourcef.Pitch, 1.0f);

            UpdateVoiceLevel(voiceLevel);
        }

        // Must be implemented by child classes
        public abstract Vec3d? GetSourcePosition();

        public abstract float GetFinalGain();

        public void UpdateVoiceLevel(VoiceLevel voiceLevel)
        {
            this.voiceLevel = voiceLevel;
            float referenceDistance = referenceDistanceByVoiceLevel[voiceLevel];
            float distanceFactor = GetDistanceFactor();
            float rolloffFactor = referenceDistance * distanceFactor;

            OALW.Source(source, ALSourcef.ReferenceDistance, referenceDistance);
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


        public void UpdateAudioSource()
        {
            Vec3d sourcePos = GetSourcePosition();
            EntityPos listenerPos = capi.World.Player.Entity?.SidedPos;
            if (sourcePos == null || listenerPos == null)
                return;

            // If the source is on the other side of something to the listener, then the source's audio should be muffled
            bool mufflingEnabled = ClientSettings.Muffling;
            float wallThickness = LocationUtils.GetWallThickness(capi, sourcePos, capi.World.Player);
            float wallThicknessWeighting = WorldConfig.GetFloat("wallThicknessWeighting");
            if (capi.World.Player.Entity.Swimming)
                wallThickness += 1.0f;

            lowpassFilter?.Stop();
            if (mufflingEnabled && wallThickness != 0)
            {
                lowpassFilter = lowpassFilter ?? new LowpassFilter(source);
                lowpassFilter.Start();
                lowpassFilter.SetHFGain(Math.Max(1.0f - (wallThickness / wallThicknessWeighting), 0.1f));
            }

            bool toBeImplementedToggle = false;
            // DEACTIVATED : TO BE IMPLEMENTED
            // If the player is in a reverberated area, then the player's voice should be reverberated
            //reverbEffect?.Clear();
            //if (toBeImplementedToggle && LocationUtils.IsReverbArea(capi, sourcePos))
            //{
            //    reverbEffect = reverbEffect ?? new ReverbEffect(source);
            //    reverbEffect.Apply();
            //}

            // DEACTIVATED : TO BE IMPLEMENTED
            // If the player has a temporal stability of less than 0.5, then the player's voice should be distorted
            // Values are temporary currently
            //unstableEffect?.Clear();
            //if (toBeImplementedToggle && player.Entity.WatchedAttributes.GetDouble("temporalStability") < 0.5)
            //{
            //    unstableEffect = unstableEffect ?? new UnstableEffect(source);
            //    unstableEffect.Apply();
            //}

            // DEACTIVATED : TO BE IMPLEMENTED
            // If the player is drunk, then the player's voice should be affected
            // Values are temporary currently
            //intoxicatedEffect?.Clear();
            //float drunkness = player.Entity.WatchedAttributes.GetFloat("intoxication");
            //if (toBeImplementedToggle && drunkness > 0)
            //{
            //    intoxicatedEffect = intoxicatedEffect ?? new IntoxicatedEffect(source);
            //    intoxicatedEffect.SetToxicRate(drunkness);
            //    intoxicatedEffect.Apply();
            //}

            float gain = GetFinalGain();
            var sourcePosition = new Vec3f();
            var velocity = new Vec3f();
            if (IsLocational)
            {
                sourcePosition = GetRelativeSourcePosition(sourcePos, listenerPos);
                velocity = GetRelativeVelocity(sourcePos, listenerPos, sourcePosition);
            }

            OALW.ClearError();
            OALW.Source(source, ALSourcef.Gain, gain);
            OALW.Source(source, ALSource3f.Position, sourcePosition.X, sourcePosition.Y, sourcePosition.Z);
            OALW.Source(source, ALSource3f.Velocity, velocity.X, velocity.Y, velocity.Z);
            OALW.Source(source, ALSourceb.SourceRelative, true);
        }

        private bool _IsPlaying()
        {
            return OALW.GetSourceState(source) == ALSourceState.Playing;
        }

        private float GetDistanceFactor()
        {
            // Distance in blocks at which audio source normally considered quiet.
            const float quietDistance = 10;
            float maxHearingDistance = WorldConfig.GetInt(voiceLevel);
            var exponent = quietDistance < maxHearingDistance ? 2 : -0.33;
            var distanceFactor = Math.Pow(quietDistance / maxHearingDistance, exponent);

            return (float)distanceFactor;
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
            if (lastSourceUpdate == null) lastSourceUpdate = currentTime;
            var dt = (currentTime - (DateTime)lastSourceUpdate).TotalSeconds;
            dt = GameMath.Clamp(dt, 0.1, 1);

            var speakerCoords = speakerPos.ToVec3f();
            if (lastSourceCoords == null || dt == 1) lastSourceCoords = speakerCoords;

            var velocity = (lastSourceCoords - speakerCoords) / (float)dt;
            lastSourceCoords = speakerCoords;
            lastSourceUpdate = currentTime;

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

            DequeueAudio();
        }

        public async void DequeueAudio()
        {
            await Task.Delay(orderingDelay);

            lock (dequeue_audio_lock)
            {
                AudioData audio;
                lock (ordering_queue_lock)
                {
                    lastAudioSequenceNumber = orderingQueue.Keys[0];
                    audio = orderingQueue[lastAudioSequenceNumber];
                    orderingQueue.RemoveAt(0);
                }

                if (codec != null) audio.data = codec.Decode(audio.data);

                int maxFadeDuration = 2 * audio.frequency / 1000; // 2ms
                AudioUtils.FadeEdges(audio.data, maxFadeDuration);

                buffer.QueueAudio(audio.data, audio.format, audio.frequency);

                // The source can stop playing if it finishes everything in queue
                var state = OALW.GetSourceState(source);
                if (state != ALSourceState.Playing)
                    StartPlaying();
            }
        }

        public void StartPlaying()
        {
            OALW.SourcePlay(source);

            OnSourceStartPlaying?.Invoke();
        }

        public void StopPlaying()
        {
            OALW.SourceStop(source);

            OnSourceStopPlaying?.Invoke();
        }

        /// <summary>
        /// Disposes of the audio source and its resources. The base dispose method should be called by child classes.
        /// </summary>
        public virtual void Dispose()
        {
            if (IsDisposed) return;

            OALW.SourceStop(source);
            OALW.DeleteSource(source);
            buffer.OnEmptyingQueue -= OnSourceStopPlaying.Invoke;
            buffer?.Dispose();

            IsDisposed = true;
        }
    }
}
