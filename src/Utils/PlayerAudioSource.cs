using NAudio.Wave;
using rpvoicechat;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

public class PlayerAudioSource
{
    public bool IsMuffled { get; set; } = false;
    public bool IsReverberated { get; set; } = false;
    public bool IsLocational { get; set; } = true;

    public Vec3d Position { get; set; }

    public Queue<AudioPacket> AudioQueue = new Queue<AudioPacket>();
    public BufferedWaveProvider Buffer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioUtils.sampleRate, 2));
    public ReverbEffect ReverbEffect = new ReverbEffect(100);

    public PlayerAudioSource(Vec3d pos)
    {
        Position = pos;
        Buffer.DiscardOnBufferOverflow = true;
    }
}