using System;
using OpenTK.Audio.OpenAL;
using rpvoicechat;
using OpenTK.Audio;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using OpenTK;
using Vintagestory.API.Common.Entities;
using rpvoicechat.Utils;
using System.Collections.Generic;


public class PlayerAudioSource : IDisposable
{
    public const int BufferCount = 4;

    private int source;

    public EffectsExtension EffectsExtension;

    private CircularAudioBuffer buffer;
    //private ReverbEffect reverbEffect;

    private ICoreClientAPI capi;
    private AudioOutputManager outputManager;

    private Vec3f lastSpeakerCoords;
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

    public PlayerAudioSource(IPlayer player, AudioOutputManager manager, ICoreClientAPI capi)
    {
        EffectsExtension = manager.EffectsExtension;
        outputManager = manager;
        this.player = player;
        this.capi = capi;
        capi.Event.EnqueueMainThreadTask(() =>
        {
            lastSpeakerCoords = player.Entity?.SidedPos?.XYZFloat;

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
        if (speakerPos == null || listenerPos == null)
            return;

        // If the player is on the other side of something to the listener, then the player's voice should be muffled
        float wallThickness = LocationUtils.GetWallThickness(capi, player, capi.World.Player);
        if (wallThickness != 0)
        {
            lowpassFilter = lowpassFilter ?? new FilterLowpass(EffectsExtension, source);
            lowpassFilter.Start();
            lowpassFilter.SetHFGain(Math.Max(1.0f - (wallThickness / 2), 0.1f));
        } else
        {
            lowpassFilter?.Stop();
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
        float drunkness = player.Entity.WatchedAttributes.GetFloat("intoxication");
        if (drunkness > 0.2)
        {
            var pitch = 1 - (drunkness / 2);
            AL.Source(source, ALSourcef.Pitch, pitch);
            Util.CheckError("Error setting source Pitch", capi);
        } else
        {
            AL.Source(source, ALSourcef.Pitch, 1.0f);
            Util.CheckError("Error setting source Pitch", capi);
        }


        if (IsLocational)
        {
            if (lastSpeakerCoords == null)
            {
                lastSpeakerCoords = speakerPos.XYZFloat;
            }

            var speakerCoords = speakerPos.XYZFloat;
            //var velocity = (lastSpeakerCoords - speakerCoords) / dt;
            lastSpeakerCoords = speakerCoords;

            // Adjust volume change due to distance based on speaker's voice level
            string key = configKeyByVoiceLevel[voiceLevel];
            float maxHearingDistance = capi.World.Config.GetInt(key);
            float distanceFactor;
            if (quietDistance < maxHearingDistance)
                distanceFactor = (float)(Math.Pow(quietDistance, 2) / Math.Pow(maxHearingDistance, 2));
            else
                distanceFactor = (float)(Math.Pow(quietDistance, 0.5) / Math.Pow(maxHearingDistance, 0.5));

            var relativeSpeakerCoords = LocationUtils.GetRelativeSpeakerLocation(speakerPos, listenerPos);

            var sourcePosition = relativeSpeakerCoords * distanceFactor;

            AL.Source(source, ALSource3f.Position, sourcePosition.X, sourcePosition.Y, sourcePosition.Z);
            Util.CheckError("Error setting source pos", capi);

            /*AL.Source(source, ALSource3f.Velocity, velocity.X, velocity.Y, velocity.Z);
            Util.CheckError("Error setting source velocity", capi);*/

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

    public void QueueAudio(byte[] audioBytes, int bufferLength)
    {
        capi.Event.EnqueueMainThreadTask(() =>
        {
            buffer.TryDequeBuffers();
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
        AL.SourceStop(source);
        Util.CheckError("Error stop playing source", capi);

        buffer?.Dispose();
        AL.DeleteSource(source);
        Util.CheckError("Error deleting source", capi);
    }
}