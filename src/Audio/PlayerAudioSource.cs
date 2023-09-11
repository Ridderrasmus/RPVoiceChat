using System;
using OpenTK.Audio.OpenAL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using RPVoiceChat.Utils;
using System.Collections;
using System.Collections.Generic;

namespace RPVoiceChat.Audio
{
    public class PlayerAudioSource : IDisposable
    {
        public const int BufferCount = 20;

        private int source;

        public EffectsExtension EffectsExtension;

        private CircularAudioBuffer buffer;
        private SortedList orderingQueue = SortedList.Synchronized(new SortedList());
        private int orderingDelay = 100;
        private long lastAudioSequenceNumber = -1;

        private ICoreClientAPI capi;
        private AudioOutputManager outputManager;

        private Vec3f lastSpeakerCoords;
        private DateTime lastSpeakerUpdate;

        public bool IsLocational { get; set; } = true;
        public VoiceLevel voiceLevel { get; private set; } = VoiceLevel.Talking;
        private Dictionary<VoiceLevel, float> referenceDistanceByVoiceLevel = new Dictionary<VoiceLevel, float>()
        {
            { VoiceLevel.Whispering, 1.25f },
            { VoiceLevel.Talking, 2.25f },
            { VoiceLevel.Shouting, 6.25f },
        };

        private IPlayer player;

        private FilterLowpass lowpassFilter;

        public PlayerAudioSource(IPlayer player, AudioOutputManager manager, ICoreClientAPI capi)
        {
            EffectsExtension = manager.EffectsExtension;
            outputManager = manager;
            this.player = player;
            this.capi = capi;

            lastSpeakerCoords = player.Entity?.SidedPos?.XYZFloat;
            lastSpeakerUpdate = DateTime.Now;

            OALW.ExecuteInContext(() =>
            {
                source = AL.GenSource();
                OALW.CheckError("Error gen source");
                buffer = new CircularAudioBuffer(source, BufferCount);

                AL.Source(source, ALSourceb.Looping, false);
                OALW.CheckError("Error setting source looping");
                AL.Source(source, ALSourceb.SourceRelative, true);
                OALW.CheckError("Error setting source SourceRelative");
                AL.Source(source, ALSourcef.Gain, 1.0f);
                OALW.CheckError("Error setting source Gain");
                AL.Source(source, ALSourcef.Pitch, 1.0f);
                OALW.CheckError("Error setting source Pitch");
            });

            UpdateVoiceLevel(voiceLevel);
        }

        public void UpdateVoiceLevel(VoiceLevel voiceLevel)
        {
            this.voiceLevel = voiceLevel;
            float referenceDistance = referenceDistanceByVoiceLevel[voiceLevel];
            float distanceFactor = GetDistanceFactor();
            float rolloffFactor = referenceDistance * distanceFactor;

            OALW.ExecuteInContext(() =>
            {
                AL.Source(source, ALSourcef.ReferenceDistance, referenceDistance);
                OALW.CheckError("Error setting source ReferenceDistance");
                AL.Source(source, ALSourcef.RolloffFactor, rolloffFactor);
                OALW.CheckError("Error setting source RolloffFactor");
            });
        }

        public void UpdatePlayer()
        {
            EntityPos speakerPos = player.Entity?.SidedPos;
            EntityPos listenerPos = capi.World.Player.Entity?.SidedPos;
            if (speakerPos == null || listenerPos == null || !outputManager.isReady)
                return;

            // If the player is on the other side of something to the listener, then the player's voice should be muffled
            float wallThickness = LocationUtils.GetWallThickness(capi, player, capi.World.Player);
            if (capi.World.Player.Entity.Swimming)
                wallThickness += 1.0f;

            OALW.ExecuteInContext(() =>
            {
                lowpassFilter?.Stop();
                if (wallThickness != 0)
                {
                    lowpassFilter = lowpassFilter ?? new FilterLowpass(EffectsExtension, source);
                    lowpassFilter.Start();
                    lowpassFilter.SetHFGain(Math.Max(1.0f - (wallThickness / 2), 0.1f));
                }

                // If the player is in a reverberated area, then the player's voice should be reverberated
                bool isReverberated = false;
                if (isReverberated)
                {

                }

                // If the player has a temporal stability of less than 0.5, then the player's voice should be distorted
                // Values are temporary currently
                if (player.Entity.WatchedAttributes.GetDouble("temporalStability") < 0.5)
                {

                }

                /* --------- DISABLED FOR NOW ---------
                // If the player is drunk, then the player's voice should be affected
                // Values are temporary currently
                float drunkness = player.Entity.WatchedAttributes.GetFloat("intoxication");
                float pitch = drunkness <= 0.2 ? 1 : 1 - (drunkness / 5);
                AL.Source(source, ALSourcef.Pitch, pitch);
                OALW.CheckError("Error setting source Pitch");
                */

                var sourcePosition = new Vec3f();
                var velocity = new Vec3f();
                if (IsLocational)
                {
                    sourcePosition = GetRelativeSourcePosition(speakerPos, listenerPos);
                    velocity = GetRelativeVelocity(speakerPos, listenerPos, sourcePosition);
                }

                AL.Source(source, ALSource3f.Position, sourcePosition.X, sourcePosition.Y, sourcePosition.Z);
                OALW.CheckError("Error setting source pos");
                AL.Source(source, ALSource3f.Velocity, velocity.X, velocity.Y, velocity.Z);
                OALW.CheckError("Error setting source velocity");
                AL.Source(source, ALSourceb.SourceRelative, true);
                OALW.CheckError("Error making source relative to client");
            });
        }

        private float GetDistanceFactor()
        {
            // Distance in blocks at which audio source normally considered quiet.
            const float quietDistance = 10;
            float maxHearingDistance = WorldConfig.GetVoiceDistance(capi, voiceLevel);
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
            var dt = (currentTime - lastSpeakerUpdate).TotalSeconds;
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
            if (orderingQueue.ContainsKey(sequenceNumber))
            {
                Logger.client.Debug("Audio sequence already received, skipping enqueueing");
                return;
            }

            if (lastAudioSequenceNumber > sequenceNumber)
            {
                Logger.client.Debug("Audio sequence arrived too late, skipping enqueueing");
                return;
            }

            orderingQueue.Add(sequenceNumber, audio);
            capi.Event.EnqueueMainThreadTask(() =>
            {
                capi.Event.RegisterCallback(DequeueAudio, orderingDelay);
            }, "PlayerAudioSource EnqueueAudio");
        }

        public void DequeueAudio(float _)
        {
            AudioData audio;

            lock (orderingQueue.SyncRoot)
            {
                audio = orderingQueue.GetByIndex(0) as AudioData;
                lastAudioSequenceNumber = (long)orderingQueue.GetKey(0);
                orderingQueue.RemoveAt(0);
            }

            byte[] audioBytes = audio.data;
            buffer.QueueAudio(audioBytes, audioBytes.Length, audio.format, audio.frequency);

            var state = OALW.GetSourceState(source);
            // the source can stop playing if it finishes everything in queue
            if (state != ALSourceState.Playing)
            {
                StartPlaying();
            }
        }

        public void StartPlaying()
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                buffer.TryDequeueBuffers();
                OALW.SourcePlay(source);
            }, "PlayerAudioSource StartPlaying");
        }

        public void StopPlaying()
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                OALW.SourceStop(source);
            }, "PlayerAudioSource StopPlaying");
        }

        public void Dispose()
        {
            OALW.SourceStop(source);

            buffer?.Dispose();
            OALW.DeleteSource(source);
        }
    }
}