using NAudio.Wave;
using rpvoicechat;
using System.Collections.Concurrent;
using Vintagestory.API.MathTools;

public class PlayerAudioSource
{
    public bool IsMuffled { get; set; } = false;
    public bool IsReverberated { get; set; } = false;
    public bool IsLocational { get; set; } = true;

    public Vec3d Position { get; set; }

    public ConcurrentQueue<AudioPacket> AudioQueue = new ConcurrentQueue<AudioPacket>();
    public BufferedWaveProvider Buffer = new BufferedWaveProvider(new WaveFormatStereo());
    public BufferedWaveProvider ReverbBuffer = new BufferedWaveProvider(new WaveFormatStereo());
    public ReverbEffect ReverbEffect;

    public PlayerAudioSource(Vec3d pos)
    {
        Position = pos;
        Buffer.DiscardOnBufferOverflow = true;
        ReverbBuffer.DiscardOnBufferOverflow = true;
        ReverbEffect = new ReverbEffect(500);
    }

}