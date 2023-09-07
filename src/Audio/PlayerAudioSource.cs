using System;
using OpenTK.Audio.OpenAL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using RPVoiceChat.Utils;
using System.Threading;

namespace RPVoiceChat.Audio
{
    public class PlayerAudioSource : IDisposable
    {
        public const int BufferCount = 20;

        private int source;

        public EffectsExtension EffectsExtension;

        private CircularAudioBuffer buffer;
        //private ReverbEffect reverbEffect;

        private ICoreClientAPI capi;
        private AudioOutputManager outputManager;
        private Thread dequeueAudioThread;

        private Vec3f lastSpeakerCoords;
        private DateTime lastSpeakerUpdate;
        public bool IsMuffled { get; set; } = false;
        public bool IsReverberated { get; set; } = false;

        public bool IsLocational { get; set; } = true;
        public VoiceLevel voiceLevel { get; private set; } = VoiceLevel.Talking;
        /// <summary>
        /// Distance in blocks at which audio source normally considered quiet. <br />
        /// Used in calculation of distanceFactor to set volume at the edge of hearing range.
        /// </summary>
        private const float quietDistance = 10;

        private IPlayer player;

        private FilterLowpass lowpassFilter;

        public PlayerAudioSource(IPlayer player, AudioOutputManager manager, ICoreClientAPI capi)
        {
            dequeueAudioThread = new Thread(DequeueAudio);
            EffectsExtension = manager.EffectsExtension;
            outputManager = manager;
            this.player = player;
            this.capi = capi;
            capi.Event.EnqueueMainThreadTask(() =>
            {
                lastSpeakerCoords = player.Entity?.SidedPos?.XYZFloat;
                lastSpeakerUpdate = DateTime.Now;

                source = AL.GenSource();
                Util.CheckError("Error gen source");
                buffer = new CircularAudioBuffer(source, BufferCount);

                AL.Source(source, ALSourceb.Looping, false);
                Util.CheckError("Error setting source looping");
                AL.Source(source, ALSourceb.SourceRelative, false);
                Util.CheckError("Error setting source SourceRelative");
                AL.Source(source, ALSourcef.Gain, 1.0f);
                Util.CheckError("Error setting source Gain");
                AL.Source(source, ALSourcef.Pitch, 1.0f);
                Util.CheckError("Error setting source Pitch");

            //reverbEffect = new ReverbEffect(manager.EffectsExtension, source);
            dequeueAudioThread.Start();
            }, "PlayerAudioSource Init");
        }

        public void UpdateVoiceLevel(VoiceLevel voiceLevel)
        {
            this.voiceLevel = voiceLevel;
            float distance = WorldConfig.GetVoiceDistance(capi, voiceLevel);

            capi.Event.EnqueueMainThreadTask(() =>
            {
                AL.Source(source, ALSourcef.MaxDistance, distance);
                Util.CheckError("Error setting max audible distance");
            }, "PlayerAudioSource update max distance");
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

            lowpassFilter?.Stop();
            if (wallThickness != 0)
            {
                lowpassFilter = lowpassFilter ?? new FilterLowpass(EffectsExtension, source);
                lowpassFilter.Start();
                lowpassFilter.SetHFGain(Math.Max(1.0f - (wallThickness / 2), 0.1f));
            }

            // If the player is in a reverberated area, then the player's voice should be reverberated
            if (IsReverberated)
            {

            }

            // If the player has a temporal stability of less than 0.7, then the player's voice should be distorted
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
            Util.CheckError("Error setting source Pitch", capi);
            */


            if (IsLocational)
            {
                var sourcePosition = GetRelativeSourcePosition(speakerPos, listenerPos);
                var velocity = GetRelativeVelocity(speakerPos, listenerPos, sourcePosition);

                // Adjust volume change due to distance based on speaker's voice level
                var distanceFactor = GetDistanceFactor();
                sourcePosition *= distanceFactor;
                velocity *= distanceFactor;

                AL.Source(source, ALSource3f.Position, sourcePosition.X, sourcePosition.Y, sourcePosition.Z);
                Util.CheckError("Error setting source pos");

                AL.Source(source, ALSource3f.Velocity, velocity.X, velocity.Y, velocity.Z);
                Util.CheckError("Error setting source velocity");

                AL.Source(source, ALSourceb.SourceRelative, true);
                Util.CheckError("Error making source relative to client");
            }
            else
            {
                AL.Source(source, ALSource3f.Position, 0, 0, 0);
                Util.CheckError("Error setting source direction");

                AL.Source(source, ALSource3f.Velocity, 0, 0, 0);
                Util.CheckError("Error setting source velocity");

                AL.Source(source, ALSourceb.SourceRelative, true);
                Util.CheckError("Error making source relative to client");
            }
        }

        private float GetDistanceFactor()
        {
            float maxHearingDistance = WorldConfig.GetVoiceDistance(capi, voiceLevel);
            var exponent = quietDistance < maxHearingDistance ? 2 : 0.5;
            var distanceFactor = Math.Pow(quietDistance, exponent) / Math.Pow(maxHearingDistance, exponent);

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

        public void QueueAudio(byte[] audioBytes, int bufferLength)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                buffer.QueueAudio(audioBytes, bufferLength, ALFormat.Mono16, MicrophoneManager.Frequency);

                var state = AL.GetSourceState(source);
                Util.CheckError("Error getting source state");
            // the source can stop playing if it finishes everything in queue
            if (state != ALSourceState.Playing)
                {
                    StartPlaying();
                }
            }, "PlayerAudioSource QueueAudio");
        }

        private void DequeueAudio()
        {
            while (dequeueAudioThread.IsAlive)
            {
                if (!outputManager.isReady)
                {
                    Thread.Sleep(100);
                    continue;
                }
                buffer.TryDequeueBuffers();

                Thread.Sleep(30);
            }
        }

        public void StartPlaying()
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                AL.SourcePlay(source);
                Util.CheckError("Error playing source");
            }, "PlayerAudioSource StartPlaying");
        }

        public void StopPlaying()
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                AL.SourceStop(source);
                Util.CheckError("Error stop playing source");
            }, "PlayerAudioSource StopPlaying");
        }

        public void Dispose()
        {
            dequeueAudioThread?.Abort();
            AL.SourceStop(source);
            Util.CheckError("Error stop playing source");

            buffer?.Dispose();
            AL.DeleteSource(source);
            Util.CheckError("Error deleting source");
        }
    }
}