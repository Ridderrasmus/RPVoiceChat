using System;
using OpenTK.Audio.OpenAL;
using rpvoicechat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using rpvoicechat.Utils;
using System.Collections.Generic;
using System.Threading;
using RPVoiceChat;

public class PlayerAudioSource : IDisposable
{
    public const int BufferCount = 20;

    private int source;

    public EffectsExtension EffectsExtension;

    private CircularAudioBuffer buffer;

    private ICoreClientAPI capi;
    private AudioOutputManager outputManager;
    private Thread dequeueAudioThread;

    private Vec3f lastSpeakerCoords;
    private DateTime lastSpeakerUpdate;
    public bool IsMuffled { get; set; } = false;
    public bool IsReverberated { get; set; } = false;

    public bool IsLocational { get; set; } = true;
    public VoiceLevel voiceLevel { get; private set; } = VoiceLevel.Talking;
    private static Dictionary<VoiceLevel, string> configKeyByVoiceLevel = new Dictionary<VoiceLevel, string>
    {
        { VoiceLevel.Whispering, "rpvoicechat:distance-whisper" },
        { VoiceLevel.Talking, "rpvoicechat:distance-talk" },
        { VoiceLevel.Shouting, "rpvoicechat:distance-shout" },
    };
    /// <summary>
    /// Distance in blocks at which audio source normally considered quiet. <br />
    /// Used in calculation of distanceFactor to set volume at the edge of hearing range.
    /// </summary>
    private const float quietDistance = 10;

    private IPlayer player;

    private FilterLowpass lowpassFilter;
    //private ReverbEffect reverbEffect;
    private EffectPitchShift drunkEffect;

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
            Util.CheckError("Error gen source", capi);
            buffer = new CircularAudioBuffer(source, BufferCount, capi);

            AL.Source(source, ALSourceb.Looping, false);
            Util.CheckError("Error setting source looping", capi);
            AL.Source(source, ALSourceb.SourceRelative, false);
            Util.CheckError("Error setting source SourceRelative", capi);
            AL.Source(source, ALSourcef.Gain, 1.0f);
            Util.CheckError("Error setting source Gain", capi);
            AL.Source(source, ALSourcef.Pitch, 1.0f);
            Util.CheckError("Error setting source Pitch", capi);

            //reverbEffect = new ReverbEffect(manager.EffectsExtension, source);
            drunkEffect = new EffectPitchShift(EffectsExtension, source);

            dequeueAudioThread.Start();
        }, "PlayerAudioSource Init");
    }

    public void UpdateVoiceLevel(VoiceLevel voiceLevel)
    {
        this.voiceLevel = voiceLevel;
        string key = configKeyByVoiceLevel[voiceLevel];

        capi.Event.EnqueueMainThreadTask(() =>
        {
            AL.Source(source, ALSourcef.MaxDistance, (float) capi.World.Config.GetInt(key));
            Util.CheckError("Error setting max audible distance", capi);
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
        lowpassFilter?.Stop();
        if (wallThickness != 0)
        {
            lowpassFilter = lowpassFilter ?? new FilterLowpass(EffectsExtension, source);
            lowpassFilter.Start();
            lowpassFilter.SetHFGain(Math.Max(1.0f - (wallThickness / 5), 0.1f));
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

        // If the player is drunk, then the player's voice should be affected
        // Values are temporary currently
        int drunkness = (int)(player.Entity.WatchedAttributes.GetFloat("intoxication") * 10);
        drunkEffect.SetPitchShift(0 - drunkness);
        Util.CheckError("Error setting source Pitch", capi);


        if (IsLocational)
        {
            var sourcePosition = GetRelativeSourcePosition(speakerPos, listenerPos);
            var velocity = GetRelativeVelocity(speakerPos, listenerPos, sourcePosition);

            // Adjust volume change due to distance based on speaker's voice level
            var distanceFactor = GetDistanceFactor();
            sourcePosition *= distanceFactor;
            velocity *= distanceFactor;

            AL.Source(source, ALSource3f.Position, sourcePosition.X, sourcePosition.Y, sourcePosition.Z);
            Util.CheckError("Error setting source pos", capi);

            AL.Source(source, ALSource3f.Velocity, velocity.X, velocity.Y, velocity.Z);
            Util.CheckError("Error setting source velocity", capi);

            AL.Source(source, ALSourceb.SourceRelative, true);
            Util.CheckError("Error making source relative to client", capi);
        }
        else
        {
            AL.Source(source, ALSource3f.Position, 0, 0, 0);
            Util.CheckError("Error setting source direction", capi);

            AL.Source(source, ALSource3f.Velocity, 0, 0, 0);
            Util.CheckError("Error setting source velocity", capi);

            AL.Source(source, ALSourceb.SourceRelative, true);
            Util.CheckError("Error making source relative to client", capi);
        }
    }

    private float GetDistanceFactor()
    {
        string configKey = configKeyByVoiceLevel[voiceLevel];
        float maxHearingDistance = capi.World.Config.GetInt(configKey);
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
            Util.CheckError("Error getting source state", capi);
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
            Util.CheckError("Error playing source", capi);
        }, "PlayerAudioSource StartPlaying");
    }

    public void StopPlaying()
    {
        capi.Event.EnqueueMainThreadTask(() =>
        {
            AL.SourceStop(source);
            Util.CheckError("Error stop playing source", capi);
        }, "PlayerAudioSource StopPlaying");
    }

    public void Dispose()
    {
        dequeueAudioThread?.Abort();
        AL.SourceStop(source);
        Util.CheckError("Error stop playing source", capi);

        buffer?.Dispose();
        AL.DeleteSource(source);
        Util.CheckError("Error deleting source", capi);
    }
}