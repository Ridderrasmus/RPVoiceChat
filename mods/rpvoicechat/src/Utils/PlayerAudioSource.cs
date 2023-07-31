using System;
using OpenTK.Audio.OpenAL;
using rpvoicechat;
using OpenTK.Audio;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class PlayerAudioSource : IDisposable
{
    public const int BufferCount = 4;

    private readonly int source;
    private bool isPlaying = false;

    private CircularAudioBuffer buffer;
    //private ReverbEffect reverbEffect;

    private ICoreClientAPI capi;
    private Vec3f lastPos;
    private long gameTickId;
    public bool IsMuffled { get; set; } = false;
    public bool IsReverberated { get; set; } = false;

    public bool IsLocational;

    private IPlayer player;

    public PlayerAudioSource(IPlayer player, AudioOutputManager manager, ICoreClientAPI capi)
    {
        this.player = player;
        this.capi = capi;
        gameTickId = capi.Event.RegisterGameTickListener(UpdatePlayer, 20);

        lastPos = player.Entity.SidedPos.XYZFloat;

        source = AL.GenSource();
        buffer = new CircularAudioBuffer(source, BufferCount, capi);

        capi.ShowChatMessage(AudioContext.CurrentContext.CurrentDevice);
        
        AL.Source(source, ALSourceb.Looping, false);
        AL.Source(source, ALSourceb.SourceRelative, false);
        AL.Source(source, ALSourcef.Gain, 1.0f);
        AL.Source(source, ALSourcef.Pitch, 1.0f);

        //reverbEffect = new ReverbEffect(manager.EffectsExtension, source);
    }

    public void UpdatePlayer(float dt)
    {
        if (IsMuffled)
        {
        }

        if (IsReverberated)
        {
        }

        if (IsLocational)
        {
            var entityPos = player.Entity.SidedPos.XYZFloat;
            var direction = (entityPos - capi.World.Player.Entity.SidedPos.XYZFloat);
            direction.Normalize();

            var velocity = (lastPos - entityPos) / dt;
            lastPos = entityPos;

            AL.Source(source, ALSource3f.Position, entityPos.X, entityPos.Y, entityPos.Z);
            AL.Source(source, ALSource3f.Direction, direction.X, direction.Y, direction.Z);
            AL.Source(source, ALSource3f.Velocity, velocity.X, velocity.Y, velocity.Z);
        }
        else
        {
            AL.GetListener(ALListener3f.Position, out var Pos);
            AL.Source(source, ALSource3f.Position, ref Pos);
            AL.Source(source, ALSource3f.Velocity, 0, 0, 0);
        }
    }

    public void QueueAudio(byte[] audioBytes, int bufferLength)
    {
        if(!isPlaying)
            return;

        buffer.TryDequeBuffers();
        buffer.QueueAudio(audioBytes, bufferLength, ALFormat.Mono16, MicrophoneManager.Frequency);

        // the source can stop playing if it finishes everything in queue
        if (AL.GetSourceState(source) != ALSourceState.Playing && isPlaying)
        {
            StartPlaying();
        }
    }

    public void StartPlaying()
    {
        isPlaying = true;

        AL.SourcePlay(source);
        var error = AL.GetError();
        if (error != ALError.NoError)
        {
            capi.Logger.Error("Error playing source %s", error.ToString());
        }
    }

    public void StopPlaying()
    {
        isPlaying = false;
        AL.SourceStop(source);
    }

    public void Dispose()
    {
        AL.SourceStop(source);

        buffer.Dispose();
        AL.DeleteSource(source);

        capi.Event.UnregisterGameTickListener(gameTickId);
    }
}